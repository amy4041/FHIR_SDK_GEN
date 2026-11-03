#!/usr/bin/env python3
"""D1 current-source gate: exact API delta, invariant sources and fresh regression.

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
BASELINE = ROOT / "docs/sdk_decoupling/baselines/d1"
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
                   "OnlyTheTwoLegacyCompositionOverloadsAreObsolete",
                   "InternalRuleOnlyValidatorCannotPretendToHaveAR5Context"):
        cases[architecture + method] = "MyFhirSdk.Architecture.Tests"
    return cases


def required_test_evidence(trx_paths):
    required = required_tests()
    observed = {name: [] for name in required}
    errors = []
    prefixes = ("MyFhirSdk.Tests.Validation.Profiles.ContextualProfileValidatorTests.",
                "MyFhirSdk.Tests.Architecture.D1.ContextualValidatorContractTests.")
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
                errors.append("Unreviewed D1 test identity: " + name)
    cases = []
    for name, assembly in sorted(required.items()):
        results = observed[name]
        passed = len(results) == 1 and results[0]["assembly"] == assembly and results[0]["outcome"] == "Passed"
        cases.append({"testName": name, "expectedAssembly": assembly, "passed": passed, "results": results})
        if not passed:
            errors.append("Required D1 case must pass exactly once in " + assembly + ": " + name)
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


def validate_api(sdk, aggregate, record=False):
    old = d0.BASELINE
    expected = {"schemaVersion": 1, "fromRevision": d0.PIN,
                "fullSdk": delta((old / "full-sdk-api.txt").read_text(encoding="utf-8"), sdk),
                "aggregate": delta((old / "snapshots/Tests/Architecture/ApprovedPublicApi.txt").read_text(encoding="utf-8"), aggregate)}
    files = {"full-sdk-api.txt": sdk, "ApprovedPublicApi.txt": aggregate,
             "api-delta.json": json.dumps(expected, ensure_ascii=False, indent=2) + "\n"}
    if record:
        if any((BASELINE / p).exists() for p in files):
            raise ValueError("D1 API evidence already exists; refusing to overwrite")
        BASELINE.mkdir(parents=True, exist_ok=True)
        for name, text in files.items():
            (BASELINE / name).write_text(text, encoding="utf-8", newline="\n")
    for name, text in files.items():
        if (BASELINE / name).read_text(encoding="utf-8") != text:
            raise ValueError("Unapproved D1 API drift: " + name)
    return expected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--record-api", action="store_true", help="Record the reviewed D1 API once")
    args = parser.parse_args()
    output = args.output.resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output == artifacts or not output.is_relative_to(artifacts) or output.exists():
        parser.error("Use a fresh directory strictly below artifacts/")
    output.mkdir(parents=True)
    summary = {"gate": "D1", "status": "Pending", "startedAtUtc": datetime.now(timezone.utc).isoformat(),
               "baselineRevision": d0.PIN, "historicalCiRevision": D0_REVISION, "fullyDecoupled": False}
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
        pinned = d0.pinned_sources()
        drift = stable_drift(pinned, sources)
        frozen = json.loads((d0.BASELINE / "inventory.json").read_text(encoding="utf-8"))
        contract_drift = [p for p in d0.protected_drift(frozen, sources, pinned)
                          if p != "Tests/Architecture/ApprovedPublicApi.txt"]
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
        binary = ROOT / "bin/Release/net9.0"
        run(["dotnet", str(ROOT / "Tests/KernelMigration/Inventory/bin/Release/net9.0/Inventory.dll"),
             str(binary / "MyFhirSdk.dll"), str(output / "all-assemblies-api.txt"),
             *[str(binary / (n + ".dll")) for n in
               ("MyFhirSdk.Runtime", "MyFhirSdk.ModelContracts", "MyFhirSdk.ModelSupport", "MyFhirSdk.R5.Models")]], "inventory.log")
        sdk_api = "\n".join(s for s in (output / "all-assemblies-api.txt").read_text(encoding="utf-8").splitlines()
                            if s.startswith(("ASSEMBLY MyFhirSdk,", "TYPE [MyFhirSdk]", "[MyFhirSdk]"))) + "\n"
        aggregate = sources["Tests/Architecture/ApprovedPublicApi.txt"].decode("utf-8").replace("\r\n", "\n")
        summary["apiDelta"] = validate_api(sdk_api, aggregate, args.record_api)
        (output / "full-sdk-api.txt").write_text(sdk_api, encoding="utf-8", newline="\n")
        reference = ROOT / "Runtime/obj/Release/net9.0/ref/MyFhirSdk.Runtime.dll"
        descriptor = json.loads(sources["CodeGen/Policy/runtime-contract.json"])
        summary["compilerReferenceSha256"] = d0.sha(reference.read_bytes())
        if summary["compilerReferenceSha256"] != descriptor["compilerReference"]["sha256"]:
            raise ValueError("Actual Runtime compiler reference differs from the pinned contract")
        error = None
        try:
            run(["dotnet", "test", "MyFhirSdk.sln", "-c", "Release", "--no-build", "--no-restore",
                 "--logger", "trx", "--results-directory", str(output / "regression")], "regression.log")
        except RuntimeError as failure:
            error = failure
        reports = regression.regression_inventory(output / "regression")
        v1.assert_allowed_regression_skips(reports, os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", ""))
        d0.write(output / "regression.json", reports)
        summary["testCounts"] = {k: sum(r[k] for r in reports) for k in ("total", "passed", "failed", "skipped")}
        required = required_test_evidence((output / "regression").glob("*.trx"))
        d0.write(output / "d1-required-tests.json", required)
        summary["requiredD1TestCounts"] = {k: required[k] for k in ("expected", "passed")}
        if required["errors"]:
            raise ValueError("Required D1 regression coverage failed; see d1-required-tests.json")
        if error or summary["testCounts"]["failed"]:
            raise RuntimeError("D1 regression failed; see regression.log and TRX")
        if d0.source_fingerprint(d0.current_sources()) != fingerprint:
            raise ValueError("Source changed during D1 acceptance; rerun from a stable checkout")
        summary["status"] = "Passed"
    except Exception as error:
        summary.update(status="Failed", error=str(error))
    d0.write(output / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if summary["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
