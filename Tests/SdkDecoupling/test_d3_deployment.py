"""Deployment checks must reject unused Models and fallback outside publish."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("deployment", ROOT / "eng/Test-SdkDecouplingDeployment.py")
deployment = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deployment)


class D3DeploymentEvidenceTests(unittest.TestCase):
    def fixture(self, root):
        expected = deployment.NEUTRAL | {"MyFhirSdk.R4.Models"}
        for name in expected:
            (root / (name + ".dll")).touch()
        deps = {"targets": {"net9.0": {name: {"runtime": {name + ".dll": {}}} for name in expected}}}
        (root / "Probe.deps.json").write_text(json.dumps(deps), encoding="utf-8")
        report = {"status": "Passed", "releases": [{"fhirVersion": "4.0.1", "roundTrip": True,
                  "validation": True, "typedClient": True}], "assemblies": [
            {"name": name, "path": str(root / (name + ".dll")),
             "references": sorted(deployment.NEUTRAL - {"MyFhirSdk"}) if name == "MyFhirSdk" else []}
            for name in sorted(expected)]}
        return expected, report, deps

    def test_unneeded_model_dll_or_manifest_dependency_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            expected, _, deps = self.fixture(root)
            deployment.validate_inventory("r4", expected, root)
            unexpected = root / "MyFhirSdk.R5.Models.dll"
            unexpected.touch()
            with self.assertRaisesRegex(ValueError, "inventory"):
                deployment.validate_inventory("r4", expected, root)
            unexpected.unlink()
            deps["targets"]["net9.0"]["unused"] = {"runtime": {unexpected.name: {}}}
            (root / "Probe.deps.json").write_text(json.dumps(deps), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "manifest"):
                deployment.validate_inventory("r4", expected, root)

    def test_loading_from_repository_or_sdk_referencing_models_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            expected, report, _ = self.fixture(root)
            deployment.validate_execution("r4", expected, report, root)
            sdk = next(a for a in report["assemblies"] if a["name"] == "MyFhirSdk")
            sdk["path"] = str(ROOT / "bin/Release/net9.0/MyFhirSdk.dll")
            with self.assertRaisesRegex(ValueError, "outside deployment"):
                deployment.validate_execution("r4", expected, report, root)
            sdk["path"] = str(root / "MyFhirSdk.dll")
            sdk["references"].append("MyFhirSdk.R5.Models")
            with self.assertRaisesRegex(ValueError, "concrete models"):
                deployment.validate_execution("r4", expected, report, root)

    def test_missing_release_behavior_or_ig_replacement_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            expected, report, _ = self.fixture(root)
            report["releases"][0]["typedClient"] = False
            with self.assertRaisesRegex(ValueError, "release behavior"):
                deployment.validate_execution("r4", expected, report, root)
            report["releases"][0]["typedClient"] = True
            expected.add("MyFhirSdk.ImplementationGuides.TwCore")
            report["assemblies"].append({"name": "MyFhirSdk.ImplementationGuides.TwCore",
                                        "path": str(root / "MyFhirSdk.ImplementationGuides.TwCore.dll"), "references": []})
            report["ig"] = {"positive": True, "negative": True, "replacement": False}
            with self.assertRaisesRegex(ValueError, "IG behavior"):
                deployment.validate_execution("r4-twcore", expected, report, root)


if __name__ == "__main__":
    unittest.main()
