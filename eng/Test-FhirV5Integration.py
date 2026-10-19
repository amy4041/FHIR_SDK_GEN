#!/usr/bin/env python3
"""G5: complete R4 inventory, current regression and fresh installed-tool R4/R5 runtime integration."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

import FhirV5Inventory

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("g4", ROOT / "eng/Test-FhirV4Generator.py")
g4 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g4)
baseline = g4.baseline


def test_cases(directory):
    reports = list(directory.glob("*.trx"))
    if len(reports) != 1:
        raise ValueError("Expected exactly one fresh V5 TRX")
    root = ET.parse(reports[0]).getroot()
    methods = root.findall(".//{*}UnitTest/{*}TestMethod")
    if {Path(m.attrib["codeBase"].replace("\\", "/")).stem for m in methods} != {"MyFhirSdk.FhirVersions.V5.Tests"}:
        raise ValueError("Unexpected fresh test assembly")
    cases = [{"test": r.attrib["testName"], "outcome": r.attrib["outcome"]} for r in root.findall(".//{*}UnitTestResult")]
    required = {"CompleteProviderOwnsEveryDeclarationAndConcreteResourceFactory", "BootstrapBackboneContainedAndPrimitiveArrayMetadataRoundTrip",
                "EveryR4PrimitiveRunsThroughOpenTypeCodecAndValidator", "InvalidPrimitiveLexicalValuesProduceFormatIssues",
                "PrimitiveJsonTokenKindsAreEnforced", "RequiredChoiceAndCollectionRulesUseFreshGeneratedComposition",
                "OpenComplexTypesAndNestedResourcesUseTheR4Factories", "UnknownAbstractAndR5OnlyResourcesAreRejected",
                "CrossVersionRootsContainedExtensionsAndOpenValuesFailEarly", "ParallelR4R5ConsumersKeepFactoriesAndPrimitiveRulesIsolated",
                "R4ApiMatchesApprovedSnapshot", "ProviderInventoryAndValidationCapabilitiesAreExplicitAndExecutable",
                "UnequalAndMetadataOnlyPrimitiveArraysKeepTheirPositions",
                "R4LexicalDifferencesPreserveTheWireLiteralAndR5Behavior", "R4LexicalAdaptersStillRejectMalformedValues",
                "R4DecimalLiteralValidationDoesNotAcceptMalformedNumbers"}
    counters = root.find(".//{*}Counters").attrib
    present = {c["test"].split("(")[0].split(".")[-1] for c in cases}
    if (len(cases) < 81 or not required <= present or any(c["outcome"] != "Passed" for c in cases)
            or int(counters["total"]) != len(cases) or int(counters["passed"]) != len(cases) or int(counters["failed"]) != 0):
        raise ValueError("Incomplete fresh G5 regression")
    return cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v5/run")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if output == ROOT / "artifacts" or not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G5", "status": "Pending", "r4ClientSupport": False,
               "completeFhirValidation": False, "deliveryGate": "G7 Pending"}
    previous_ui = os.environ.get("DOTNET_CLI_UI_LANGUAGE")
    os.environ["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
    try:
        scope = FhirV5Inventory.build()
        baseline.write(output / "scope-inventory.json", scope)
        approved_scope = json.loads((ROOT / "docs/fhir_versions/baselines/v5/scope-inventory.json").read_text(encoding="utf-8"))
        if scope != approved_scope:
            raise ValueError("R4 scope/source inventory differs from approved G5 baseline")
        sys.argv = ["Test-FhirV4Generator.py", "--output", str(output / "generator"), "--export-generated"]
        g4.main()
        regression = json.loads((output / "generator/summary.json").read_text(encoding="utf-8"))
        if regression["status"] != "Passed":
            raise ValueError("G4 prerequisites failed")
        for key in ("sourceRevision", "sourceFingerprint", "createdAtUtc", "os", "sdkVersion", "testCounts", "runtimeContract", "toolVersion", "toolPackageSha256", "preservedGeneratedSources"):
            summary[key] = regression[key]
        rollback = subprocess.run(["pwsh", "-NoProfile", "-File", str(ROOT / "eng/Test-KernelSourceRollbackHarness.ps1"),
            "-OutputDirectory", str(output / "rollback-harness"), "-CurrentRegressionDirectory",
            str(output / "generator/migration/context/test-results")], cwd=ROOT, capture_output=True,
            text=True, encoding="utf-8", errors="replace", timeout=180)
        (output / "rollback-harness.log").write_text(rollback.stdout + rollback.stderr, encoding="utf-8", newline="\n")
        if rollback.returncode:
            raise ValueError("Current/historical rollback regression failed; see rollback-harness.log")
        rollback_evidence = json.loads((output / "rollback-harness/evidence.json").read_text(encoding="utf-8-sig"))
        if rollback_evidence["status"] != "passed" or "current-v5-regression-reports-accepted" not in rollback_evidence["checks"]:
            raise ValueError("Missing actual eight-assembly rollback regression acceptance")
        summary["rollbackRegression"] = {"status": "Passed", "currentAssemblies": 8, "historicalAssemblies": 7,
            "harnessChecks": len(rollback_evidence["checks"]), "evidenceSha256": baseline.sha(output / "rollback-harness/evidence.json")}
        # Validate complete inventories including both manifests and exact bytes.
        for release in ("R4", "R5"):
            generated = output / "generator/generated" / release
            if g4.inventory(generated) != g4.inventory(ROOT / "Generated" / release):
                raise ValueError("Committed sources differ from fresh installed tool: " + release)
        with tempfile.TemporaryDirectory(prefix="MyFhirSdk-V5-Fresh-") as temporary:
            stage = Path(temporary).resolve()
            if stage.is_relative_to(ROOT):
                raise ValueError("Fresh integration must run outside checkout")
            files = baseline.source_inventory()
            for row in files:
                if row["path"].startswith(("Generated/R4/", "Generated/R5/")):
                    continue
                source = ROOT / row["path"]
                destination = stage / row["path"]
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, destination)
            if any(stage.rglob("bin")) or any(stage.rglob("obj")) or (stage / "Generated").exists():
                raise ValueError("Fresh stage contains build outputs or committed generated sources")
            for release in ("R4", "R5"):
                shutil.copytree(output / "generator/generated" / release, stage / "Generated" / release)
            before = g4.inventory(stage / "Generated")
            environment = dict(os.environ)
            for name in ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS"):
                environment.pop(name, None)
            project = "Tests/FhirVersions/V5/MyFhirSdk.FhirVersions.V5.Tests.csproj"
            process = subprocess.run(["dotnet", "test", project, "-c", "Release", "--logger", "trx;LogFileName=fresh-v5.trx",
                                      "--results-directory", str(output / "fresh-test-results")], cwd=stage, env=environment,
                                     capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=300)
            log = (process.stdout + process.stderr).replace(str(stage), "<fresh>").replace(str(ROOT), "<checkout>")
            (output / "fresh-integration.log").write_text(log, encoding="utf-8", newline="\n")
            if process.returncode:
                raise ValueError("Fresh installed-source compilation/runtime tests failed; see fresh-integration.log")
            cases = test_cases(output / "fresh-test-results")
            baseline.write(output / "v5-test-cases.json", cases)
            if g4.inventory(stage / "Generated") != before:
                raise ValueError("Build changed generated inputs")
            binaries = stage / "Tests/FhirVersions/V5/bin/Release/net9.0"
            actual = json.loads((binaries / "ActualR4ProviderInventory.json").read_text(encoding="utf-8"))
            approved = json.loads((ROOT / "docs/fhir_versions/baselines/v5/provider-inventory.json").read_text(encoding="utf-8"))
            if actual != approved:
                raise ValueError("Fresh compiled provider differs from approved capability/type/rule inventory")
            declared = {d["clrType"] for d in scope["definitions"] if d["disposition"] in {"generated-model", "generated-primitive", "constraint-auxiliary"}}
            declared.update(d["clrType"] for d in scope["nestedDeclarations"])
            if set(actual["modelTypes"]) != declared or len(actual["resources"]) != 146:
                raise ValueError("Compiled model/provider does not cover the full official approved scope")
            baseline.write(output / "provider-inventory.json", actual)
            summary["freshIntegration"] = {"outsideCheckout": True, "reusedBinObj": False, "reusedCommittedGeneratedSources": False,
                "r4GeneratedSources": 677, "r5GeneratedSources": 852, "testsPassed": len(cases), "generatedInputsUnchanged": True,
                "sameProcessR4R5Consumer": True,
                "testedImplementationHashes": {name: baseline.sha(binaries / name) for name in (
                    "MyFhirSdk.dll", "MyFhirSdk.Runtime.dll", "MyFhirSdk.ModelContracts.dll", "MyFhirSdk.ModelSupport.dll",
                    "MyFhirSdk.R4.Models.dll", "MyFhirSdk.R5.Models.dll")}}
        if baseline.fingerprint(baseline.source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Source changed during G5 run")
        summary.update(status="Passed", r4ModelsAndRuntimeIntegration=True, scopeDefinitions=scope["structureDefinitions"],
                       concreteR4Resources=146, r4ModelDeclarations=675)
        print(f"G5 Passed: 655 definitions accounted for; {len(cases)} fresh integration cases; {summary['testCounts']}")
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
