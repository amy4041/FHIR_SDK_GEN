"""Evidence integrity checks; run: python -m unittest discover -s Tests/FhirVersions."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("baseline", ROOT / "eng/Export-FhirVersionBaseline.py")
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)


class BaselineIntegrityTests(unittest.TestCase):
    def test_r4_root_definitions_are_runtime_candidates_without_specialization(self):
        lock = json.loads((ROOT / "CodeGen/Policy/r4-package-lock.json").read_text())
        inventory = baseline.package_inventory(lock)
        definitions = {d["id"]: d for d in inventory["definitions"]}
        for name in ("Element", "Resource"):
            with self.subTest(name=name):
                self.assertIsNone(definitions[name]["derivation"])
                self.assertEqual("runtime-bootstrap-candidate-requires-V1", definitions[name]["disposition"])
                self.assertIn("Runtime compile owner", definitions[name]["dispositionReason"])
        self.assertEqual(7, inventory["byDisposition"]["runtime-bootstrap-candidate-requires-V1"])
        self.assertEqual("model-generation-candidate-requires-V4-policy", definitions["Patient"]["disposition"])

    def test_r5_bootstrap_matches_existing_ownership_policy(self):
        lock = json.loads((ROOT / "CodeGen/Policy/r5-package-lock.json").read_text())
        inventory = baseline.package_inventory(lock)
        policy = json.loads((ROOT / "CodeGen/Policy/r5-model-ownership-policy.json").read_text())
        expected = {d["fhirType"] for d in policy["externalDefinitionNodes"]}
        actual = {d["id"] for d in inventory["definitions"] if d["disposition"] == "runtime-bootstrap-candidate-requires-V1"}
        self.assertEqual(expected, actual)
        self.assertTrue({"Base", "DataType", "BackboneType"}.issubset(actual))

    def test_consumer_inventory_includes_conditional_runtime_and_actual_dll_paths(self):
        for project in ("Tests/KernelMigration/Consumer/Consumer.csproj",
                        "Tests/KernelMigration/RebuiltLibrary/RebuiltLibrary.csproj",
                        "Tests/KernelMigration/RebuiltConsumer/RebuiltConsumer.csproj"):
            with self.subTest(project=project):
                inventory = baseline.project_inventory(project)
                references = {r["identity"]: r["hintPath"] for r in inventory["dllReferences"]}
                self.assertEqual({"MyFhirSdk": "bin/Release/net9.0/MyFhirSdk.dll",
                                  "MyFhirSdk.R5.Models": "Models/R5/bin/Release/net9.0/MyFhirSdk.R5.Models.dll",
                                  "MyFhirSdk.ModelContracts": "bin/Release/net9.0/MyFhirSdk.ModelContracts.dll",
                                  "MyFhirSdk.ModelSupport": "bin/Release/net9.0/MyFhirSdk.ModelSupport.dll",
                                  "MyFhirSdk.Runtime": "bin/Release/net9.0/MyFhirSdk.Runtime.dll"}, references)
                self.assertEqual(sorted(references.values()), inventory["items"]["Reference"])
                self.assertEqual("Release", inventory["evaluation"]["Configuration"])

    def test_unresolved_implementation_hintpath_is_rejected(self):
        result = {"Properties": {}, "Items": {"Compile": [], "ProjectReference": [],
                                              "Reference": [{"Identity": "MyFhirSdk"}]}}
        with patch.object(baseline, "run", return_value=json.dumps(result)):
            with self.assertRaisesRegex(ValueError, "Unresolved implementation HintPath"):
                baseline.project_inventory("Tests/KernelMigration/Consumer/Consumer.csproj")

    def test_committed_r4_input_matches_exact_lock_and_has_real_version_difference(self):
        lock = json.loads((ROOT / "CodeGen/Policy/r4-package-lock.json").read_text())
        inventory = baseline.package_inventory(lock)
        self.assertEqual(["unsignedInt"], next(d for d in inventory["definitions"] if d["id"] == "Attachment")["attachmentSizeTypes"])
        self.assertEqual("version-specific-auxiliary-required", next(d for d in inventory["definitions"] if d["id"] == "SimpleQuantity")["disposition"])
        self.assertEqual(inventory["structureDefinitions"], sum(inventory["byDisposition"].values()))

    def test_modified_archive_hash_is_rejected(self):
        lock = json.loads((ROOT / "CodeGen/Policy/r4-package-lock.json").read_text())
        lock["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            baseline.package_inventory(lock)

    def test_wrong_package_identity_is_rejected_even_with_valid_archive_hash(self):
        lock = json.loads((ROOT / "CodeGen/Policy/r4-package-lock.json").read_text())
        lock["packageVersion"] = "4.3.0"
        with self.assertRaisesRegex(ValueError, "metadata"):
            baseline.package_inventory(lock)

    def test_missing_test_assemblies_cannot_pass(self):
        (ROOT / "artifacts").mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as directory:
            with self.assertRaisesRegex(ValueError, "exactly the expected"):
                baseline.regression_inventory(Path(directory))

    def test_trx_skips_are_counted_from_results_and_counter_mismatch_is_rejected(self):
        trx = '''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions><UnitTest><TestMethod codeBase="Example.Tests.dll"/></UnitTest></TestDefinitions>
          <Results><UnitTestResult testName="works" outcome="Passed"/>
          <UnitTestResult testName="external" outcome="NotExecuted"><Output><StdOut>Endpoint not configured</StdOut></Output></UnitTestResult></Results>
          <ResultSummary><Counters total="2" passed="1" failed="0" notExecuted="0"/></ResultSummary></TestRun>'''
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as directory:
            path = Path(directory) / "example.trx"
            path.write_text(trx)
            with patch.object(baseline, "EXPECTED_TESTS", {"Example.Tests"}):
                result = baseline.regression_inventory(Path(directory))[0]
                self.assertEqual(1, result["skipped"])
                self.assertIn("Endpoint not configured", result["skips"][0]["reason"])
                path.write_text(trx.replace('total="2"', 'total="3"'))
                with self.assertRaisesRegex(ValueError, "counter mismatch"):
                    baseline.regression_inventory(Path(directory))


if __name__ == "__main__":
    unittest.main()
