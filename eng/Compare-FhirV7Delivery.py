#!/usr/bin/env python3
"""Validate two G7 artifacts before accepting Windows/Ubuntu delivery."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

EXPECTED = {"R4": 677, "R5": 852}
FHIR_VERSIONS = {"R4": "4.0.1", "R5": "5.0.0"}
SHARED = {"MyFhirSdk.dll", "MyFhirSdk.Runtime.dll", "MyFhirSdk.ModelContracts.dll", "MyFhirSdk.ModelSupport.dll"}
ASSEMBLIES = {"MyFhirSdk.Architecture.Tests", "MyFhirSdk.CodeGen.Tests", "MyFhirSdk.Client.Tests", "MyFhirSdk.Parser.Json.Tests",
    "MyFhirSdk.Serialization.Json.Tests", "MyFhirSdk.Validation.Tests", "MyFhirSdk.ImplementationGuides.TwCore.Tests",
    "MyFhirSdk.FhirVersions.V5.Tests", "MyFhirSdk.FhirVersions.V6.Tests"}
ROLLBACK_REVISION = "99017eff8c608aa1e34292ddaf10eab60912bbed"


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check_reports(directory, expected_assemblies, expected_counts, records=None):
    assemblies = []
    cases = []
    for path in sorted(directory.glob("*.trx")):
        root = ET.parse(path).getroot()
        names = {Path(m.attrib["codeBase"].replace("\\", "/")).stem for m in root.findall(".//{*}TestMethod")}
        if len(names) != 1:
            raise ValueError("Unexpected TRX assembly identity")
        assembly = names.pop()
        assemblies.append(assembly)
        results = root.findall(".//{*}UnitTestResult")
        outcomes = Counter(r.attrib["outcome"] for r in results)
        counters = root.find(".//{*}Counters").attrib
        if (not outcomes["Passed"] or set(outcomes) - {"Passed", "NotExecuted"}
                or int(counters["total"]) != len(results) or int(counters["passed"]) != outcomes["Passed"] or int(counters["failed"]) != 0):
            raise ValueError("Unsuccessful or inconsistent TRX counters/results")
        for result in results:
            if result.attrib["outcome"] == "NotExecuted" and (assembly != "MyFhirSdk.Client.Tests" or
                    result.attrib["testName"] != "MyFhirSdk.Tests.Client.FhirClientIntegrationSmokeTests.PatientCrudSearchSmokeFlow"):
                raise ValueError("Unexpected skipped case")
            cases.append({"test": result.attrib["testName"], "outcome": result.attrib["outcome"]})
        if records is not None:
            matches = [r for r in records if r["assembly"] == assembly]
            if len(matches) != 1 or matches[0]["sha256"] != sha(path):
                raise ValueError("TRX bytes do not match accepted evidence")
    counts = {"total": len(cases), "passed": sum(c["outcome"] == "Passed" for c in cases), "failed": 0,
        "skipped": sum(c["outcome"] == "NotExecuted" for c in cases)}
    if len(assemblies) != len(expected_assemblies) or set(assemblies) != expected_assemblies or counts != expected_counts:
        raise ValueError("Missing TRX assemblies or changed test counts")
    return cases


def check_external_acceptance(root, data, prerequisite):
    embedded = prerequisite.get("externalSmokes", {})
    if set(embedded) != set(FHIR_VERSIONS):
        raise ValueError("Missing version-specific external smoke evidence")
    passed = []
    for release, version in FHIR_VERSIONS.items():
        smoke = load(root / "prerequisite" / (release + "-external-smoke.json"))
        if smoke != embedded[release]:
            raise ValueError("External smoke file/summary mismatch: " + release)
        if smoke.get("release") != release or smoke.get("expectedFhirVersion") != version:
            raise ValueError("External smoke release/version mismatch: " + release)
        status = smoke.get("status")
        if status == "Passed":
            if (smoke.get("serverFhirVersion") != version or smoke.get("writesStarted") is not True
                    or set(smoke.get("operations", [])) != {"metadata-version", "create", "read", "update", "search", "delete"}):
                raise ValueError("Incomplete passed external smoke: " + release)
        elif status != "Skipped" or not smoke.get("reason"):
            raise ValueError("Failed or incomplete external smoke: " + release)
        passed.append(status == "Passed")
    derived = "Passed" if all(passed) else "Pending"
    if prerequisite.get("externalServerAcceptance") != derived or data.get("externalServerAcceptance") != derived:
        raise ValueError("External server acceptance does not match smoke evidence")
    return derived


def validate_leg(root):
    data = load(root / "summary.json")
    if data.get("gate") != "G7-platform" or data.get("status") != "Passed" or data["os"] not in {"Windows", "Linux"}:
        raise ValueError("Expected a passed Windows/Linux G7 platform artifact")
    if sha(root / "canonical-tool.nupkg") != data["toolPackageSha256"]:
        raise ValueError("Canonical package hash mismatch")
    if sha(root / "prerequisite/summary.json") != data["prerequisiteSha256"]:
        raise ValueError("G6 prerequisite hash mismatch")
    prerequisite = load(root / "prerequisite/summary.json")
    if prerequisite["status"] != "Passed" or prerequisite["sourceFingerprint"] != data["sourceFingerprint"] or prerequisite["testCounts"] != data["testCounts"]:
        raise ValueError("G6 prerequisite source/count mismatch")
    data["externalServerAcceptance"] = check_external_acceptance(root, data, prerequisite)
    if data["testCounts"]["failed"] or data["testCounts"]["passed"] <= 0 or data["freshV5Tests"] < 81 or data["freshV6Tests"] < 92:
        raise ValueError("Incomplete G7 integration regression")
    check_reports(root / "prerequisite/sdk/generator/migration/context/test-results", ASSEMBLIES, data["testCounts"],
        load(root / "prerequisite-reports.json"))
    for version in (5, 6):
        cases = load(root / f"v{version}-test-cases.json")
        if len(cases) != data[f"freshV{version}Tests"] or any(c["outcome"] != "Passed" for c in cases):
            raise ValueError("Fresh integration cases do not match summary")
        actual = check_reports(root / f"fresh-v{version}-results", {f"MyFhirSdk.FhirVersions.V{version}.Tests"},
            {"total": len(cases), "passed": len(cases), "failed": 0, "skipped": 0})
        if actual != cases:
            raise ValueError("Fresh TRX results do not match accepted cases")
    for section in ("canonicalInstall", "freshIntegration"):
        if not data[section] or any(v is not True for k, v in data[section].items() if k not in {"reusedBinObj", "reusedCommittedGeneratedSources"}):
            raise ValueError("Isolation was not accepted")
    if data["freshIntegration"]["reusedBinObj"] or data["freshIntegration"]["reusedCommittedGeneratedSources"]:
        raise ValueError("Fresh integration reused outputs")
    for release, count in EXPECTED.items():
        record = data["generation"][release]
        path = root / (release + "-generated-hashes.json")
        hashes = load(path)
        actual = {p.relative_to(root / "generated" / release).as_posix(): sha(p) for p in (root / "generated" / release).rglob("*") if p.is_file()}
        if sha(path) != record["hashesSha256"] or hashes != actual or sum(p.endswith(".cs") for p in hashes) != count:
            raise ValueError("Generated artifact/hash inventory mismatch: " + release)
        if not record["deterministic"] or not record["matchesCommittedModels"] or record["sourceCount"] != count or record["fileCount"] != len(hashes):
            raise ValueError("Incomplete generation acceptance: " + release)
    if set(data["deployments"]) != {"R4", "R5", "Dual"}:
        raise ValueError("Missing delivery consumer")
    expected_negative = set()
    for consumer, record in data["deployments"].items():
        required = SHARED | ({"MyFhirSdk.R4.Models.dll", "MyFhirSdk.R5.Models.dll"} if consumer == "Dual" else {f"MyFhirSdk.{consumer}.Models.dll"})
        hashes_path = root / (consumer + "-deployment-hashes.json")
        hashes = load(hashes_path)
        directory = root / "deployments" / consumer
        actual = {p.relative_to(directory).as_posix(): sha(p) for p in directory.rglob("*") if p.is_file()}
        required_files = required | {consumer + "Consumer" + suffix for suffix in (".dll", ".deps.json", ".runtimeconfig.json")}
        missing = required_files - actual.keys()
        if missing:
            raise ValueError("Missing required deployment files for " + consumer + ": " + ", ".join(sorted(missing)))
        if (record["status"] != "Passed" or record["ownDirectoryOnly"] is not True or set(record["requiredDlls"]) != required
                or record["files"] != len(actual) or hashes != actual or sha(hashes_path) != record["hashesSha256"]):
            raise ValueError("Deployment evidence mismatch: " + consumer)
        expected_negative.update((consumer, name) for name in required)
    negative = load(root / "missing-dependencies.json")
    if ({(r["consumer"], r["missing"]) for r in negative} != expected_negative or len(negative) != len(expected_negative)
            or len(negative) != data["missingDependencyCases"] or any(r["status"] != "Passed" or r["exitCode"] == 0 for r in negative)):
        raise ValueError("Incomplete missing-dependency evidence")
    rollback = data["rollback"]
    if (rollback["status"] != "Passed" or not rollback["freshRebuild"] or rollback["currentBranchChanged"] or rollback["mainChanged"]
            or not rollback["restoredInputsUnchanged"] or rollback["testCounts"]["failed"] or len(rollback["assemblies"]) != 9):
        raise ValueError("Incomplete rollback acceptance")
    if rollback["sourceRevision"] != ROLLBACK_REVISION:
        raise ValueError("Rollback revision is not the accepted V6 pin")
    check_reports(root / "rollback-test-results", ASSEMBLIES, rollback["testCounts"], load(root / "rollback-regression.json"))
    return data


def compare(windows, linux):
    a, b = validate_leg(windows), validate_leg(linux)
    if (a["os"], b["os"]) != ("Windows", "Linux"):
        raise ValueError("Two distinct Windows/Linux artifacts are required")
    for key in ("sourceRevision", "portableSourceFingerprint", "sdkVersion", "targetFramework", "configuration", "toolVersion",
                "toolPackageSha256", "runtimeContract", "policies", "testCounts", "freshV5Tests", "freshV6Tests"):
        if a[key] != b[key]:
            raise ValueError("Cross-platform mismatch: " + key)
    if any(a["rollback"][k] != b["rollback"][k] for k in ("sourceRevision", "archiveSha256", "testCounts", "assemblies")):
        raise ValueError("Rollback source/count mismatch")
    for release in EXPECTED:
        if a["generation"][release] != b["generation"][release]:
            # Inventory JSON file bytes may have platform line endings; compare semantic fields instead.
            for key in ("fhirVersion", "packageSha256", "sourceCount", "fileCount", "deterministic", "matchesCommittedModels"):
                if a["generation"][release][key] != b["generation"][release][key]:
                    raise ValueError("Generation contract mismatch: " + release)
        if load(windows / (release + "-generated-hashes.json")) != load(linux / (release + "-generated-hashes.json")):
            raise ValueError("Cross-platform generated bytes differ: " + release)
    return {"schemaVersion": 1, "gate": "G7", "status": "Passed", "platforms": ["Windows", "Linux"],
        "sourceRevision": a["sourceRevision"], "portableSourceFingerprint": a["portableSourceFingerprint"],
        "toolPackageSha256": a["toolPackageSha256"], "sdkVersion": a["sdkVersion"],
        "canonicalGenerationMatches": True, "deploymentAndRollbackAccepted": True,
        "externalServerAcceptance": "Passed" if all(d["externalServerAcceptance"] == "Passed" for d in (a, b)) else "Pending",
        "publicNuGetRelease": "OutOfScope", "platformSummaryHashes": {"Windows": sha(windows / "summary.json"), "Linux": sha(linux / "summary.json")}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--windows", type=Path, required=True)
    parser.add_argument("--linux", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = {"schemaVersion": 1, "gate": "G7", "status": "Failed"}
    try:
        result = compare(args.windows, args.linux)
        print("G7 Passed: same canonical tool and R4/R5 generated bytes; both deployments and rollbacks accepted.")
    except Exception as error:
        result["error"] = str(error)
        raise
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
