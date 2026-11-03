#!/usr/bin/env python3
"""Run fresh V1 closure verification and the seven-assembly SDK regression.

Requires a restored solution, Python 3.10+, SDK 9.0.317 and PowerShell 7.2+.
Writes raw evidence only to a fresh directory under artifacts/.
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
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("baseline", ROOT / "eng/Export-FhirVersionBaseline.py")
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)

EXTERNAL_SMOKE = "MyFhirSdk.Tests.Client.FhirClientIntegrationSmokeTests.PatientCrudSearchSmokeFlow"
EXTERNAL_SKIP_REASON = "Set MYFHIRSDK_INTEGRATION_BASE_URL to run integration smoke tests."


def assert_allowed_regression_skips(reports, endpoint):
    """Match the existing K3 skip policy for every assembly, not only V1 cases."""
    allowed = 0
    for report in reports:
        for skipped in report["skips"]:
            if (report["assembly"] != "MyFhirSdk.Client.Tests" or skipped["test"] != EXTERNAL_SMOKE or
                    endpoint.strip() or skipped["reason"] != EXTERNAL_SKIP_REASON):
                raise ValueError(f"Unexpected skipped test: {report['assembly']} / {skipped['test']}")
            allowed += 1
    if allowed > 1:
        raise ValueError("Duplicate external Client smoke skip")


def assert_tested_models_match_release(release, tested):
    if baseline.sha(release) != baseline.sha(tested):
        raise ValueError("Architecture tests did not use the measured Release R4 Models implementation")
    return baseline.sha(tested)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v1/run")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G1", "status": "Pending", "r4ProductionSupport": False,
               "v0Revision": "5958d92dd6457a09002662440244ce06de8432bb",
               "stableMainRevision": "ca0c27f81adb729657da8ddab61c71c70e113984"}
    try:
        files = baseline.source_inventory()
        summary.update(sourceRevision=baseline.run(["git", "rev-parse", "HEAD"]),
                       sourceFingerprint=baseline.fingerprint(files),
                       createdAtUtc=datetime.now(timezone.utc).isoformat(),
                       os=platform.system(), sdkVersion=baseline.run(["dotnet", "--version"]),
                       pythonVersion=platform.python_version(), targetFramework="net9.0", configuration="Release",
                       powershellVersion=baseline.run(["pwsh", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"]))
        if summary["sdkVersion"] != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
            raise ValueError("Pinned SDK mismatch")
        temp = Path(tempfile.gettempdir()).resolve()
        summary["testTempDirectory"] = temp.relative_to(ROOT).as_posix() if temp.is_relative_to(ROOT) else "system-temp"
        summary["buildEnvironment"] = {name: os.getenv(name) for name in
                                       ("DOTNET_CLI_USE_MSBUILD_SERVER", "MSBUILDDISABLENODEREUSE")}
        baseline.write(output / "source-inventory.json", files)
        baseline.run([sys.executable, "eng/Generate-FhirR4V1Experiment.py", "--check"], output / "generation-check.log")
        baseline.run([sys.executable, "-m", "unittest", "discover", "-s", "Tests/FhirVersions", "-v"], output / "integrity-tests.log")
        closure = json.loads((ROOT / "Tests/FhirVersions/V1/closure.json").read_text())
        for version in ("r4", "r5"):
            lock = json.loads((ROOT / f"CodeGen/Policy/{version}-package-lock.json").read_text())
            baseline.package_inventory(lock)  # Reverify both archive identities/hashes.
            summary[f"{version}Package"] = {"id": lock["packageId"], "version": lock["packageVersion"], "sha256": lock["sha256"]}
        summary["selectedClosure"] = {"seeds": closure["seeds"], "definitions": len(closure["definitions"]),
                                      "generatedSources": len(closure["sources"]),
                                      "manifestSha256": baseline.sha(ROOT / "Tests/FhirVersions/V1/closure.json")}
        error = None
        try:
            baseline.run(["dotnet", "test", "MyFhirSdk.sln", "-c", "Release", "--no-restore", "--logger", "trx",
                          "--results-directory", str(output / "regression")], output / "regression.log")
        except RuntimeError as caught:
            error = caught
        summary["regression"] = baseline.regression_inventory(output / "regression")
        summary["testCounts"] = {k: sum(r[k] for r in summary["regression"]) for k in ("total", "passed", "failed", "skipped")}
        endpoint = os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", "")
        summary["externalClientSmoke"] = {"endpointConfigured": bool(endpoint.strip()), "endpointSha256": hashlib.sha256(endpoint.encode()).hexdigest(),
                                          "skipPolicy": "Only the known Client smoke may skip, with its exact reason and no configured endpoint"}
        assert_allowed_regression_skips(summary["regression"], endpoint)
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        cases = []
        for trx in sorted((output / "regression").glob("*.trx")):
            for result in ET.parse(trx).getroot().findall(".//t:UnitTestResult", ns):
                if result.attrib["testName"].startswith("MyFhirSdk.Tests.Architecture.V1."):
                    cases.append({"test": result.attrib["testName"], "outcome": result.attrib["outcome"]})
        baseline.write(output / "v1-test-cases.json", cases)
        summary["v1Tests"] = {"total": len(cases), "passed": sum(c["outcome"] == "Passed" for c in cases)}
        if error or summary["testCounts"]["failed"] or not cases or summary["v1Tests"]["passed"] != len(cases):
            raise RuntimeError("V1 or SDK regression failed; see regression.log and TRX")
        descriptor = ROOT / "CodeGen/Policy/runtime-contract.json"
        reference = ROOT / "Runtime/obj/Release/net9.0/ref/MyFhirSdk.Runtime.dll"
        if baseline.sha(reference) != json.loads(descriptor.read_text())["compilerReference"]["sha256"]:
            raise ValueError("Runtime compiler reference changed")
        summary["runtimeContract"] = {"descriptorSha256": baseline.sha(descriptor), "compilerReferenceSha256": baseline.sha(reference)}
        assemblies = [ROOT / "bin/Release/net9.0/MyFhirSdk.dll", ROOT / "bin/Release/net9.0/MyFhirSdk.Runtime.dll",
                      ROOT / "Tests/FhirVersions/V1/Models/bin/Release/net9.0/MyFhirSdk.R4.V1.Models.dll"]
        summary["implementationHashes"] = {p.name: baseline.sha(p) for p in assemblies}
        summary["testedR4ModelsSha256"] = assert_tested_models_match_release(
            assemblies[-1], ROOT / "Tests/Architecture/bin/Release/net9.0/MyFhirSdk.R4.V1.Models.dll")
        summary["runtimeConclusion"] = "Shared Runtime works for this selected closure with the documented abstract bridges and V1 context guard; no compiler contract change."
        summary["productionBlockers"] = ["Neutral public SPI and immutable provider identities", "Nested ownership guard in production engines",
                                         "R5 Models extraction", "Version-specific primitive/model/validation policies and full R4 generation"]
        if baseline.fingerprint(baseline.source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Source changed during V1 run")
        summary["status"] = "Passed"
        print(f"G1 Passed: {summary['v1Tests']['passed']} V1 cases; {summary['testCounts']}")
    except Exception as error:
        summary["status"] = "Failed"
        summary["error"] = str(error).replace(str(ROOT), "<checkout>")
        raise
    finally:
        baseline.write(output / "summary.json", summary)


if __name__ == "__main__":
    main()
