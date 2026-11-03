#!/usr/bin/env python3
"""Accept D4 only from complete matching Windows and Linux delivery artifacts."""
import argparse
import copy
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "eng"))
import FhirDelivery as delivery
spec = importlib.util.spec_from_file_location("d4_compare_gate", ROOT / "eng/Test-SdkDecouplingD4.py")
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def validate_recorded_execution(name, report, manifest, deployed):
    for assembly in report["assemblies"]:
        dll = assembly["name"] + ".dll"
        if assembly.get("path") != "<deployment>/" + dll:
            raise ValueError("Recorded assembly path is not the accepted deployment marker: " + dll)
        if assembly.get("identity") != manifest["assemblyIdentities"].get(dll):
            raise ValueError("Recorded assembly identity differs from delivery: " + dll)
    # Rebase only verified markers to the downloaded artifact; retain the original evidence.
    rebased = copy.deepcopy(report)
    for assembly in rebased["assemblies"]:
        assembly["path"] = str(deployed / (assembly["name"] + ".dll"))
    delivery.validate_execution(name, rebased, deployed)


def validate_leg(root):
    # CLI artifact arguments may be relative; the shared TRX inventory uses absolute ROOT.
    root = root.resolve()
    files = delivery.inventory(root)
    files.pop("evidence-files.json", None)
    if files != load(root / "evidence-files.json"):
        raise ValueError("D4 evidence inventory differs")
    summary = load(root / "summary.json")
    if summary.get("gate") != "D4-platform" or summary.get("status") != "Passed" or summary.get("os") not in ("Windows", "Linux"):
        raise ValueError("Expected a passed D4 platform artifact")
    neutral = load(root / "neutral-sdk/summary.json")
    if (neutral.get("status") != "Passed" or neutral.get("fullyDecoupled") is not True or
            neutral["sourceFingerprint"] != summary["sourceFingerprint"] or neutral["testCounts"] != summary["testCounts"]):
        raise ValueError("Neutral SDK source or regression evidence mismatch")
    reports = gate.d3.regression.regression_inventory(root / "neutral-sdk/k3/test-results")
    gate.d3.v1.assert_allowed_regression_skips(reports, "")
    counts = {k: sum(r[k] for r in reports) for k in ("total", "passed", "failed", "skipped")}
    if counts != summary["testCounts"] or counts["failed"]:
        raise ValueError("Current TRX counts differ")
    for folder in ("neutral-sdk/k3/test-results", "rollback-test-results"):
        if gate.d3.required_test_evidence((root / folder).glob("*.trx"))["errors"]:
            raise ValueError("Required D3 cases missing")
    current = gate.cases((root / "neutral-sdk/k3/test-results").glob("*.trx"))
    if gate.assert_fresh_cases(current, gate.cases((root / "fresh-test-results").glob("*.trx"))) != summary["freshIntegrationTests"]:
        raise ValueError("Fresh integration counts differ")
    if (summary["freshIntegration"] != {"outsideCheckout": True, "freshCache": True, "generatedByInstalledTool": True,
            "reusedBinObj": False, "generatedInputsUnchanged": True}):
        raise ValueError("Fresh generated integration isolation failed")
    manifest = load(root / "bundle/delivery-manifest.json")
    delivery.validate_manifest(root / "bundle", manifest)
    if (manifest["sourceRevision"] != summary["sourceRevision"] or manifest["sourceFingerprint"] != summary["deliverySourceFingerprint"]
            or delivery.sha(root / "bundle/delivery-manifest.json") != summary["deliveryManifestSha256"]):
        raise ValueError("Delivery source identity mismatch")
    if delivery.sha(root / "canonical-tool.nupkg") != summary["toolPackageSha256"] or manifest["toolPackageSha256"] != summary["toolPackageSha256"]:
        raise ValueError("Canonical package differs")
    external = load(root / "external/summary.json")
    if (external.get("status") != "Passed" or external.get("outsideRepository") is not True or external.get("isolatedCache") is not True
            or external.get("inheritedEndpointsDisabled") is not True or external.get("externalServer") != "NotRun"
            or set(external["examples"]) != set(delivery.COMPOSITIONS)
            or external["canonicalInstall"] != {"emptyInitialCache": True, "localFeedOnly": True,
                "compilerReferenceSha256": summary["compilerReferenceSha256"], "toolPackageSha256": summary["toolPackageSha256"]}):
        raise ValueError("Incomplete outside delivery or tool installation evidence")
    for name in delivery.COMPOSITIONS:
        record = external["examples"][name]
        spec = delivery.composition(name)
        negatives = record["missingDependencies"]
        if (record["status"] != "Passed" or record["versions"] != spec["fhirVersions"] or record["requiredDlls"] != spec["requiredDlls"]
                or any(record.get(k) is not True for k in ("dllReferencesOnly", "publishedExecution", "onlyRequiredInputs"))
                or len(negatives) != len(spec["requiredDlls"]) or {n["assembly"] for n in negatives} != set(spec["requiredDlls"])
                or any(n.get("diagnosed") is not True or not n.get("exitCode") for n in negatives)):
            raise ValueError("Incomplete deployed/missing-dependency evidence")
        deployed = root / "external/deployments" / name
        if delivery.inventory(deployed) != load(root / "external" / (name + "-dll-inventory.json")):
            raise ValueError("Deployed bytes differ")
        report = load(root / "external" / (name + "-execution.json"))
        validate_recorded_execution(name, report, manifest, deployed)
        if any(delivery.sha(deployed / dll) != manifest["files"]["sdk/" + dll] for dll in spec["requiredDlls"]):
            raise ValueError("Scenarios did not use delivered binaries")
    for release in ("R4", "R5"):
        hashes = load(root / "external" / (release + "-generated-hashes.json"))
        if (not hashes or hashes != delivery.inventory(root / "external/generated" / release)
                or hashes != load(root / "bundle/contracts" / ("generated-" + release + ".json"))
                or external["generation"][release] != {"status": "Passed", "sourceCount": sum(p.endswith(".cs") for p in hashes),
                      "repeatedGenerationMatches": True, "matchesCommittedModels": True}):
            raise ValueError("Generated files differ from accepted inputs")
    rollback = summary["rollback"]
    restored = load(root / "rollback-source-hashes.json")
    reports = gate.d3.regression.regression_inventory(root / "rollback-test-results")
    gate.d3.v1.assert_allowed_regression_skips(reports, "")
    if (rollback.get("status") != "Passed" or rollback.get("sourceRevision") != gate.D3_REVISION
            or rollback.get("restoredInputsUnchanged") is not True or not restored or len(restored) != rollback["sourceFiles"]
            or {k: sum(r[k] for r in reports) for k in ("total", "passed", "failed", "skipped")} != rollback["testCounts"]):
        raise ValueError("Incomplete D3 whole-source rollback")
    deployment = load(root / "rollback-deployment/evidence.json")
    if deployment.get("status") != "Passed" or set(deployment["scenarios"]) != {"r4", "r5", "dual", "r4-twcore"}:
        raise ValueError("Rollback deployment failed")
    for name, record in deployment["scenarios"].items():
        composition = {"r4": "R4", "r5": "R5", "dual": "Dual", "r4-twcore": "R4TwCore"}[name]
        expected = {Path(n).stem for n in delivery.composition(composition)["requiredDlls"]}
        negative = record["missingDependencies"]
        if (record.get("outsideCheckout") is not True or len(negative) != len(expected)
                or {n["assembly"] for n in negative} != expected
                or any(n.get("diagnosed") is not True or not n.get("exitCode") for n in negative)
                or record["dllSha256"]["MyFhirSdk.dll"] != deployment["sdkSha256"]):
            raise ValueError("Incomplete rollback negative deployment evidence")
    if summary.get("externalServerAcceptance") != "Pending":
        raise ValueError("Controlled delivery must not claim real server acceptance")
    return summary


