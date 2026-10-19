#!/usr/bin/env python3
"""G6: current SDK regression, fresh versioned-client HTTP tests and explicit R4/R5 external smoke evidence."""
import argparse
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
V5_REVISION = "9aee18656326a175b92d7b9fded1379188d64a4e"
spec = importlib.util.spec_from_file_location("g5", ROOT / "eng/Test-FhirV5Integration.py")
g5 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g5)
baseline = g5.baseline


def test_cases(directory):
    reports = list(directory.glob("*.trx"))
    if len(reports) != 1:
        raise ValueError("Expected one fresh V6 test report")
    root = ET.parse(reports[0]).getroot()
    assemblies = {Path(m.attrib["codeBase"].replace("\\", "/")).stem for m in root.findall(".//{*}TestMethod")}
    if assemblies != {"MyFhirSdk.FhirVersions.V6.Tests"}:
        raise ValueError("Unexpected V6 test assembly")
    cases = [{"test": r.attrib["testName"], "outcome": r.attrib["outcome"]} for r in root.findall(".//{*}UnitTestResult")]
    required = {"CrudSearchUsesTheBoundContextAndHttpHeaders", "SearchRawAndStructuredQueriesReturnVersionOwnedBundleEntries",
        "HttpErrorsExposeVersionOwnedOperationOutcomeAndRequestDetails", "MalformedErrorBodyKeepsHttpFailureAndRawBody",
        "Read404ReturnsNullAndDelete200ReturnsTypedOutcome", "InvalidSuccessResponsesAreWrappedAsInvalidResponse",
        "UnknownContainedResourceInSearchIsRejected", "DefaultValidationUsesContextAndCanBeDisabled",
        "ForeignModelsAreRejectedBeforeHttpEvenWithoutValidation", "ForeignReadAndAbstractSearchTypesAreRejectedBeforeHttp",
        "MissingIdsAreRejectedBeforeHttp", "DotSegmentIdsAreRejectedBeforeHttp", "CancellationPropagatesWithoutBeingWrapped", "ResponseAndRequestContentAreDisposedAfterParsing",
        "DeleteErrorsRetainHttpStatusAndUnexpectedSuccessBodiesAreRejected", "SearchRejectsEmptyOrNonBundleSuccessBodies",
        "DefaultValidationHonorsTheReleaseSpecificLexicalRules", "AuthenticationFailuresDisposeRequestsWithoutSendingHttp"}
    for release in ("R4", "R5"):
        present = {c["test"].split("(")[0].split(".")[-1] for c in cases if f".{release}ClientTests." in c["test"]}
        if not required <= present:
            raise ValueError("Missing version-specific HTTP test coverage: " + release)
    composition = {"MixedReleaseAndIncorrectBundleOrOutcomeArgumentsAreRejectedAtConstruction", "LegacyClientKeepsR5DefaultsAndRejectsForeignReadsBeforeHttp",
        "ParallelR4AndR5ClientsUseIndependentFactoriesAndWireRules", "ClientApiMatchesApprovedSnapshot"}
    counters = root.find(".//{*}Counters").attrib
    if (len(cases) < 92 or not composition <= {c["test"].split("(")[0].split(".")[-1] for c in cases}
            or any(c["outcome"] != "Passed" for c in cases) or int(counters["total"]) != len(cases)
            or int(counters["passed"]) != len(cases) or int(counters["failed"]) != 0):
        raise ValueError("Incomplete fresh V6 HTTP regression")
    return cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v6/run")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if output == ROOT / "artifacts" or not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G6", "status": "Pending", "scope": "Client implementation and controlled HTTP acceptance", "deliveryGate": "G7 Pending"}
    previous_ui = os.environ.get("DOTNET_CLI_UI_LANGUAGE")
    os.environ["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
    try:
        archive = subprocess.check_output(["git", "archive", "--format=tar", V5_REVISION, "Generated/R4", "Generated/R5"], cwd=ROOT)
        with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
            historical = {m.name: hashlib.sha256(tar.extractfile(m).read()).hexdigest() for m in tar if m.isfile()}
        current = {p.relative_to(ROOT).as_posix(): baseline.sha(p) for release in ("R4", "R5")
            for p in (ROOT / "Generated" / release).rglob("*") if p.is_file()}
        if current != historical:
            raise ValueError("V6 changed committed R4/R5 generation from the accepted V5 revision")
        summary["modelBaselineRevision"] = V5_REVISION
        summary["preservedModelSources"] = {r: sum(p.startswith(f"Generated/{r}/") and p.endswith(".cs") for p in current) for r in ("R4", "R5")}
        sys.argv = ["Test-FhirV5Integration.py", "--output", str(output / "sdk")]
        g5.main()
        regression = json.loads((output / "sdk/summary.json").read_text(encoding="utf-8"))
        if regression["status"] != "Passed":
            raise ValueError("G5 prerequisites failed")
        for key in ("sourceRevision", "sourceFingerprint", "createdAtUtc", "os", "sdkVersion", "testCounts", "runtimeContract",
                    "toolVersion", "toolPackageSha256", "preservedGeneratedSources", "rollbackRegression"):
            summary[key] = regression[key]
        with tempfile.TemporaryDirectory(prefix="MyFhirSdk-V6-Fresh-") as temporary:
            stage = Path(temporary).resolve()
            if stage.is_relative_to(ROOT):
                raise ValueError("Client integration must run outside checkout")
            for row in baseline.source_inventory():
                if row["path"].startswith(("Generated/R4/", "Generated/R5/")):
                    continue
                destination = stage / row["path"]
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(ROOT / row["path"], destination)
            if any(stage.rglob("bin")) or any(stage.rglob("obj")) or (stage / "Generated").exists():
                raise ValueError("Fresh client stage contains reused build/generated outputs")
            for release in ("R4", "R5"):
                shutil.copytree(output / "sdk/generator/generated" / release, stage / "Generated" / release)
            before = g5.g4.inventory(stage / "Generated")
            environment = dict(os.environ)
            for key in ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS"):
                environment.pop(key, None)

            def run(name, arguments, timeout=300):
                result = subprocess.run(["dotnet", *arguments], cwd=stage, env=environment,
                    capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
                log = (result.stdout + result.stderr).replace(str(stage), "<fresh>").replace(str(ROOT), "<checkout>")
                (output / (name + ".log")).write_text(log, encoding="utf-8", newline="\n")
                if result.returncode:
                    raise ValueError("Fresh client check failed: " + name)

            run("fresh-client-tests", ["test", "Tests/FhirVersions/V6/MyFhirSdk.FhirVersions.V6.Tests.csproj", "-c", "Release",
                "--logger", "trx;LogFileName=fresh-v6.trx", "--results-directory", str(output / "fresh-test-results")])
            cases = test_cases(output / "fresh-test-results")
            baseline.write(output / "v6-test-cases.json", cases)
            run("smoke-build", ["build", "Tests/FhirVersions/V6/Smoke/VersionedClientSmoke.csproj", "-c", "Release"])
            smoke = stage / "Tests/FhirVersions/V6/Smoke/bin/Release/net9.0/VersionedClientSmoke.dll"
            smokes = {}
            for release in ("R4", "R5"):
                target = output / (release + "-external-smoke.json")
                run(release + "-external-smoke", [str(smoke), release, str(target)], timeout=360)
                smokes[release] = json.loads(target.read_text(encoding="utf-8"))
                configured = bool(environment.get(f"MYFHIRSDK_{release}_INTEGRATION_BASE_URL", "").strip())
                expected = "Passed" if configured else "Skipped"
                if smokes[release]["status"] != expected:
                    raise ValueError("Unexpected external smoke result: " + release)
            if g5.g4.inventory(stage / "Generated") != before:
                raise ValueError("Client integration changed generated inputs")
            binaries = stage / "Tests/FhirVersions/V6/bin/Release/net9.0"
            summary["freshClientIntegration"] = {"outsideCheckout": True, "reusedBinObj": False, "reusedCommittedGeneratedSources": False,
                "testsPassed": len(cases), "generatedInputsUnchanged": True,
                "clientApiSnapshotSha256": baseline.sha(ROOT / "Tests/FhirVersions/V6/ApprovedClientApi.txt"),
                "testedImplementationHashes": {name: baseline.sha(binaries / name) for name in (
                    "MyFhirSdk.dll", "MyFhirSdk.Runtime.dll", "MyFhirSdk.ModelContracts.dll", "MyFhirSdk.ModelSupport.dll",
                    "MyFhirSdk.R4.Models.dll", "MyFhirSdk.R5.Models.dll")}}
            summary["externalSmokes"] = smokes
            summary["externalServerAcceptance"] = "Passed" if all(s["status"] == "Passed" for s in smokes.values()) else "Pending"
        if baseline.fingerprint(baseline.source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Source changed during G6 run")
        summary.update(status="Passed", r4ClientImplementationAccepted=True)
        print(f"G6 Passed: {len(cases)} fresh HTTP cases; {summary['testCounts']}; external server acceptance: {summary['externalServerAcceptance']}")
    except Exception as error:
        summary.update(status="Failed", error=str(error).replace(str(ROOT), "<checkout>"))
        raise
    finally:
        baseline.write(output / "summary.json", summary)
        if previous_ui is None:
            os.environ.pop("DOTNET_CLI_UI_LANGUAGE", None)
        else:
            os.environ["DOTNET_CLI_UI_LANGUAGE"] = previous_ui


if __name__ == "__main__":
    main()
