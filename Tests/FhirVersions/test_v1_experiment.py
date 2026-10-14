"""Integrity checks for the bounded V1 prototype; no production CodeGen policy."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("v1", ROOT / "eng/Generate-FhirR4V1Experiment.py")
prototype = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prototype)
runner_spec = importlib.util.spec_from_file_location("v1_runner", ROOT / "eng/Test-FhirV1Experiment.py")
runner = importlib.util.module_from_spec(runner_spec)
runner_spec.loader.exec_module(runner)


class V1PrototypeIntegrityTests(unittest.TestCase):
    def test_tested_models_cannot_use_debug_or_stale_bytes_in_release_evidence(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as directory:
            release = Path(directory) / "release.dll"
            tested = Path(directory) / "tested.dll"
            release.write_bytes(b"release implementation")
            tested.write_bytes(b"debug or stale implementation")
            with self.assertRaisesRegex(ValueError, "did not use the measured Release"):
                runner.assert_tested_models_match_release(release, tested)
            tested.write_bytes(release.read_bytes())
            self.assertEqual(prototype.digest(release.read_bytes()), runner.assert_tested_models_match_release(release, tested))

    def test_only_known_external_client_smoke_may_skip_without_endpoint(self):
        allowed = [{"assembly": "MyFhirSdk.Client.Tests", "skips": [
            {"test": runner.EXTERNAL_SMOKE, "reason": runner.EXTERNAL_SKIP_REASON}]}]
        runner.assert_allowed_regression_skips(allowed, "")
        runner.assert_allowed_regression_skips([], "https://example.invalid/fhir")
        with self.assertRaisesRegex(ValueError, "Unexpected skipped test"):
            runner.assert_allowed_regression_skips(allowed, "https://example.invalid/fhir")

    def test_r5_v1_wrong_reason_and_duplicate_skips_are_rejected(self):
        for assembly, test, reason in (
                ("MyFhirSdk.Validation.Tests", "RequiredFieldRuleTests", "disabled"),
                ("MyFhirSdk.Architecture.Tests", "MyFhirSdk.Tests.Architecture.V1.Probe", "disabled"),
                ("MyFhirSdk.Client.Tests", "AnotherClientTest", runner.EXTERNAL_SKIP_REASON),
                ("MyFhirSdk.Client.Tests", runner.EXTERNAL_SMOKE, "incorrect reason")):
            with self.subTest(assembly=assembly, test=test):
                with self.assertRaisesRegex(ValueError, "Unexpected skipped test"):
                    runner.assert_allowed_regression_skips([{"assembly": assembly, "skips": [
                        {"test": test, "reason": reason}]}], "")
        duplicate = {"assembly": "MyFhirSdk.Client.Tests", "skips": [
            {"test": runner.EXTERNAL_SMOKE, "reason": runner.EXTERNAL_SKIP_REASON}] * 2}
        with self.assertRaisesRegex(ValueError, "Duplicate external"):
            runner.assert_allowed_regression_skips([duplicate], "")

    def test_locked_closure_is_complete_and_committed_generation_is_deterministic(self):
        first = prototype.generate()
        self.assertEqual(first, prototype.generate())
        for name, text in first.items():
            self.assertEqual(text.replace("\r\n", "\n").encode(), (ROOT / name).read_bytes(), name)
        manifest = json.loads(first[prototype.MANIFEST])
        definitions = {d["id"] for d in manifest["definitions"]}
        self.assertTrue({"Patient", "Observation", "Attachment", "Element", "Resource", "Meta",
                         "Extension", "Narrative", "SimpleQuantity"}.issubset(definitions))
        self.assertTrue(all(e["from"] in definitions and e["to"] in definitions for e in manifest["dependencyEdges"]))
        self.assertNotIn("integer64", definitions)
        self.assertEqual(["Patient", "Observation", "Attachment"], manifest["seeds"])
        self.assertFalse(manifest["productionR4Support"])
        self.assertEqual(len(first) - 1, len(manifest["sources"]))
        for source in manifest["sources"]:
            self.assertEqual(source["sha256"], prototype.digest((ROOT / source["path"]).read_bytes()))


if __name__ == "__main__":
    unittest.main()
