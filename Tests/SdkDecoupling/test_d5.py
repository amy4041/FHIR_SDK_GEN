"""D5 must distinguish offline success, clean sources and an actual server read."""
import copy
from contextlib import redirect_stdout
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import test_d4

ROOT = Path(__file__).resolve().parents[2]


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / "eng" / file)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


d5 = module("test_d5_gate", "Test-SdkDecouplingD5.py")
server = module("test_d5_server", "Test-FhirDeliveryServer.py")


class D5AcceptanceTests(unittest.TestCase):
    def setUp(self):
        (ROOT / "artifacts").mkdir(exist_ok=True)

    def cli_fixture(self, root, all_compositions=False):
        bundle = root / "bundle"
        bundle.mkdir()
        manifest = test_d4.D4EvidenceTests().fixture(bundle, "Dual" if all_compositions else "R4")
        if all_compositions:
            manifest["compositions"] = {n: d5.delivery.composition(n) for n in d5.delivery.COMPOSITIONS}
            name = d5.delivery.IG_DLL
            (bundle / "sdk" / name).write_bytes(name.encode())
            manifest["assemblyIdentities"][name] = Path(name).stem + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
        for name in ("NuGet.Config", "examples/SdkReferences.props", "examples/Shared/Program.cs", "examples/R4/R4Example.csproj"):
            path = bundle / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("fixture", encoding="utf-8")
        manifest.update(sourceRevision="revision", sourceFingerprint=d5.source.fingerprint([]), workingTreeDirty=False)
        manifest["files"] = d5.delivery.inventory(bundle)
        (bundle / "delivery-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        return bundle, manifest

    def change_manifest(self, bundle, scenario):
        path = bundle / "delivery-manifest.json"
        if scenario == "whitespace":
            path.write_bytes(path.read_bytes() + b"\n")
        elif scenario == "revision":
            data = json.loads(path.read_text(encoding="utf-8"))
            data["sourceRevision"] = "changed-during-verification"
            path.write_text(json.dumps(data), encoding="utf-8")

    def fixture(self):
        manifest = {"sourceRevision": "revision", "sourceFingerprint": {"sha256": "source"},
                    "toolPackageSha256": "package", "runtimeCompilerReferenceSha256": "reference",
                    "compositions": {n: d5.delivery.composition(n) for n in d5.delivery.COMPOSITIONS}}
        report = {"status": "Passed", "deliveryManifestSha256": "manifest", "externalServer": "NotRun",
                  "sourceRevision": "revision", "sourceFingerprint": {"sha256": "source"},
                  "outsideRepository": True, "isolatedCache": True, "inheritedEndpointsDisabled": True,
                  "canonicalInstall": {"emptyInitialCache": True, "localFeedOnly": True,
                                       "toolPackageSha256": "package", "compilerReferenceSha256": "reference"},
                  "examples": {}, "generation": {}}
        for name, composition in manifest["compositions"].items():
            report["examples"][name] = {"status": "Passed", "requiredDlls": composition["requiredDlls"],
                "versions": composition["fhirVersions"], "dllReferencesOnly": True, "publishedExecution": True,
                "onlyRequiredInputs": True, "missingDependencies": [{"assembly": n, "exitCode": 1, "diagnosed": True}
                                                                     for n in composition["requiredDlls"]]}
        for release in d5.delivery.VERSIONS:
            report["generation"][release] = {"status": "Passed", "sourceCount": 1,
                "repeatedGenerationMatches": True, "matchesCommittedModels": True}
        return manifest, report

    def test_complete_offline_evidence_is_accepted_without_claiming_server_success(self):
        manifest, report = self.fixture()
        d5.validate_offline(manifest, report, "manifest")
        self.assertEqual("NotRun", report["externalServer"])

    def test_mismatched_source_manifest_install_or_inherited_endpoints_are_rejected(self):
        manifest, original = self.fixture()
        changes = [("sourceRevision", "old"), ("sourceFingerprint", {}), ("deliveryManifestSha256", "old"),
                   ("externalServer", "Passed"), ("outsideRepository", False), ("isolatedCache", False),
                   ("inheritedEndpointsDisabled", False), ("canonicalInstall", {})]
        for name, value in changes:
            with self.subTest(field=name), self.assertRaises(ValueError):
                d5.validate_offline(manifest, original | {name: value}, "manifest")

    def test_all_four_deployments_generation_and_missing_dependency_failures_are_required(self):
        manifest, original = self.fixture()
        for name in d5.delivery.COMPOSITIONS:
            report = copy.deepcopy(original)
            report["examples"].pop(name)
            with self.assertRaises(ValueError):
                d5.validate_offline(manifest, report, "manifest")
        for key in ("generation", "examples"):
            with self.assertRaises(ValueError):
                d5.validate_offline(manifest, original | {key: {}}, "manifest")
        report = copy.deepcopy(original)
        report["examples"]["R4"]["missingDependencies"][0]["exitCode"] = 0
        with self.assertRaises(ValueError):
            d5.validate_offline(manifest, report, "manifest")
        manifest["compositions"].pop("R4TwCore")
        with self.assertRaises(ValueError):
            d5.validate_offline(manifest, original, "manifest")

    def test_server_configuration_is_explicit_single_version_and_rejects_paths_as_ids(self):
        config = {"MYFHIRSDK_R4_INTEGRATION_BASE_URL": "https://example.test/fhir/",
                  "MYFHIRSDK_R4_PATIENT_ID": "synthetic-patient.1",
                  "MYFHIRSDK_R4_INTEGRATION_BEARER_TOKEN": "test-token",
                  "MYFHIRSDK_R5_INTEGRATION_BEARER_TOKEN": "must-not-inherit"}
        selected = server.configuration("R4", config)
        self.assertNotIn("MYFHIRSDK_R5_INTEGRATION_BEARER_TOKEN", selected)
        for field, value in (("MYFHIRSDK_R4_PATIENT_ID", ""), ("MYFHIRSDK_R4_PATIENT_ID", "../Patient/other"),
                             ("MYFHIRSDK_R4_PATIENT_ID", "x" * 65),
                             ("MYFHIRSDK_R4_INTEGRATION_BASE_URL", "file:///tmp/"),
                             ("MYFHIRSDK_R4_INTEGRATION_BASE_URL", "https://user:secret@example.test/fhir/"),
                             ("MYFHIRSDK_R4_INTEGRATION_BASE_URL", "https://example.test/fhir/?token=secret")):
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                server.configuration("R4", config | {field: value})
        with self.assertRaises(ValueError):
            server.configuration("R5", config)

    def test_real_server_report_requires_actual_read_version_check_and_delivered_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest, deployed, report = test_d4.D4EvidenceTests().recorded_execution_fixture(Path(tmp))
            for assembly in report["assemblies"]:
                assembly["path"] = str(deployed / (assembly["name"] + ".dll"))
            remote = {"status": "Passed", "operation": "GET metadata; GET Patient/{id}", "versionChecked": True}
            report["releases"][0]["externalServer"] = remote
            server.validate_server_report("R4", report, deployed, manifest)
            for changed in ({}, remote | {"status": "NotRun"}, remote | {"versionChecked": False}):
                wrong = copy.deepcopy(report)
                wrong["releases"][0]["externalServer"] = changed
                with self.assertRaises(ValueError):
                    server.validate_server_report("R4", wrong, deployed, manifest)
            (deployed / "MyFhirSdk.dll").write_bytes(b"different")
            with self.assertRaisesRegex(ValueError, "different delivery"):
                server.validate_server_report("R4", report, deployed, manifest)

    def test_ci_keeps_d5_evidence_outside_immutable_d4_inventory_and_never_runs_server(self):
        yaml = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        self.assertIn("Test-SdkDecouplingD5.py --bundle artifacts/sdk-decoupling/d4/ci/bundle --output artifacts/sdk-decoupling/d5/ci", yaml)
        self.assertNotIn("Test-FhirDeliveryServer.py", yaml)
        self.assertNotIn("--allow-working-tree", yaml)

    def test_offline_cli_rejects_manifest_bytes_changed_during_child_verification(self):
        for scenario in ("unchanged", "whitespace", "revision"):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as tmp:
                root = Path(tmp)
                bundle, manifest = self.cli_fixture(root, all_compositions=True)
                output = root / "output"
                _, report = self.fixture()
                report.update(sourceFingerprint=manifest["sourceFingerprint"],
                              deliveryManifestSha256=d5.delivery.sha(bundle / "delivery-manifest.json"))
                report["canonicalInstall"].update(toolPackageSha256=manifest["toolPackageSha256"],
                    compilerReferenceSha256=manifest["runtimeCompilerReferenceSha256"])
                def child(arguments, **kwargs):
                    evidence = Path(arguments[arguments.index("--output") + 1])
                    evidence.mkdir()
                    (evidence / "summary.json").write_text(json.dumps(report), encoding="utf-8")
                    self.change_manifest(bundle, scenario)
                    return subprocess.CompletedProcess(arguments, 0, stdout="", stderr="")
                def git(arguments):
                    return "revision" if arguments[1] == "rev-parse" else ""
                with patch.object(sys, "argv", ["d5", "--bundle", str(bundle), "--output", str(output)]), \
                     patch.object(d5.source, "run", side_effect=git), patch.object(d5.source, "source_inventory", return_value=[]), \
                     patch.object(d5.subprocess, "run", side_effect=child), redirect_stdout(io.StringIO()):
                    code = d5.main()
                summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
                self.assertEqual(0 if scenario == "unchanged" else 1, code)
                self.assertEqual("Passed" if scenario == "unchanged" else "Failed", summary["status"])
                if scenario != "unchanged":
                    self.assertNotEqual("Passed", summary["offlineAcceptance"])
                    self.assertIn("manifest changed", summary["error"])

    def test_server_cli_rejects_changed_manifest_and_records_safe_failure_stages(self):
        expected = {"unchanged": None, "whitespace": ("ManifestIntegrity", "ManifestChanged"),
                    "revision": ("ManifestIntegrity", "ManifestChanged"), "publish": ("Publish", "PublishFailed"),
                    "timeout": ("ServerExecution", "Timeout"), "execution": ("ServerExecution", "ExecutionFailed"),
                    "metadata": ("MetadataVersion", "VersionMismatch"), "patient": ("PatientIdentity", "PatientMissing"),
                    "report": ("ReportParsing", "InvalidExecutionReport")}
        for scenario, failure in expected.items():
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as tmp:
                root = Path(tmp)
                bundle, manifest = self.cli_fixture(root)
                output = root / "output"
                def child(arguments, **kwargs):
                    if "publish" in arguments:
                        if scenario == "publish":
                            return subprocess.CompletedProcess(arguments, 23, stdout="private-build-details", stderr="")
                        deployed = Path(arguments[arguments.index("-o") + 1])
                        deployed.mkdir()
                        for dll in manifest["assemblyIdentities"]:
                            shutil.copy2(bundle / "sdk" / dll, deployed / dll)
                        deps = {"targets": {"net9.0": {"fixture": {"runtime": {n: {} for n in manifest["assemblyIdentities"]}}}}}
                        (deployed / "R4Example.deps.json").write_text(json.dumps(deps), encoding="utf-8")
                        return subprocess.CompletedProcess(arguments, 0, stdout="", stderr="")
                    if scenario == "timeout":
                        raise subprocess.TimeoutExpired(arguments, 120, output="private-server-body")
                    if scenario in ("execution", "metadata", "patient"):
                        marker = ""
                        if scenario != "execution":
                            marker = "D5_SERVER_FAILURE " + json.dumps(dict(zip(("stage", "code"), failure)))
                        return subprocess.CompletedProcess(arguments, 17, stdout="private-server-body", stderr=marker)
                    if scenario == "report":
                        return subprocess.CompletedProcess(arguments, 0, stdout="private-invalid-json", stderr="")
                    deployed = Path(kwargs["cwd"])
                    report = {"status": "Passed", "releases": [{"fhirVersion": "4.0.1", "modelAssembly": "MyFhirSdk.R4.Models",
                        "roundTrip": True, "validation": True, "typedClient": True, "externalServer": {
                            "status": "Passed", "operation": "GET metadata; GET Patient/{id}", "versionChecked": True}}], "assemblies": []}
                    for dll, identity in manifest["assemblyIdentities"].items():
                        report["assemblies"].append({"name": Path(dll).stem, "identity": identity, "path": str(deployed / dll),
                            "references": sorted(Path(n).stem for n in server.delivery.NEUTRAL - {"MyFhirSdk.dll"}) if dll == "MyFhirSdk.dll" else []})
                    self.change_manifest(bundle, scenario)
                    return subprocess.CompletedProcess(arguments, 0, stdout=json.dumps(report), stderr="")
                selected = {"MYFHIRSDK_R4_INTEGRATION_BASE_URL": "http://127.0.0.1:1/fhir/",
                            "MYFHIRSDK_R4_PATIENT_ID": "private-synthetic-id", "MYFHIRSDK_R4_INTEGRATION_BEARER_TOKEN": "private-token"}
                with patch.dict(os.environ, selected), patch.object(sys, "argv", ["server", "--bundle", str(bundle),
                        "--output", str(output), "--release", "R4", "--synthetic-patient"]), \
                     patch.object(server.subprocess, "run", side_effect=child), redirect_stdout(io.StringIO()):
                    code = server.main()
                text = (output / "summary.json").read_text(encoding="utf-8")
                summary = json.loads(text)
                self.assertEqual(0 if failure is None else 1, code)
                self.assertEqual("Passed" if failure is None else "Failed", summary["status"])
                if failure:
                    self.assertEqual(failure, (summary["failureStage"], summary["errorCode"]))
                    self.assertNotIn("patientRead", summary)
                    if scenario in ("publish", "execution", "metadata", "patient"):
                        self.assertEqual(23 if scenario == "publish" else 17, summary["processExitCode"])
                for secret in ("private-", "127.0.0.1"):
                    self.assertNotIn(secret, text)

    def test_remote_failure_diagnostics_only_accept_fixed_vocabulary(self):
        for raw in ("remote secret", 'D5_SERVER_FAILURE {"stage":"secret","code":"secret"}',
                    'D5_SERVER_FAILURE {"stage":[],"code":{}}', "D5_SERVER_FAILURE null", "D5_SERVER_FAILURE invalid"):
            self.assertEqual(("ServerExecution", "ExecutionFailed"), server.remote_failure(raw, ""))
        marker = 'D5_SERVER_FAILURE {"stage":"PatientIdentity","code":"PatientIdMismatch","body":"private-body"}'
        self.assertEqual(("PatientIdentity", "PatientIdMismatch"), server.remote_failure("", marker))


if __name__ == "__main__":
    unittest.main()
