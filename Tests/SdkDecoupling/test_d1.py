"""D1 acceptance failure modes; no relaxation of historical API gates."""
import importlib.util
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("d1", ROOT / "eng/Test-SdkDecouplingD1.py")
d1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d1)


class D1EvidenceTests(unittest.TestCase):
    def required_evidence(self, rows):
        """Create independent TRX definitions/results, including assembly identities."""
        with tempfile.TemporaryDirectory() as tmp:
            root = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
            definitions = ET.SubElement(root, "TestDefinitions")
            results = ET.SubElement(root, "Results")
            for index, (name, assembly, outcome) in enumerate(rows):
                test_id = str(index)
                definition = ET.SubElement(definitions, "UnitTest", id=test_id)
                ET.SubElement(definition, "TestMethod", codeBase="C:\\tests\\" + assembly + ".dll")
                ET.SubElement(results, "UnitTestResult", testId=test_id, testName=name, outcome=outcome)
            path = Path(tmp) / "tests.trx"
            ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
            return d1.required_test_evidence([path])

    def required_rows(self):
        return [(name, assembly, "Passed") for name, assembly in d1.required_tests().items()]

    def test_required_cases_pass_once_with_exact_assembly_and_parameters(self):
        rows = self.required_rows()
        self.assertEqual(37, len(rows))
        evidence = self.required_evidence(rows)
        self.assertEqual([], evidence["errors"])
        self.assertEqual(37, evidence["passed"])

    def test_missing_all_d1_results_cannot_pass(self):
        evidence = self.required_evidence([])
        self.assertEqual(37, len(evidence["errors"]))
        self.assertEqual(0, evidence["passed"])

    def test_missing_each_individual_case_cannot_pass(self):
        rows = self.required_rows()
        for index, (name, _, _) in enumerate(rows):
            with self.subTest(case=name):
                evidence = self.required_evidence(rows[:index] + rows[index + 1:])
                self.assertEqual(36, evidence["passed"])
                self.assertTrue(evidence["errors"])

    def test_duplicate_skipped_failed_or_wrong_assembly_cannot_pass(self):
        rows = self.required_rows()
        name, assembly, _ = rows[0]
        changes = (rows + [rows[0]], [(name, assembly, "NotExecuted")] + rows[1:],
                   [(name, assembly, "Failed")] + rows[1:], [(name, "Other.Tests", "Passed")] + rows[1:])
        for changed in changes:
            with self.subTest(result=changed[0], count=len(changed)):
                self.assertTrue(self.required_evidence(changed)["errors"])

    def test_unknown_parameter_variant_cannot_substitute_required_case(self):
        rows = self.required_rows()
        name, assembly, _ = rows[0]
        changed = [(name.replace("r5: False", "r5: Unknown"), assembly, "Passed")] + rows[1:]
        self.assertTrue(self.required_evidence(changed)["errors"])

    def test_shared_build_inputs_detect_edits_additions_and_deletions(self):
        for path in ("Directory.Build.props", "Directory.Build.targets", "eng/Shared.props", "other/Shared.TARGETS"):
            with self.subTest(path=path):
                pinned = {path: b"<Project>\n</Project>\n"}
                self.assertEqual([], d1.stable_drift(pinned, {path: b"<Project>\r\n</Project>\r\n"}))
                self.assertEqual([path], d1.stable_drift(pinned, {}))
                self.assertEqual([path], d1.stable_drift({}, pinned))
                changed = b'<Project><PropertyGroup Condition="\'$(MSBuildProjectName)\' == \'MyFhirSdk.R4.Models\'">' \
                          b'<AssemblyVersion>2.0.0.0</AssemblyVersion></PropertyGroup></Project>'
                self.assertEqual([path], d1.stable_drift(pinned, {path: changed}))

    def test_stable_sources_detect_addition_deletion_and_edits(self):
        pinned = {"Models/R4/Model.cs": b"old\n", "Runtime/Kernel.cs": b"kernel\n",
                  "Tests/CodeGen/Fixtures/FhirPackages/R4/core.tgz": b"\xff\0binary"}
        self.assertEqual([], d1.stable_drift(pinned, {p: b.replace(b"\n", b"\r\n") for p, b in pinned.items()}))
        for current in ({}, pinned | {"Models/R4/extra.cs": b"extra"},
                        pinned | {"Runtime/Kernel.cs": b"changed\n"}):
            self.assertTrue(d1.stable_drift(pinned, current))

    def test_api_delta_cannot_accept_unknown_addition_or_removal(self):
        sdk = (d1.d0.BASELINE / "full-sdk-api.txt").read_text(encoding="utf-8")
        aggregate = (d1.d0.BASELINE / "snapshots/Tests/Architecture/ApprovedPublicApi.txt").read_text(encoding="utf-8")
        with tempfile.TemporaryDirectory() as tmp, patch.object(d1, "BASELINE", Path(tmp)):
            d1.validate_api(sdk, aggregate, record=True)
            d1.validate_api(sdk, aggregate)
            for changed in (sdk + "TYPE unreviewed\n", "\n".join(sdk.splitlines()[1:]) + "\n"):
                with self.assertRaisesRegex(ValueError, "Unapproved D1 API drift"):
                    d1.validate_api(changed, aggregate)
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                d1.validate_api(sdk, aggregate, record=True)

    def test_current_and_historical_ci_sources_are_explicit(self):
        yaml = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        jobs = dict(re.findall(r"^  ([\w-]+):\n(.*?)(?=^  [\w-]+:|\Z)", yaml, flags=re.M | re.S))
        for job in ("sdk-decoupling-d0", "fhir-v3-migration", "fhir-v4-generator", "fhir-v5-integration",
                    "fhir-v6-client", "fhir-v7-delivery", "fhir-v7-compare"):
            self.assertIn("ref: " + d1.D0_REVISION, jobs[job])
            self.assertIn("Historical", jobs[job])
        self.assertNotIn("ref: " + d1.D0_REVISION, jobs["sdk-decoupling-d1"])
        self.assertIn("eng/Test-SdkDecouplingD1.py", jobs["sdk-decoupling-d1"])

    def test_k0_build_host_is_isolated_from_runner_runtime_updates(self):
        yaml = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        jobs = dict(re.findall(r"^  ([\w-]+):\n(.*?)(?=^  [\w-]+:|\Z)", yaml, flags=re.M | re.S))
        k0 = jobs["kernel-migration-baseline"]
        job_env, steps = k0.split("    steps:\n", 1)
        self.assertNotIn("runner.", job_env)
        prepare, setup = steps.split("      - name: Setup isolated K0 SDK and build runtime", 1)
        self.assertIn("Join-Path $env:RUNNER_TEMP 'kernel-k0-dotnet'", prepare)
        self.assertIn('"DOTNET_INSTALL_DIR=$installDirectory" >> $env:GITHUB_ENV', prepare)
        self.assertIn('DOTNET_MULTILEVEL_LOOKUP: "0"', k0)
        self.assertIn("dotnet-version: '9.0.318'", k0)
        self.assertIn("global-json-file: global.json", k0)
        self.assertEqual("9.0.317", json.loads((ROOT / "global.json").read_text())["sdk"]["version"])
        probe = ET.parse(ROOT / "eng/KernelBuildRuntime.proj")
        self.assertEqual("$([System.Environment]::Version)", probe.findtext("PropertyGroup/KernelBuildRuntimeVersion"))


if __name__ == "__main__":
    unittest.main()