def compare(windows, linux):
    a, b = validate_leg(windows), validate_leg(linux)
    if a["os"] != "Windows" or b["os"] != "Linux":
        raise ValueError("Both distinct Windows/Linux legs are required")
    for key in ("sourceRevision", "sourceFingerprint", "toolPackageSha256", "compilerReferenceSha256", "sdkVersion", "rollbackRevision", "testCounts", "freshIntegrationTests"):
        if a[key] != b[key]:
            raise ValueError("Cross-platform mismatch: " + key)
    for release in ("R4", "R5"):
        if load(windows / "external" / (release + "-generated-hashes.json")) != load(linux / "external" / (release + "-generated-hashes.json")):
            raise ValueError("Cross-platform generated bytes differ")
    if load(windows / "rollback-source-hashes.json") != load(linux / "rollback-source-hashes.json"):
        raise ValueError("Rollback source inventories differ")
    return {"gate": "D4", "status": "Passed", "sourceRevision": a["sourceRevision"], "sourceFingerprint": a["sourceFingerprint"],
            "toolPackageSha256": a["toolPackageSha256"], "platforms": ["Windows", "Linux"], "externalServerAcceptance": "Pending"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--windows", type=Path, required=True)
    parser.add_argument("--linux", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = {"gate": "D4", "status": "Failed"}
    try:
        result = compare(args.windows, args.linux)
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
