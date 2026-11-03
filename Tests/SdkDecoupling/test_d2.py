"""D2 gate rejects missing coverage, unreviewed API and IG ownership drift."""
import importlib.util
from pathlib import Path
import re
import json
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("d2", ROOT / "eng/Test-SdkDecouplingD2.py")
d2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d2)


class D2EvidenceTests(unittest.TestCase):
    def evidence(self, rows):
        with tempfile.TemporaryDirectory() as tmp:
            root = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
            definitions = ET.SubElement(root, "TestDefinitions")
            results = ET.SubElement(root, "Results")
            for index, (name, assembly, outcome) in enumerate(rows):
                test_id = str(index)
                definition = ET.SubElement(definitions, "UnitTest", id=test_id)
                ET.SubElement(definition, "TestMethod", codeBase="/tests/" + assembly + ".dll")
                ET.SubElement(results, "UnitTestResult", testId=test_id, testName=name, outcome=outcome)
            path = Path(tmp) / "tests.trx"
            ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
            return d2.required_test_evidence([path])

    def test_every_framework_and_twcore_case_is_required(self):
        rows = [(name, assembly, "Passed") for name, assembly in d2.required_tests().items()]
        self.assertEqual(65, len(rows))
        self.assertEqual([], self.evidence(rows)["errors"])
        for index, (name, _, _) in enumerate(rows):
            with self.subTest(case=name):
                evidence = self.evidence(rows[:index] + rows[index + 1:])
                self.assertEqual(64, evidence["passed"])
                self.assertTrue(evidence["errors"])
        self.assertEqual(0, self.evidence([])["passed"])

    def test_duplicate_skip_failure_or_wrong_assembly_is_rejected(self):
        rows = [(name, assembly, "Passed") for name, assembly in d2.required_tests().items()]
        name, assembly, _ = rows[-1]
        for changed in (rows + [rows[-1]], rows[:-1] + [(name, assembly, "NotExecuted")],
                        rows[:-1] + [(name, assembly, "Failed")], rows[:-1] + [(name, "Other.Tests", "Passed")]):
            self.assertTrue(self.evidence(changed)["errors"])

    def test_only_two_type_assembly_move_can_be_recorded(self):
        sdk, ig, aggregate = d2.expected_apis()
        with tempfile.TemporaryDirectory() as tmp, patch.object(d2, "BASELINE", Path(tmp)):
            delta = d2.validate_api(sdk, ig, aggregate, record=True)
            self.assertEqual([], delta["fullSdk"]["added"])
            self.assertEqual(2, len(delta["movedTypes"]))
            d2.validate_api(sdk, ig, aggregate)
            for actual in ((sdk + "TYPE unexpected\n", ig, aggregate), (sdk, "", aggregate),
                           (sdk, ig + "TYPE unexpected\n", aggregate), (sdk, ig, aggregate + "TYPE unexpected\n")):
                with self.assertRaisesRegex(ValueError, "exceeds the reviewed"):
                    d2.validate_api(*actual, record=True)
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                d2.validate_api(sdk, ig, aggregate, record=True)
            (Path(tmp) / "twcore-api.txt").write_text("tampered", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Unapproved D2 API drift"):
                d2.validate_api(sdk, ig, aggregate)

    def graphs(self):
        def refs(*paths):
            return [{"FullPath": str(ROOT / path)} for path in paths]
        sdk = {"Compile": [{"FullPath": str(ROOT / "Validation/FhirValidator.cs")}],
               "ProjectReference": refs("Runtime/MyFhirSdk.Runtime.csproj", "ModelContracts/MyFhirSdk.ModelContracts.csproj",
                                        "ModelSupport/MyFhirSdk.ModelSupport.csproj", "Models/R5/MyFhirSdk.R5.Models.csproj")}
        ig = {"ProjectReference": refs("MyFhirSdk.csproj", "Models/R4/MyFhirSdk.R4.Models.csproj")}
        return sdk, ig

    def test_evaluated_graph_rejects_sdk_ig_sources_and_extra_dependencies(self):
        sdk, ig = self.graphs()
        d2.validate_project_graph(sdk, ig)
        bad_compile = {"FullPath": str(ROOT / "ImplementationGuides/TwCore/TwCorePackage.cs")}
        bad_ref = {"FullPath": str(ROOT / "ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj")}
        for bad_sdk, bad_ig in (
            (sdk | {"Compile": sdk["Compile"] + [bad_compile]}, ig),
            (sdk | {"Compile": []}, ig),
            (sdk | {"ProjectReference": sdk["ProjectReference"] + [bad_ref]}, ig),
            (sdk, ig | {"ProjectReference": ig["ProjectReference"][:1]}),
            (sdk, ig | {"ProjectReference": ig["ProjectReference"] + [{"FullPath": str(ROOT / "Models/R5/MyFhirSdk.R5.Models.csproj")}]}),
            (sdk, ig | {"Reference": [{"Identity": "MyFhirSdk.R5.Models"}]}),
            (sdk, ig | {"PackageReference": [{"Identity": "Unapproved.Ig"}]})):
            with self.assertRaises(ValueError):
                d2.validate_project_graph(bad_sdk, bad_ig)

    def test_sdk_dll_and_package_dependencies_are_rejected(self):
        sdk, ig = self.graphs()
        for kind in ("Reference", "PackageReference"):
            for name in ("MyFhirSdk.ImplementationGuides.TwCore", "Unapproved.Dependency"):
                with self.subTest(kind=kind, name=name):
                    with self.assertRaisesRegex(ValueError, "SDK project dependencies changed"):
                        d2.validate_project_graph(sdk | {kind: [{"Identity": name}]}, ig)

    def test_real_msbuild_injected_sdk_dependencies_are_rejected(self):
        _, ig = self.graphs()
        with tempfile.TemporaryDirectory() as tmp:
            for kind in ("Reference", "PackageReference"):
                with self.subTest(kind=kind):
                    project = ET.Element("Project")
                    group = ET.SubElement(project, "ItemGroup", Condition="'$(MSBuildProjectName)' == 'MyFhirSdk'")
                    item = ET.SubElement(group, kind, Include="MyFhirSdk.ImplementationGuides.TwCore")
                    if kind == "PackageReference":
                        item.set("Version", "1.0.0")
                    else:
                        ET.SubElement(item, "HintPath").text = str(ROOT / "ImplementationGuides/TwCore/bin/Release/net9.0/MyFhirSdk.ImplementationGuides.TwCore.dll")
                    path = Path(tmp) / "Injected.targets"
                    ET.ElementTree(project).write(path, encoding="utf-8", xml_declaration=True)
                    result = subprocess.run(["dotnet", "msbuild", "MyFhirSdk.csproj", "-nologo", "-p:Configuration=Release",
                        "-p:CustomBeforeMicrosoftCommonTargets=" + str(path),
                        "-getItem:Compile,ProjectReference,Reference,PackageReference"], cwd=ROOT,
                        capture_output=True, encoding="utf-8", check=True)
                    sdk = json.loads(result.stdout)["Items"]
                    self.assertTrue(any(r["Identity"] == "MyFhirSdk.ImplementationGuides.TwCore" for r in sdk[kind]))
                    with self.assertRaisesRegex(ValueError, "SDK project dependencies changed"):
                        d2.validate_project_graph(sdk, ig)

    def test_ci_preserves_historical_d1_and_gates_current_d2(self):
        yaml = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        jobs = dict(re.findall(r"^  ([\w-]+):\n(.*?)(?=^  [\w-]+:|\Z)", yaml, flags=re.M | re.S))
        self.assertIn("Historical D1", jobs["sdk-decoupling-d1"])
        self.assertIn("ref: " + d2.D1_REVISION, jobs["sdk-decoupling-d1"])
        self.assertIn("ref: c2e9098225718c79daf1aed4289d4f806ebab570", jobs["sdk-decoupling-d2"])
        self.assertIn("ref: d47374bf99e91a61848fccf7612e2d571ae4bf67", jobs["sdk-decoupling-d3"])
        self.assertNotIn("ref:", jobs["sdk-decoupling-d4"])
        self.assertIn("eng/Test-SdkDecouplingD2.py", jobs["sdk-decoupling-d2"])


if __name__ == "__main__":
    unittest.main()
