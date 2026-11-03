"""Reject delivery composition drift, unsafe manifests and incomplete D4 acceptance."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


gate = module("test_d4_gate", "eng/Test-SdkDecouplingD4.py")
contract = gate.delivery
comparison = module("test_d4_comparison", "eng/Compare-SdkDecouplingD4.py")


class D4EvidenceTests(unittest.TestCase):
    def recorded_execution_fixture(self, root):
        manifest = self.fixture(root)
        deployed = root / "deployment"
        deployed.mkdir()
        dlls = contract.composition("R4")["requiredDlls"]
        for dll in dlls:
            shutil.copy2(root / "sdk" / dll, deployed / dll)
        deps = {"targets": {"net9.0": {"fixture": {"runtime": {dll: {} for dll in dlls}}}}}
        (deployed / "R4Example.deps.json").write_text(json.dumps(deps), encoding="utf-8")
        report = {"status": "Passed", "releases": [{"fhirVersion": "4.0.1", "modelAssembly": "MyFhirSdk.R4.Models",
                   "roundTrip": True, "validation": True, "typedClient": True}],
                  "assemblies": [{"name": Path(dll).stem, "identity": manifest["assemblyIdentities"][dll],
                                  "path": "<deployment>/" + dll,
                                  "references": sorted(Path(n).stem for n in contract.NEUTRAL - {"MyFhirSdk.dll"}) if dll == "MyFhirSdk.dll" else []}
                                 for dll in dlls]}
        return manifest, deployed, report

    def fixture(self, root, name="R4"):
        def write(path, value):
            path = root / path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(value), encoding="utf-8")
        dlls = contract.composition(name)["requiredDlls"]
        (root / "sdk").mkdir()
        for dll in dlls:
            (root / "sdk" / dll).write_bytes(dll.encode())
        descriptor = {"targetFramework": "net9.0", "compatibility": {"toolVersion": "1.3.1"}, "compilerReference": {"sha256": "a" * 64}}
        write("contracts/runtime-contract.json", descriptor)
        write("global.json", {"sdk": {"version": "9.0.317"}})
        (root / "feed").mkdir()
        (root / "feed/tool.nupkg").write_bytes(b"fixture")
        inputs = []
        for release in contract.composition(name)["fhirVersions"]:
            digest = hashlib.sha256(release.encode()).hexdigest()
            write(f"inputs/{release.lower()}-package-lock.json", {"sha256": digest})
            (root / f"inputs/hl7.fhir.{release.lower()}.core-{contract.VERSIONS[release]}.tgz").write_bytes(release.encode())
            inputs.append({"release": release, "packageId": f"hl7.fhir.{release.lower()}.core", "version": contract.VERSIONS[release], "sha256": digest})
        return {"schemaVersion": 2, "compositions": {name: contract.composition(name)}, "files": contract.inventory(root),
                "assemblyIdentities": {n: Path(n).stem + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" for n in dlls},
                "toolPackage": "feed/tool.nupkg", "toolPackageSha256": contract.sha(root / "feed/tool.nupkg"),
                "dotnetSdk": "9.0.317", "targetFramework": "net9.0", "toolVersion": "1.3.1",
                "runtimeCompilerReferenceSha256": "a" * 64, "fhirInputs": inputs}

    def test_all_compositions_have_only_required_models_and_optional_r4_ig(self):
        for name in contract.COMPOSITIONS:
            with self.subTest(composition=name), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                manifest = self.fixture(root, name)
                contract.validate_manifest(root, manifest)
                if name == "R4":
                    self.assertNotIn("MyFhirSdk.R5.Models.dll", manifest["assemblyIdentities"])
                if name == "R5":
                    self.assertNotIn(contract.IG_DLL, manifest["assemblyIdentities"])

    def test_adding_models_even_with_rehashed_manifest_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest = self.fixture(root)
            extra = root / "sdk/MyFhirSdk.R5.Models.dll"
            extra.write_bytes(b"unexpected")
            manifest["files"] = contract.inventory(root)
            with self.assertRaisesRegex(ValueError, "DLL set"):
                contract.validate_manifest(root, manifest)
            for bad in ("../outside", "/absolute", "sdk/../escape", "sdk\\escape", "C:/escape"):
                with self.assertRaisesRegex(ValueError, "Unsafe"):
                    contract.safe_path(root, bad)

    def test_tampered_missing_unlisted_files_and_false_versions_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            original = self.fixture(root)
            for field, value in (("dotnetSdk", "10.0.100"), ("toolVersion", "other"), ("runtimeCompilerReferenceSha256", "0" * 64)):
                manifest = copy.deepcopy(original)
                manifest[field] = value
                with self.assertRaises(ValueError):
                    contract.validate_manifest(root, manifest)
            for key in ("files", "assemblyIdentities"):
                manifest = copy.deepcopy(original)
                manifest[key].pop(next(iter(manifest[key])))
                with self.assertRaises(ValueError):
                    contract.validate_manifest(root, manifest)
            (root / "sdk/MyFhirSdk.dll").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "integrity"):
                contract.validate_manifest(root, original)

    def test_only_external_template_props_are_exempt_from_stable_drift(self):
        before = {"examples/external/SdkReferences.props": b"old", "Runtime/keep.cs": b"same", "Directory.Build.props": b"same"}
        current = before | {"examples/external/SdkReferences.props": b"new"}
        self.assertEqual([], gate.stable_drift(before, current))
        for path in ("Runtime/keep.cs", "Directory.Build.props"):
            self.assertIn(path, gate.stable_drift(before, current | {path: b"changed"}))

    def test_fresh_integration_requires_every_current_case_without_skips(self):
        expected = {name + ":case": ["Passed"] for name in gate.FRESH_PROJECTS}
        self.assertEqual(3, gate.assert_fresh_cases(expected, expected))
        for actual in ({}, dict(list(expected.items())[:2]), expected | {next(iter(expected)): ["NotExecuted"]},
                       expected | {"wrong:case": ["Passed"]}, expected | {next(iter(expected)): ["Passed", "Passed"]}):
            with self.assertRaises(ValueError):
                gate.assert_fresh_cases(expected, actual)

    def test_package_rejects_wrong_runtime_reference_and_embedded_models(self):
        with tempfile.TemporaryDirectory() as tmp:
            package = Path(tmp) / "tool.nupkg"
            reference = b"reference"
            descriptor = {"compatibility": {"toolVersion": "1.3.1"}, "compilerReference": {"sha256": hashlib.sha256(reference).hexdigest()}}
            def pack(extra=False):
                with zipfile.ZipFile(package, "w") as archive:
                    archive.writestr("tool.nuspec", "<package><metadata><id>MyFhirSdk.CodeGen.Tool</id><version>1.3.1</version></metadata></package>")
                    archive.writestr("tools/net9.0/any/Assets/RuntimeReferences/net9.0/MyFhirSdk.Runtime.dll", reference)
                    if extra:
                        archive.writestr("tools/MyFhirSdk.R5.Models.dll", b"bad")
            pack()
            contract.validate_tool(package, descriptor)
            with self.assertRaisesRegex(ValueError, "reference"):
                contract.validate_tool(package, descriptor | {"compilerReference": {"sha256": "0" * 64}})
            pack(True)
            with self.assertRaisesRegex(ValueError, "embeds"):
                contract.validate_tool(package, descriptor)

    def test_renamed_package_is_staged_with_nuget_identity_filename_and_identical_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            package = root / "canonical-tool.nupkg"
            reference = b"reference"
            descriptor = {"compatibility": {"toolVersion": "1.3.1"}, "compilerReference": {"sha256": hashlib.sha256(reference).hexdigest()}}
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("tool.nuspec", "<package><metadata><id>MyFhirSdk.CodeGen.Tool</id><version>1.3.1</version></metadata></package>")
                archive.writestr("tools/net9.0/any/Assets/RuntimeReferences/net9.0/MyFhirSdk.Runtime.dll", reference)
            staged = contract.stage_tool(package, root / "feed", descriptor)
            self.assertEqual("MyFhirSdk.CodeGen.Tool.1.3.1.nupkg", staged.name)
            self.assertEqual(package.read_bytes(), staged.read_bytes())

    def test_recorded_execution_validates_before_rebasing_and_preserves_original_report(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest, deployed, report = self.recorded_execution_fixture(Path(tmp))
            original = copy.deepcopy(report)
            comparison.validate_recorded_execution("R4", report, manifest, deployed)
            self.assertEqual(original, report)

    def test_recorded_repository_paths_wrong_basenames_and_false_identities_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            manifest, deployed, report = self.recorded_execution_fixture(Path(tmp))
            for field, value in (("path", str(ROOT / "bin/Release/net9.0/MyFhirSdk.dll")),
                                 ("path", "<deployment>/MyFhirSdk.R5.Models.dll"),
                                 ("identity", "MyFhirSdk, Version=999.0.0.0, Culture=neutral, PublicKeyToken=null")):
                with self.subTest(field=field, value=value):
                    changed = copy.deepcopy(report)
                    next(a for a in changed["assemblies"] if a["name"] == "MyFhirSdk")[field] = value
                    with self.assertRaisesRegex(ValueError, "Recorded assembly"):
                        comparison.validate_recorded_execution("R4", changed, manifest, deployed)

    def test_readme_generator_commands_select_only_manifest_inputs(self):
        text = (ROOT / "examples/external/README.md").read_text(encoding="utf-8")
        block = next(b for b in re.findall(r"```powershell\n(.*?)\n```", text, flags=re.S) if "$previousPackages" in b)
        powershell = os.getenv("MYFHIRSDK_TEST_PWSH") or shutil.which("pwsh")
        self.assertTrue(powershell, "PowerShell 7 is required to verify shipped generator instructions")
        for release, version in contract.VERSIONS.items():
            with self.subTest(release=release), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                manifest = {"fhirInputs": [{"release": release, "version": version, "packageId": f"hl7.fhir.{release.lower()}.core"}]}
                (root / "delivery-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
                script = root / "commands.ps1"
                script.write_text("$ErrorActionPreference = 'Stop'\n$script:Calls = [Collections.Generic.List[string]]::new()\n"
                                  "function dotnet { $script:Calls.Add(($args -join '|')); $global:LASTEXITCODE = 0 }\n"
                                  + block + "\nConvertTo-Json -InputObject $script:Calls.ToArray()\n", encoding="utf-8")
                result = subprocess.run([powershell, "-NoProfile", "-File", str(script)], cwd=root,
                                        capture_output=True, encoding="utf-8", errors="replace", timeout=30)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                calls = json.loads(result.stdout)
                self.assertEqual(3, len(calls))
                self.assertTrue(calls[0].startswith("tool|restore|"))
                for mode, call in zip(("model", "primitive"), calls[1:]):
                    self.assertIn("--mode|" + mode, call)
                    self.assertIn(f"--input|./inputs/hl7.fhir.{release.lower()}.core-{version}.tgz", call)
                    self.assertIn("--fhir-version|" + version, call)

    def test_comparison_rejects_source_drift_and_same_platform(self):
        fields = {"os": "Windows", "sourceRevision": "abc", "sourceFingerprint": {}, "toolPackageSha256": "a",
                  "compilerReferenceSha256": "b", "sdkVersion": "9.0.317", "rollbackRevision": gate.D3_REVISION,
                  "testCounts": {}, "freshIntegrationTests": 201}
        for changed in (fields, fields | {"os": "Linux", "toolPackageSha256": "other"}):
            with patch.object(comparison, "validate_leg", side_effect=[fields, changed]):
                with self.assertRaises(ValueError):
                    comparison.compare(Path("windows"), Path("linux"))

    def test_platform_validation_reads_real_trx_with_relative_and_absolute_artifact_paths(self):
        artifacts = ROOT / "artifacts"
        artifacts.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="d4-relative-path-", dir=artifacts) as tmp:
            root = Path(tmp)
            results = root / "neutral-sdk/k3/test-results"
            results.mkdir(parents=True)
            assemblies = comparison.gate.d3.regression.EXPECTED_TESTS
            for assembly in assemblies:
                run = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
                definitions = ET.SubElement(run, "TestDefinitions")
                test = ET.SubElement(definitions, "UnitTest", id="case")
                ET.SubElement(test, "TestMethod", codeBase=assembly + ".dll")
                rows = ET.SubElement(run, "Results")
                ET.SubElement(rows, "UnitTestResult", testId="case", testName="fixture", outcome="Passed")
                summary = ET.SubElement(run, "ResultSummary")
                ET.SubElement(summary, "Counters", total="1", passed="1", failed="0")
                ET.ElementTree(run).write(results / (assembly + ".trx"), encoding="utf-8", xml_declaration=True)
            counts = {"total": len(assemblies), "passed": len(assemblies), "failed": 0, "skipped": 0}
            platform = {"gate": "D4-platform", "status": "Passed", "os": "Windows", "sourceFingerprint": {}, "testCounts": counts}
            neutral = {"status": "Passed", "fullyDecoupled": True, "sourceFingerprint": {}, "testCounts": counts}
            for path, data in ((root / "summary.json", platform), (root / "neutral-sdk/summary.json", neutral)):
                path.write_text(json.dumps(data), encoding="utf-8")
            (root / "evidence-files.json").write_text(json.dumps(contract.inventory(root)), encoding="utf-8")

            class RequiredCoverageReached(Exception):
                pass

            # Stop at the next guard after real nine-assembly TRX parsing and count validation.
            for argument in (root, Path(os.path.relpath(root, Path.cwd()))):
                with self.subTest(path=str(argument)), patch.object(comparison.gate.d3, "required_test_evidence",
                                                                    side_effect=RequiredCoverageReached):
                    with self.assertRaises(RequiredCoverageReached):
                        comparison.validate_leg(argument)

    def test_ci_pins_history_and_uses_one_canonical_package_for_current_platforms(self):
        yaml = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        jobs = dict(re.findall(r"^  ([\w-]+):\n(.*?)(?=^  [\w-]+:|\Z)", yaml, flags=re.M | re.S))
        self.assertIn("ref: " + gate.D3_REVISION, jobs["sdk-decoupling-d3"])
        self.assertNotIn("ref:", jobs["sdk-decoupling-d4"])
        self.assertIn("needs: compiler-reference-asset", jobs["sdk-decoupling-d4"])
        self.assertIn("canonical-tool-package", jobs["sdk-decoupling-d4"])
        self.assertIn("include-hidden-files: true", jobs["sdk-decoupling-d4"])
        self.assertIn("Compare-SdkDecouplingD4.py", jobs["sdk-decoupling-d4-compare"])


if __name__ == "__main__":
    unittest.main()
