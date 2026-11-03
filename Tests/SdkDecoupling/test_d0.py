"""Failure-mode checks for the D0 evidence boundary."""
import importlib.util
import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("d0", ROOT / "eng/Test-SdkDecoupling.py")
d0 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d0)


class EvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        (ROOT / "artifacts").mkdir(exist_ok=True)

    def test_ci_cannot_use_previous_revision_or_failed_job(self):
        good = {"head_sha": d0.PIN, "status": "completed", "conclusion": "success",
                "jobs": [{"conclusion": "success"}]}
        d0.validate_ci(good)
        for change in ({"head_sha": "c7680b7"}, {"status": "in_progress"},
                       {"conclusion": "failure"}, {"jobs": []},
                       {"jobs": [{"conclusion": "skipped"}]}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                d0.validate_ci(good | change)

    def test_frozen_and_current_consumers_are_distinct(self):
        self.assertEqual("frozen-consumer", d0.category("Tests/KernelMigration/BaselineConsumer/v1/Program.cs"))
        self.assertEqual("current-test-or-fixture", d0.category("Tests/KernelMigration/Consumer/Program.cs"))
        self.assertEqual("historical-evidence", d0.category("docs/fhir_versions/baselines/v3/README.md"))

    def test_generic_client_and_exception_are_not_legacy_client(self):
        rows = d0.references({"Client/Test.cs": b"FhirClient<T, U> c; FhirClientException e; FhirClientOptions o;\nnew FhirClient(http);"})
        self.assertEqual([2], [r["line"] for r in rows if r["kind"] == "legacy-client"])

    def test_external_inventory_does_not_copy_secrets_or_outputs(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "Program.cs").write_text('new FhirJsonParser(); // https://secret.example/token', encoding="utf-8")
            (root / "appsettings.json").write_text('secret')
            (root / "bin").mkdir()
            (root / "bin/hidden.cs").write_text('secret')
            result = d0.external_inventory(root)
            self.assertEqual(["Program.cs"], [r["path"] for r in result["files"]])
            self.assertNotIn("secret", str(result))
            self.assertEqual("default-engine", result["files"][0]["references"][0]["kind"])

    def test_inventory_detects_same_size_content_change(self):
        self.assertNotEqual(d0.inventory({"a.cs": b"old"}), d0.inventory({"a.cs": b"new"}))

    def test_generated_addition_removal_and_mutation_fail_protection(self):
        frozen = {"generated": d0.inventory({"Generated/model.cs": b"original"}), "contracts": []}
        for current in ({}, {"Generated/model.cs": b"changed"},
                        {"Generated/model.cs": b"original", "Generated/extra.cs": b"new"}):
            with self.subTest(current=current):
                self.assertTrue(d0.protected_drift(frozen, current, {}))
        self.assertEqual([], d0.protected_drift(frozen, {"Generated/model.cs": b"original"}, {}))

    def test_contract_checkout_eol_is_accepted_but_content_changes_fail(self):
        pinned = {"global.json": b'{\n"version": "9.0.317"\n}\n'}
        frozen = {"generated": [], "contracts": d0.inventory(pinned)}
        current = {p: b.replace(b"\n", b"\r\n") for p, b in pinned.items()}
        self.assertEqual([], d0.protected_drift(frozen, current, pinned))
        current["global.json"] = current["global.json"].replace(b"9.0.317", b"9.0.318")
        self.assertEqual(["global.json"], d0.protected_drift(frozen, current, pinned))

    def test_generated_bytes_are_still_exact(self):
        pinned = {"Generated/model.cs": b"class Model {}\n"}
        frozen = {"generated": d0.inventory(pinned), "contracts": []}
        self.assertEqual(["Generated/model.cs"], d0.protected_drift(
            frozen, {"Generated/model.cs": b"class Model {}\r\n"}, pinned))

    def test_existing_output_is_rejected_before_validation(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as tmp:
            output = Path(tmp) / "existing"
            d0.write(output / "summary.json", {"status": "Passed", "sourceRevision": "previous"})
            with patch.object(sys, "argv", ["d0", "--output", str(output)]), \
                    patch.object(d0, "pinned_sources") as pin, contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as error:
                    d0.main()
                self.assertEqual(2, error.exception.code)
                pin.assert_not_called()

    def test_failed_pin_writes_failed_summary_in_fresh_output(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as tmp:
            output = Path(tmp) / "fresh"
            with patch.object(sys, "argv", ["d0", "--output", str(output)]), \
                    patch.object(d0, "pinned_sources", side_effect=ValueError("invalid pin")), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, d0.main())
            summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
            self.assertEqual("Failed", summary["status"])
            self.assertEqual("invalid pin", summary["error"])

    def test_msbuild_checks_effective_ig_sources_and_references(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as tmp:
            root = Path(tmp)
            source = root / "ImplementationGuides/TwCore/Package.cs"
            source.parent.mkdir(parents=True)
            source.write_text("public class Package {}", encoding="utf-8")
            cases = [
                ('<!-- ImplementationGuides -->', True, False),
                ('<PropertyGroup><Marker>ImplementationGuides</Marker></PropertyGroup>', True, False),
                ('<ItemGroup Condition="\'false\' == \'true\'"><Compile Remove="ImplementationGuides/**/*.cs" /></ItemGroup>', True, False),
                ('<ItemGroup><Compile Remove="ImplementationGuides/**/*.cs" /></ItemGroup>', False, False),
                ('<ItemGroup><Compile Remove="ImplementationGuides/**/*.cs" /><Compile Include="ImplementationGuides/TwCore/Package.cs" /></ItemGroup>', True, False),
                ('<ItemGroup><ProjectReference Include="Models/R5/Models.csproj" /></ItemGroup>', True, True),
            ]
            for body, expected_ig, expected_models in cases:
                with self.subTest(body=body):
                    (root / "MyFhirSdk.csproj").write_text(
                        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>'
                        + body + '</Project>', encoding="utf-8")
                    sources = {p: b"" for p in ("Serialization/Json/FhirJsonParser.cs",
                        "Serialization/Json/FhirJsonSerializer.cs", "Validation/FhirValidator.cs",
                        "Client/Requests/FhirResourceTypeResolver.cs")}
                    gaps = d0.target_gaps(sources, d0.evaluate_sdk(root))
                    self.assertEqual(expected_ig, "sdk-compiles-concrete-ig" in gaps)
                    self.assertEqual(expected_models, "sdk-references-version-models" in gaps)

    def test_full_api_rejects_snapshot_and_provenance_drift(self):
        captured = {"full-sdk-api.txt": b"full API\n", "twcore-api.txt": b"TW Core API\n"}
        with tempfile.TemporaryDirectory() as tmp, patch.object(d0, "BASELINE", Path(tmp)):
            d0.verify_full_api(captured, {}, record=True)
            d0.verify_full_api(captured, {})
            (Path(tmp) / "twcore-api.txt").write_bytes(b"wrong\n")
            with self.assertRaisesRegex(ValueError, "snapshot drift"):
                d0.verify_full_api(captured, {})
            with self.assertRaisesRegex(ValueError, "refusing to overwrite"):
                d0.verify_full_api(captured, {}, record=True)
            metadata = Path(tmp) / "full-api.json"
            value = json.loads(metadata.read_text(encoding="utf-8"))
            value["revision"] = "wrong"
            d0.write(metadata, value)
            with self.assertRaisesRegex(ValueError, "metadata differs"):
                d0.verify_full_api(captured, {})

    def test_recorded_api_covers_twcore_types_members_and_defining_assembly(self):
        sdk = (d0.BASELINE / "full-sdk-api.txt").read_text(encoding="utf-8")
        twcore = (d0.BASELINE / "twcore-api.txt").read_text(encoding="utf-8")
        for name in ("TwCorePackage", "TwCoreProfiles"):
            self.assertIn("TYPE [MyFhirSdk]MyFhirSdk.ImplementationGuides.TwCore." + name, twcore)
        self.assertIn(" GetRules(", twcore)
        self.assertIn(" Patient nullability=", twcore)
        self.assertIn("TYPE [MyFhirSdk]MyFhirSdk.Serialization.Json.FhirJsonParser", sdk)
        self.assertTrue(set(twcore.splitlines()).issubset(sdk.splitlines()))

    def test_baseline_current_target_is_not_decoupled(self):
        import json
        value = json.loads((d0.BASELINE / "inventory.json").read_text(encoding="utf-8"))
        self.assertEqual(d0.PIN, value["revision"])
        self.assertTrue(any(r["kind"] == "registry-default" for r in value["references"]))
        self.assertEqual(4, sum(r["path"].endswith("generation-manifest.json") for r in value["generated"]))


if __name__ == "__main__":
    unittest.main()
