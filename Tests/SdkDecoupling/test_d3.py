"""Reject unreviewed breaking changes and incomplete neutral SDK evidence."""
import importlib.util
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

import test_d2

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("d3", ROOT / "eng/Test-SdkDecouplingD3.py")
d3 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d3)


class D3EvidenceTests(unittest.TestCase):
    def test_only_documented_api_removals_can_be_recorded(self):
        sdk, ig, aggregate = d3.expected_apis()
        self.assertIn("MyFhirSdk.Client.FhirClient`2", sdk)
        self.assertIn("MyFhirSdk.Client.Exceptions.FhirHttpException", sdk)
        with tempfile.TemporaryDirectory() as tmp, patch.object(d3, "BASELINE", Path(tmp)):
            delta = d3.validate_api(sdk, ig, aggregate, record=True)
            self.assertEqual([], delta["fullSdk"]["added"])
            self.assertEqual({"added": [], "removed": []}, delta["independentIg"])
            self.assertEqual(5, len(delta["removedTypes"]))
            d3.validate_api(sdk, ig, aggregate)
            for actual in ((sdk + "TYPE unexpected\n", ig, aggregate), (sdk.replace("FhirClient`2", "ChangedClient`2"), ig, aggregate),
                           (sdk, "", aggregate), (sdk, ig, aggregate + "TYPE unexpected\n")):
                with self.assertRaisesRegex(ValueError, "exceeds the reviewed"):
                    d3.validate_api(*actual, record=True)
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                d3.validate_api(sdk, ig, aggregate, record=True)
            (Path(tmp) / "full-sdk-api.txt").write_text("tampered", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Unapproved D3 API drift"):
                d3.validate_api(sdk, ig, aggregate)

    def test_all_migrated_cases_and_neutrality_tests_are_mandatory(self):
        rows = [(name, assembly, "Passed") for name, assembly in d3.required_tests().items()]
        self.assertEqual(67, len(rows))
        # Reuse the TRX fixture builder; replace only its gate under test.
        import test_d2
        with patch.object(test_d2, "d2", d3):
            fixture = test_d2.D2EvidenceTests()
            self.assertEqual([], fixture.evidence(rows)["errors"])
            for index in range(len(rows)):
                self.assertTrue(fixture.evidence(rows[:index] + rows[index + 1:])["errors"])
            for outcome in ("Failed", "NotExecuted"):
                self.assertTrue(fixture.evidence(rows[:-1] + [(rows[-1][0], rows[-1][1], outcome)])["errors"])
            self.assertTrue(fixture.evidence(rows + rows[:1])["errors"])

    def test_sdk_graph_requires_only_three_neutral_projects(self):
        sdk, ig = test_d2.D2EvidenceTests().graphs()
        sdk["ProjectReference"] = sdk["ProjectReference"][:3]
        d3.validate_project_graph(sdk, ig)
        for project in ("Models/R4/MyFhirSdk.R4.Models.csproj", "Models/R5/MyFhirSdk.R5.Models.csproj",
                        "ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj"):
            with self.assertRaises(ValueError):
                d3.validate_project_graph(sdk | {"ProjectReference": sdk["ProjectReference"] + [{"FullPath": str(ROOT / project)}]}, ig)
        for kind in ("Reference", "PackageReference"):
            with self.assertRaises(ValueError):
                d3.validate_project_graph(sdk | {kind: [{"Identity": "Hidden.Models"}]}, ig)

    def test_ci_preserves_d2_and_runs_d3_on_current_source(self):
        yaml = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        jobs = dict(re.findall(r"^  ([\w-]+):\n(.*?)(?=^  [\w-]+:|\Z)", yaml, flags=re.M | re.S))
        self.assertIn("Historical D2", jobs["sdk-decoupling-d2"])
        self.assertIn("ref: " + d3.D2_REVISION, jobs["sdk-decoupling-d2"])
        self.assertIn("ref: d47374bf99e91a61848fccf7612e2d571ae4bf67", jobs["sdk-decoupling-d3"])
        self.assertNotIn("ref:", jobs["sdk-decoupling-d4"])
        self.assertIn("Test-SdkDecouplingD3.py", jobs["sdk-decoupling-d3"])


if __name__ == "__main__":
    unittest.main()
