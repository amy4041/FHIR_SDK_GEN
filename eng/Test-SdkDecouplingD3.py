#!/usr/bin/env python3
"""D3 current-source gate: exact API delta, invariant sources and fresh regression.

Requires the solution to be restored with the pinned SDK before invocation.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
D0_REVISION = "447e7f141c5edf1e803b3d6c85c3bfca9778c5de"
D2_REVISION = "c2e9098225718c79daf1aed4289d4f806ebab570"
D1_REVISION = "464474b190e702231d4b26a7883f1edbebb13ac2"
BASELINE = ROOT / "docs/sdk_decoupling/baselines/d3"
STABLE_ROOTS = ("Runtime/", "ModelContracts/", "ModelSupport/", "Models/", "Generated/", "CodeGen/",
                "core/", "Tests/CodeGen/Fixtures/FhirPackages/", "Tests/KernelMigration/Inventory/")


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


d0 = module("d0", "eng/Test-SdkDecoupling.py")
regression = module("regression", "eng/Export-FhirVersionBaseline.py")
v1 = module("v1", "eng/Test-FhirV1Experiment.py")


def required_tests():
    """Reviewed case identities, independent of test discovery or current source."""
    validation = "MyFhirSdk.Tests.Validation.Profiles.ContextualProfileValidatorTests."
    architecture = "MyFhirSdk.Tests.Architecture.D1.ContextualValidatorContractTests."
    cases = {}

    def add(method, variants=("",)):
        cases.update({validation + method + variant: "MyFhirSdk.Validation.Tests" for variant in variants})

    for method in ("RealValidatorExposesItsExactContextAndMergesBaseAndIgIssues",
                   "CustomValidatorAndPackageReplacementUseOnlySelectedImplementations",
                   "VersionMismatchIsRejectedBeforeRuleEnumeration",
                   "KnownUnsupportedResourceIsErrorEvenWhenUnknownProfilesAreIgnored",
                   "DeclaredSupportedResourceWithNoExtraRulesStillRunsBaseValidation",
                   "OwnershipIsCheckedBeforeCustomValidatorOrRules",
                   "PositiveResourceWithSupportedProfilePassesRealBaseValidation"):
        add(method, ("(r5: False)", "(r5: True)"))
    add("DuplicateProfileRegistrationsAreRejectedRegardlessOfOrder", ("(reverse: False)", "(reverse: True)"))
    add("NoncanonicalBaseVersionsAreNotNormalized",
        tuple('(version: ' + value + ')' for value in
              ('"R4.0.1"', '"4.0"', '"04.0.1"', '"4.0.1-preview"', '"4.0.1\\n"', '""', 'null')))
    add("UnknownProfileOptionsApplyToExplicitAndDeclaredProfiles",
        ("(behavior: Ignore, count: 0)", "(behavior: Warning, count: 1)", "(behavior: Error, count: 1)"))
    add("InvalidDeclaredProfileKeysAreRejectedAtConstruction",
        ('(profile: null)', '(profile: "")', '(profile: " ")', '(profile: "https://example.test/p|1")'))
    for method in ("DuplicateDeclaredProfilesRunBaseAndRuleOnce",
                   "VersionQualifiedProfilesAreDiagnosedEvenWithIgnorePolicy",
                   "InvalidRuleCollectionsAndEntriesAreConfigurationFailures",
                   "ContextualCompositionRejectsMissingDependenciesAndInvalidOptions"):
        add(method)
    for method in ("GeneralValidatorInterfaceIsUnchangedAndContextualInterfaceIsReadOnly",
                   "OnlyContextualCompositionOverloadsRemain",
                   "InternalRuleOnlyValidatorCannotPretendToHaveAR5Context"):
        cases[architecture + method] = "MyFhirSdk.Architecture.Tests"
    twcore = "MyFhirSdk.Tests.ImplementationGuides.TwCore."
    for name in (
        "TwCorePackageTests.DefaultPackageExposesTwCoreMetadata",
        "TwCorePackageTests.SupportsTwCorePatientProfile",
        "TwCorePackageTests.GetRulesReturnsInitialPatientRules",
        "TwCorePackageTests.GetRulesReturnsEmptyForUnsupportedResourceType",
        "Validation.TwCorePatientValidationTests.EmptyPatientRemainsValidForBaseFhirButInvalidForTwCorePatient",
        "Validation.TwCorePatientValidationTests.ValidateReportsMissingIdentifierSystem",
        "Validation.TwCorePatientValidationTests.ValidateReportsMissingIdentifierValue",
        "Validation.TwCorePatientValidationTests.ValidateReportsMissingSystemAndValueForEachIdentifier",
        "Validation.TwCorePatientValidationTests.ValidatePassesWhenIdentifierHasSystemAndValue",
        "Validation.KernelTwCoreMigrationTests.ProfileRulesStillTraverseRuntimeMetaAndSdkIdentifiersAfterRoundTrip",
        "D2.R4TwCoreCompositionTests.TwCoreTypesAreOwnedOnlyByTheIndependentIgAssembly",
        "D2.R4TwCoreCompositionTests.PackageSupportsOnlyItsRegisteredR4PatientPair",
        "D2.R4TwCoreCompositionTests.R5ContextCannotComposeTheR4Package",
        "D2.R4TwCoreCompositionTests.R5PatientIsRejectedByTheR4Composition",
        "D2.R4TwCoreCompositionTests.ExtensionOnlyPrimitiveCountsAsPresentAfterR4RoundTrip(system: True)",
        "D2.R4TwCoreCompositionTests.ExtensionOnlyPrimitiveCountsAsPresentAfterR4RoundTrip(system: False)",
        "D2.R4TwCoreCompositionTests.EmptyPrimitiveWithoutExtensionDoesNotCountAsPresent(system: True)",
        "D2.R4TwCoreCompositionTests.EmptyPrimitiveWithoutExtensionDoesNotCountAsPresent(system: False)",
        "D2.R4TwCoreCompositionTests.DeclaredProfileMergesBaseAndIgIssuesWithOriginalProvenance",
        "D2.R4TwCoreCompositionTests.NullIdentifierEntriesDoNotSatisfyTheRequiredIdentifierRule"):
        cases[twcore + name] = "MyFhirSdk.ImplementationGuides.TwCore.Tests"
    legacy = twcore + "D2.R4TwCoreCompositionTests."
    for with_options in ("False", "True"):
        cases[legacy + "ContextualOverloadsStillExecuteRulesForSupportedR4Patient(withOptions: " + with_options + ")"] = "MyFhirSdk.ImplementationGuides.TwCore.Tests"
        for selection in range(3):
            cases[legacy + "ContextualOverloadsRejectUnsupportedR4ResourceForEveryProfileSelection(withOptions: " + with_options +
                  ", selection: " + str(selection) + ")"] = "MyFhirSdk.ImplementationGuides.TwCore.Tests"
    for name in ("SdkAssemblyDependsOnlyOnNeutralSdkComponents", "RetiredDefaultsClientsAndProfileOverloadsAreAbsent"):
        cases["MyFhirSdk.Tests.Architecture.D3.NeutralSdkTests." + name] = "MyFhirSdk.Architecture.Tests"
    return cases


def required_test_evidence(trx_paths):
    required = required_tests()
    observed = {name: [] for name in required}
    errors = []
    prefixes = ("MyFhirSdk.Tests.Validation.Profiles.ContextualProfileValidatorTests.",
                "MyFhirSdk.Tests.Architecture.D1.ContextualValidatorContractTests.",
                "MyFhirSdk.Tests.ImplementationGuides.TwCore.",
                "MyFhirSdk.Tests.Architecture.D3.NeutralSdkTests.")
    for path in sorted(trx_paths):
        root = ET.parse(path).getroot()
        definitions = {}
        for test in root.findall(".//{*}TestDefinitions/{*}UnitTest"):
            method = test.find("{*}TestMethod")
            test_id = test.get("id")
            if test_id in definitions:
                raise ValueError("Duplicate TRX test definition: " + str(test_id))
            definitions[test_id] = Path(method.get("codeBase", "").replace("\\", "/")).stem if method is not None else ""
        for result in root.findall(".//{*}UnitTestResult"):
            name = result.get("testName", "")
            if name in required:
                observed[name].append({"assembly": definitions.get(result.get("testId"), ""),
                                       "outcome": result.get("outcome"), "trx": path.name})
            elif name.startswith(prefixes):
                errors.append("Unreviewed D3 test identity: " + name)
    cases = []
    for name, assembly in sorted(required.items()):
        results = observed[name]
        passed = len(results) == 1 and results[0]["assembly"] == assembly and results[0]["outcome"] == "Passed"
        cases.append({"testName": name, "expectedAssembly": assembly, "passed": passed, "results": results})
        if not passed:
            errors.append("Required D3 case must pass exactly once in " + assembly + ": " + name)
    return {"expected": len(required), "passed": sum(case["passed"] for case in cases), "cases": cases, "errors": errors}


def stable_drift(pinned, current):
    def canonical(data):
        try:
            return d0.text_bytes(data) if b"\0" not in data else data
        except UnicodeDecodeError:
            return data
    paths = {p for p in pinned.keys() | current.keys()
             if p.startswith(STABLE_ROOTS) or Path(p).suffix.lower() in (".props", ".targets")}
    return sorted(p for p in paths if p not in pinned or p not in current or
                  canonical(pinned[p]) != canonical(current[p]))


def delta(before, after):
    old, new = set(before.splitlines()), set(after.splitlines())
    return {"added": sorted(new - old), "removed": sorted(old - new)}


def validate_project_graph(sdk, ig):
    def refs(items):
        return [Path(r["FullPath"]).resolve() for r in items.get("ProjectReference", [])]
    expected_sdk = [ROOT / p for p in ("Runtime/MyFhirSdk.Runtime.csproj", "ModelContracts/MyFhirSdk.ModelContracts.csproj",
                    "ModelSupport/MyFhirSdk.ModelSupport.csproj")]
    expected_ig = [ROOT / p for p in ("MyFhirSdk.csproj", "Models/R4/MyFhirSdk.R4.Models.csproj")]
    if (sorted(refs(sdk)) != sorted(p.resolve() for p in expected_sdk)
            or sdk.get("Reference") or sdk.get("PackageReference")):
        raise ValueError("D3 SDK project dependencies changed")
    if sorted(refs(ig)) != sorted(p.resolve() for p in expected_ig) or ig.get("Reference") or ig.get("PackageReference"):
        raise ValueError("D3 IG must reference only SDK and R4 Models directly")
    compile_items = sdk.get("Compile", [])
    if not compile_items or any(Path(c["FullPath"]).resolve().is_relative_to((ROOT / "ImplementationGuides").resolve())
                                for c in compile_items):
        raise ValueError("SDK compilation contains IG sources or is empty")


REMOVED_TYPES = (
    "MyFhirSdk.ModelMetadata.R5.R5ModelContext", "MyFhirSdk.Client.FhirClient",
    "MyFhirSdk.Client.Abstractions.IFhirClient", "MyFhirSdk.Client.Responses.FhirResponseHandler",
    "MyFhirSdk.Client.Abstractions.IFhirResponseHandler")
ENGINES = ("MyFhirSdk.Serialization.Json.FhirJsonParser", "MyFhirSdk.Serialization.Json.FhirJsonSerializer",
           "MyFhirSdk.Validation.FhirValidator", "MyFhirSdk.Client.Requests.FhirResourceTypeResolver")


def expected_apis():
    old = ROOT / "docs/sdk_decoupling/baselines/d2"
    lines = (old / "full-sdk-api.txt").read_text(encoding="utf-8").splitlines()
    def removed(line):
        if any(line.startswith(("TYPE [MyFhirSdk]" + name + " ", "[MyFhirSdk]" + name + " ")) for name in REMOVED_TYPES):
            return True
        if any(line.startswith("[MyFhirSdk]" + name + " METHOD ") and line.endswith(" .ctor()") for name in ENGINES):
            return True
        return (line.startswith("[MyFhirSdk]MyFhirSdk.Validation.Profiles.ProfileValidator METHOD ") and
                ".ctor(None:[MyFhirSdk]MyFhirSdk.Validation.IFhirValidator " in line)
    sdk = "\n".join(line for line in lines if not removed(line)) + "\n"
    aggregate = []
    current_type = ""
    for line in (old / "ApprovedPublicApi.txt").read_text(encoding="utf-8").splitlines():
        if line.startswith("TYPE "):
            current_type = line.split(" | ")[0].split()[-1]
        if current_type in REMOVED_TYPES:
            continue
        if current_type in ENGINES and line == "  CONSTRUCTOR Void .ctor()":
            continue
        if current_type == "MyFhirSdk.Validation.Profiles.ProfileValidator" and "Void .ctor(MyFhirSdk.Validation.IFhirValidator," in line:
            continue
        aggregate.append(line)
    return sdk, (old / "twcore-api.txt").read_text(encoding="utf-8"), "\n".join(aggregate) + "\n"


def validate_api(sdk, ig, aggregate, record=False):
    old = ROOT / "docs/sdk_decoupling/baselines/d2"
    if (sdk, ig, aggregate) != expected_apis():
        raise ValueError("D3 API change exceeds the reviewed defaults and legacy Client removals")
    expected = {"schemaVersion": 1, "fromRevision": D2_REVISION,
                "fullSdk": delta((old / "full-sdk-api.txt").read_text(encoding="utf-8"), sdk),
                "independentIg": delta((old / "twcore-api.txt").read_text(encoding="utf-8"), ig),
                "aggregate": delta((old / "ApprovedPublicApi.txt").read_text(encoding="utf-8"), aggregate),
                "removedTypes": list(REMOVED_TYPES), "removedParameterlessConstructors": list(ENGINES),
                "removedLegacyProfileConstructors": 2}
    files = {"full-sdk-api.txt": sdk, "twcore-api.txt": ig, "ApprovedPublicApi.txt": aggregate,
             "api-delta.json": json.dumps(expected, ensure_ascii=False, indent=2) + "\n"}
    if record:
        if any((BASELINE / p).exists() for p in files):
            raise ValueError("D3 API evidence already exists; refusing to overwrite")
        BASELINE.mkdir(parents=True, exist_ok=True)
        for name, text in files.items():
            (BASELINE / name).write_text(text, encoding="utf-8", newline="\n")
    for name, text in files.items():
        if (BASELINE / name).read_text(encoding="utf-8") != text:
            raise ValueError("Unapproved D3 API drift: " + name)
    return expected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--record-api", action="store_true", help="Record the reviewed D3 API once")
    args = parser.parse_args()
    output = args.output.resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output == artifacts or not output.is_relative_to(artifacts) or output.exists():
        parser.error("Use a fresh directory strictly below artifacts/")
    output.mkdir(parents=True)
    summary = {"gate": "D3", "status": "Pending", "startedAtUtc": datetime.now(timezone.utc).isoformat(),
               "baselineRevision": d0.PIN, "historicalCiRevision": D0_REVISION, "d1Revision": D1_REVISION, "fullyDecoupled": False}
    d0.write(output / "summary.json", summary)
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_NOLOGO="1")

    def run(args, log):
        result = subprocess.run(args, cwd=ROOT, env=env, capture_output=True, encoding="utf-8", errors="replace")
        (output / log).write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode:
            raise RuntimeError(f"Command failed ({result.returncode}); see {log}")
        return result.stdout.strip()

    try:
        sources = d0.current_sources()
        fingerprint = d0.source_fingerprint(sources)
        summary.update(sourceRevision=d0.git("rev-parse", "HEAD").decode().strip(),
                       dirty=bool(d0.git("status", "--porcelain")), sourceFingerprint=fingerprint)
        d0.write(output / "source-inventory.json", d0.inventory(sources))
        # D0 artifacts are immutable; pair historical CI with its accepted checkout.
        names = d0.git("ls-tree", "-r", "--name-only", D0_REVISION,
                       "docs/sdk_decoupling/baselines/d0").decode().splitlines()
        current_names = sorted(p.relative_to(ROOT).as_posix() for p in d0.BASELINE.rglob("*") if p.is_file())
        if sorted(names) != current_names:
            raise ValueError("D0 evidence file set changed")
        for name in names:
            if d0.text_bytes((ROOT / name).read_bytes()) != d0.text_bytes(d0.git("show", f"{D0_REVISION}:{name}")):
                raise ValueError("Historical D0 evidence changed: " + name)
        d1_names = d0.git("ls-tree", "-r", "--name-only", D1_REVISION,
                          "docs/sdk_decoupling/baselines/d1").decode().splitlines()
        actual_d1 = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / "docs/sdk_decoupling/baselines/d1").rglob("*") if p.is_file())
        if sorted(d1_names) != actual_d1 or any(d0.text_bytes((ROOT / p).read_bytes()) !=
                d0.text_bytes(d0.git("show", f"{D1_REVISION}:{p}")) for p in d1_names):
            raise ValueError("Historical D1 evidence changed")
        d2_names = d0.git("ls-tree", "-r", "--name-only", D2_REVISION,
                          "docs/sdk_decoupling/baselines/d2").decode().splitlines()
        actual_d2 = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / "docs/sdk_decoupling/baselines/d2").rglob("*") if p.is_file())
        if sorted(d2_names) != actual_d2 or any(d0.text_bytes((ROOT / p).read_bytes()) !=
                d0.text_bytes(d0.git("show", f"{D2_REVISION}:{p}")) for p in d2_names):
            raise ValueError("Historical D2 evidence changed")
        pinned = d0.pinned_sources()
        drift = stable_drift(pinned, sources)
        frozen = json.loads((d0.BASELINE / "inventory.json").read_text(encoding="utf-8"))
        contract_drift = [p for p in d0.protected_drift(frozen, sources, pinned)
                          if p not in ("Tests/Architecture/ApprovedPublicApi.txt", "Tests/FhirVersions/V6/ApprovedClientApi.txt")]
        summary.update(stableSourceDrift=drift, protectedContractDrift=contract_drift)
        if drift or contract_drift:
            raise ValueError("Models, Runtime, generator or protected contracts changed")
        sdk_version = run(["dotnet", "--version"], "sdk-version.log")
        if sdk_version != json.loads(sources["global.json"])["sdk"]["version"]:
            raise ValueError("Pinned SDK mismatch")
        summary["sdkVersion"] = sdk_version
        powershell = os.getenv("MYFHIRSDK_TEST_PWSH", "pwsh")
        summary["powershellVersion"] = run([powershell, "-NoProfile", "-Command",
            "$PSVersionTable.PSVersion.ToString()"], "powershell-version.log")
        if tuple(map(int, summary["powershellVersion"].split(".")[:2])) < (7, 2):
            raise ValueError("PowerShell 7.2+ is required for existing packaging regression")
        run(["dotnet", "build", "MyFhirSdk.sln", "-c", "Release", "--no-restore"], "build.log")
        run(["dotnet", "build", "Tests/KernelMigration/Inventory/Inventory.csproj", "-c", "Release",
             "-p:NuGetAudit=false"], "inventory-build.log")
        graphs = {}
        for name, project in (("sdk", "MyFhirSdk.csproj"),
                              ("ig", "ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj")):
            graphs[name] = json.loads(run(["dotnet", "msbuild", project, "-nologo", "-p:Configuration=Release",
                "-getItem:Compile,ProjectReference,Reference,PackageReference"], name + "-project-graph.log"))["Items"]
        d0.write(output / "project-graph.json", graphs)
        validate_project_graph(graphs["sdk"], graphs["ig"])
        binary = ROOT / "bin/Release/net9.0"
        run(["dotnet", str(ROOT / "Tests/KernelMigration/Inventory/bin/Release/net9.0/Inventory.dll"),
             str(binary / "MyFhirSdk.dll"), str(output / "all-assemblies-api.txt"),
             *[str(binary / (n + ".dll")) for n in
               ("MyFhirSdk.Runtime", "MyFhirSdk.ModelContracts", "MyFhirSdk.ModelSupport")],
             str(ROOT / "ImplementationGuides/TwCore/bin/Release/net9.0/MyFhirSdk.ImplementationGuides.TwCore.dll"),
             str(ROOT / "Models/R4/bin/Release/net9.0/MyFhirSdk.R4.Models.dll"),
             str(ROOT / "Models/R5/bin/Release/net9.0/MyFhirSdk.R5.Models.dll")], "inventory.log")
        sdk_api = "\n".join(s for s in (output / "all-assemblies-api.txt").read_text(encoding="utf-8").splitlines()
                            if s.startswith(("ASSEMBLY MyFhirSdk,", "TYPE [MyFhirSdk]", "[MyFhirSdk]"))) + "\n"
        aggregate = sources["Tests/Architecture/ApprovedPublicApi.txt"].decode("utf-8").replace("\r\n", "\n")
        ig_api = "\n".join(s for s in (output / "all-assemblies-api.txt").read_text(encoding="utf-8").splitlines()
                            if s.startswith(("ASSEMBLY MyFhirSdk.ImplementationGuides.TwCore,",
                                             "TYPE [MyFhirSdk.ImplementationGuides.TwCore]", "[MyFhirSdk.ImplementationGuides.TwCore]"))) + "\n"
        summary["apiDelta"] = validate_api(sdk_api, ig_api, aggregate, args.record_api)
        (output / "twcore-api.txt").write_text(ig_api, encoding="utf-8", newline="\n")
        (output / "full-sdk-api.txt").write_text(sdk_api, encoding="utf-8", newline="\n")
        reference = ROOT / "Runtime/obj/Release/net9.0/ref/MyFhirSdk.Runtime.dll"
        descriptor = json.loads(sources["CodeGen/Policy/runtime-contract.json"])
        summary["compilerReferenceSha256"] = d0.sha(reference.read_bytes())
        if summary["compilerReferenceSha256"] != descriptor["compilerReference"]["sha256"]:
            raise ValueError("Actual Runtime compiler reference differs from the pinned contract")
        error = None
        try:
            run([powershell, "-NoProfile", "-File", "eng/Test-KernelConsumerRebuildHarness.ps1",
                 "-OutputDirectory", str(output / "k3-harness")], "k3-harness.log")
            run([powershell, "-NoProfile", "-File", "eng/Test-KernelConsumerRebuild.ps1",
                 "-OutputDirectory", str(output / "k3")], "regression.log")
        except RuntimeError as failure:
            error = failure
        if error and not (output / "k3/evidence.json").exists():
            raise error
        k3 = json.loads((output / "k3/evidence.json").read_text(encoding="utf-8-sig"))
        summary["k3Status"] = k3["status"]
        if k3["status"] != "passed" or any(v != "passed" for v in k3["gates"].values()):
            raise ValueError("Fresh K3 classification, regression or deployment failed")
        projects = json.loads((output / "k3/consumer-inventory.json").read_text(encoding="utf-8-sig"))["projects"]
        if not any(p["source"] == "ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj"
                   and p["role"] == "production-r4-ig" for p in projects):
            raise ValueError("Independent IG is missing from K3 classification")
        reports = regression.regression_inventory(output / "k3/test-results")
        v1.assert_allowed_regression_skips(reports, os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", ""))
        d0.write(output / "regression.json", reports)
        summary["testCounts"] = {k: sum(r[k] for r in reports) for k in ("total", "passed", "failed", "skipped")}
        required = required_test_evidence((output / "k3/test-results").glob("*.trx"))
        d0.write(output / "d3-required-tests.json", required)
        summary["requiredD3TestCounts"] = {k: required[k] for k in ("expected", "passed")}
        if required["errors"]:
            raise ValueError("Required D3 regression coverage failed; see d3-required-tests.json")
        if error or summary["testCounts"]["failed"]:
            raise RuntimeError("D3 regression failed; see regression.log and TRX")
        if d0.source_fingerprint(d0.current_sources()) != fingerprint:
            raise ValueError("Source changed during D3 acceptance; rerun from a stable checkout")
        run([sys.executable, "eng/Test-SdkDecouplingDeployment.py", "--output", str(output / "deployment")], "deployment.log")
        if d0.source_fingerprint(d0.current_sources()) != fingerprint:
            raise ValueError("Source changed during deployment verification")
        summary.update(status="Passed", fullyDecoupled=True)
    except Exception as error:
        summary.update(status="Failed", error=str(error))
    d0.write(output / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if summary["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
