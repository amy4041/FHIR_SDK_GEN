#!/usr/bin/env python3
"""G2: verify the neutral SPI, bounded independent provider and fresh full SDK regression.

Requires a restored solution, pinned SDK and PowerShell 7.2+ on PATH.
K3 deployment/dependency verification is run by the existing consumer-rebuild gate.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("v1_runner", ROOT / "eng/Test-FhirV1Experiment.py")
v1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v1)
baseline = v1.baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v2/run")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if output == (ROOT / "artifacts").resolve() or not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G2", "status": "Pending",
               "productionContext": True, "completeR4Support": False}
    try:
        files = baseline.source_inventory()
        summary.update(sourceRevision=baseline.run(["git", "rev-parse", "HEAD"]),
                       sourceFingerprint=baseline.fingerprint(files), createdAtUtc=datetime.now(timezone.utc).isoformat(),
                       os=platform.system(), sdkVersion=baseline.run(["dotnet", "--version"]),
                       pythonVersion=platform.python_version(), targetFramework="net9.0", configuration="Release")
        if summary["sdkVersion"] != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
            raise ValueError("Pinned SDK mismatch")
        baseline.write(output / "source-inventory.json", files)
        baseline.run([sys.executable, "eng/Generate-FhirR4V1Experiment.py", "--check"], output / "v1-generation.log")
        baseline.run([sys.executable, "eng/Export-FhirV2ProviderFixture.py", "--check"], output / "v2-fixture.log")
        baseline.run([sys.executable, "-m", "unittest", "discover", "-s", "Tests/FhirVersions", "-v"], output / "integrity.log")
        summary["packages"] = []
        for name in ("r4-package-lock.json", "r5-package-lock.json"):
            lock = json.loads((ROOT / "CodeGen/Policy" / name).read_text())
            inventory = baseline.package_inventory(lock)
            summary["packages"].append({"packageId": lock["packageId"], "version": lock["packageVersion"],
                                         "sha256": inventory["sha256"]})
        error = None
        try:
            baseline.run(["dotnet", "test", "MyFhirSdk.sln", "-c", "Release", "--no-restore",
                          "--logger", "trx;LogFilePrefix=g2", "--results-directory", str(output / "test-results")],
                         output / "regression.log")
        except RuntimeError as failure:
            error = failure
        reports = baseline.regression_inventory(output / "test-results")
        endpoint = os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", "").strip()
        v1.assert_allowed_regression_skips(reports, endpoint)
        summary["integrationEndpoint"] = {"configured": bool(endpoint),
            "sha256": hashlib.sha256(endpoint.encode()).hexdigest() if endpoint else None}
        summary["regression"] = reports
        summary["testCounts"] = {key: sum(r[key] for r in reports) for key in ("total", "passed", "failed", "skipped")}
        cases = []
        for path in sorted((output / "test-results").glob("*.trx")):
            for result in ET.parse(path).getroot().findall(".//{*}UnitTestResult"):
                if result.attrib["testName"].startswith("MyFhirSdk.Tests.Architecture.V2."):
                    cases.append({"test": result.attrib["testName"], "outcome": result.attrib["outcome"]})
        baseline.write(output / "v2-test-cases.json", cases)
        summary["v2Tests"] = {"total": len(cases), "passed": sum(c["outcome"] == "Passed" for c in cases)}
        snapshot_case = "MyFhirSdk.Tests.Architecture.V2.ModelContractsApiSnapshotTests.NeutralSpiAndItsRuntimeSignatureDependenciesMatchTheApprovedSnapshot"
        review_class = "MyFhirSdk.Tests.Architecture.V2.ModelOwnershipReviewTests."
        required_cases = {snapshot_case} | {review_class + method for method in (
            "ForeignEnumerableResourcesCannotBypassOwnershipAtRootOrContained",
            "SharedMetaMappingsMustExistAndUseTheRegisteredCodingType",
            "JsonIgnoredForeignModelsAreRejectedWithTheSameEarlyDiagnostic",
            "RegisteredEnumerableModelsStillTraverseTheirFhirPropertiesAndRules")}
        present_cases = {c["test"].split("(", 1)[0] for c in cases}
        if not required_cases.issubset(present_cases) or len(cases) < 31:
            raise RuntimeError("Missing G2 context or neutral SPI snapshot cases")
        if error or summary["testCounts"]["failed"] or summary["v2Tests"]["passed"] != len(cases):
            raise RuntimeError("G2 or SDK regression failed; see regression.log and TRX")
        reference = ROOT / "Runtime/obj/Release/net9.0/ref/MyFhirSdk.Runtime.dll"
        descriptor = ROOT / "CodeGen/Policy/runtime-contract.json"
        if baseline.sha(reference) != json.loads(descriptor.read_text())["compilerReference"]["sha256"]:
            raise ValueError("Runtime compiler reference changed")
        summary["runtimeContract"] = {"descriptorSha256": baseline.sha(descriptor), "compilerReferenceSha256": baseline.sha(reference)}
        summary["apiSnapshots"] = {p: baseline.sha(ROOT / p) for p in (
            "Tests/Architecture/ApprovedPublicApi.txt", "Tests/Architecture/ApprovedR5ModelApi.txt",
            "Tests/Architecture/ApprovedModelContractsApi.txt")}
        assemblies = {"MyFhirSdk.dll": ROOT / "bin/Release/net9.0/MyFhirSdk.dll",
            "MyFhirSdk.Runtime.dll": ROOT / "Runtime/bin/Release/net9.0/MyFhirSdk.Runtime.dll",
            "MyFhirSdk.ModelContracts.dll": ROOT / "ModelContracts/bin/Release/net9.0/MyFhirSdk.ModelContracts.dll",
            "MyFhirSdk.R4.V1.Models.dll": ROOT / "Tests/FhirVersions/V1/Models/bin/Release/net9.0/MyFhirSdk.R4.V1.Models.dll",
            "MyFhirSdk.R4.V2.Provider.dll": ROOT / "Tests/FhirVersions/V2/Provider/bin/Release/net9.0/MyFhirSdk.R4.V2.Provider.dll"}
        summary["testedImplementationHashes"] = {}
        for name, path in assemblies.items():
            tested = ROOT / "Tests/Architecture/bin/Release/net9.0" / name
            if baseline.sha(path) != baseline.sha(tested):
                raise ValueError(f"Tests did not use the measured Release bytes: {name}")
            summary["testedImplementationHashes"][name] = baseline.sha(tested)
        if baseline.fingerprint(baseline.source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Source changed during G2 run")
        summary["status"] = "Passed"
        print(f"G2 Passed: {summary['v2Tests']['passed']} V2 cases; {summary['testCounts']}")
    except Exception as error:
        summary["status"] = "Failed"
        summary["error"] = str(error).replace(str(ROOT), "<checkout>")
        raise
    finally:
        baseline.write(output / "summary.json", summary)


if __name__ == "__main__":
    main()
