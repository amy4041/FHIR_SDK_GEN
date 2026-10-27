#!/usr/bin/env python3
"""Current delivery acceptance: neutral SDK, canonical tool, fresh integration and whole-source rollback."""
import argparse
from contextlib import redirect_stdout
from datetime import datetime, timezone
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "eng"))
import FhirDelivery as delivery

D3_REVISION = "d47374bf99e91a61848fccf7612e2d571ae4bf67"
spec = importlib.util.spec_from_file_location("d4_d3", ROOT / "eng/Test-SdkDecouplingD3.py")
d3 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d3)
ORIGINAL_STABLE_DRIFT = d3.stable_drift
FRESH_PROJECTS = {"MyFhirSdk.FhirVersions.V5.Tests": "Tests/FhirVersions/V5/MyFhirSdk.FhirVersions.V5.Tests.csproj",
                  "MyFhirSdk.FhirVersions.V6.Tests": "Tests/FhirVersions/V6/MyFhirSdk.FhirVersions.V6.Tests.csproj",
                  "MyFhirSdk.ImplementationGuides.TwCore.Tests": "Tests/ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.Tests.csproj"}


def stable_drift(pinned, current):
    # D4 explicitly migrates this external template, never shared/product build properties.
    return [p for p in ORIGINAL_STABLE_DRIFT(pinned, current) if p != "examples/external/SdkReferences.props"]


def cases(paths):
    result = {}
    for path in paths:
        root = ET.parse(path).getroot()
        definitions = {}
        for unit in root.findall(".//{*}TestDefinitions/{*}UnitTest"):
            method = unit.find("{*}TestMethod")
            definitions[unit.get("id")] = Path(method.get("codeBase").replace("\\", "/")).stem
        for test in root.findall(".//{*}UnitTestResult"):
            key = definitions[test.get("testId")] + ":" + test.get("testName")
            # Some valid xUnit theories have identical display names (e.g. empty primitives).
            # Preserve their multiplicity rather than collapsing or rejecting those cases.
            result.setdefault(key, []).append(test.get("outcome"))
    return result


def assert_fresh_cases(expected, actual):
    selected = {k: v for k, v in expected.items() if k.split(":", 1)[0] in FRESH_PROJECTS}
    if (not selected or set(selected) != set(actual)
            or any(sorted(selected[k]) != sorted(actual[k]) for k in selected)
            or any(v != "Passed" for outcomes in actual.values() for v in outcomes)):
        raise ValueError("Fresh generated integration must pass the exact current V5/V6/TW Core cases")
    return sum(len(values) for values in actual.values())


def assert_historical_evidence():
    prefix = "docs/sdk_decoupling/baselines/d3"
    expected = d3.d0.git("ls-tree", "-r", "--name-only", D3_REVISION, prefix).decode().splitlines()
    actual = sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / prefix).rglob("*") if p.is_file())
    if sorted(expected) != actual or any(d3.d0.text_bytes((ROOT / p).read_bytes()) != d3.d0.text_bytes(
            d3.d0.git("show", D3_REVISION + ":" + p)) for p in expected):
        raise ValueError("Historical D3 evidence changed")
    baseline = ROOT / "docs/sdk_decoupling/baselines/d4"
    for name in ("full-sdk-api.txt", "twcore-api.txt", "ApprovedPublicApi.txt"):
        if (baseline / name).read_bytes() != (ROOT / prefix / name).read_bytes():
            raise ValueError("D4 must preserve the accepted D3 API: " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tool-package", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists() or output == ROOT / "artifacts" or not output.is_relative_to((ROOT / "artifacts").resolve()):
        parser.error("Use a fresh directory strictly below artifacts/")
    output.mkdir(parents=True)
    package = args.tool_package.resolve()
    summary = {"schemaVersion": 1, "gate": "D4-platform", "status": "Pending", "os": platform.system(),
               "sourceRevision": d3.d0.git("rev-parse", "HEAD").decode().strip(), "rollbackRevision": D3_REVISION,
               "createdAtUtc": datetime.now(timezone.utc).isoformat(), "crossPlatformAcceptance": "Pending",
               "externalServerAcceptance": "Pending", "publicNuGetRelease": "OutOfScope"}
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_NOLOGO="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1")
    # No inherited endpoint may turn a delivery or rollback check into remote writes.
    for name in list(env):
        if name.startswith("MYFHIRSDK_") and name != "MYFHIRSDK_TEST_PWSH":
            env.pop(name)

    def run(name, arguments, cwd=ROOT, environment=None):
        result = subprocess.run(arguments, cwd=cwd, env=environment or env, capture_output=True, encoding="utf-8", errors="replace", timeout=1800)
        (output / (name + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode:
            raise ValueError("D4 command failed: " + name)
        return result.stdout.strip()

    try:
        assert_historical_evidence()
        sources = d3.d0.current_sources()
        fingerprint = d3.d0.source_fingerprint(sources)
        summary["sourceFingerprint"] = fingerprint
        d3.d0.write(output / "source-inventory.json", d3.d0.inventory(sources))
        summary["sdkVersion"] = run("sdk-version", ["dotnet", "--version"])
        descriptor = json.loads(sources["CodeGen/Policy/runtime-contract.json"])
        delivery.validate_tool(package, descriptor)
        summary["toolPackageSha256"] = delivery.sha(package)
        summary["compilerReferenceSha256"] = descriptor["compilerReference"]["sha256"]
        shutil.copy2(package, output / "canonical-tool.nupkg")
        # Reuse reviewed D3 checks with one explicit D4 template exception. Historical runner is unchanged.
        old_args, old_drift, old_env = sys.argv, d3.stable_drift, dict(os.environ)
        try:
            os.environ.clear()
            os.environ.update(env)
            d3.stable_drift = stable_drift
            sys.argv = ["Test-SdkDecouplingD3.py", "--output", str(output / "neutral-sdk")]
            with (output / "neutral-sdk.log").open("w", encoding="utf-8") as log, redirect_stdout(log):
                if d3.main():
                    raise ValueError("Current neutral SDK regression failed")
        finally:
            sys.argv, d3.stable_drift = old_args, old_drift
            os.environ.clear()
            os.environ.update(old_env)
        neutral = json.loads((output / "neutral-sdk/summary.json").read_text(encoding="utf-8"))
        summary["testCounts"] = neutral["testCounts"]
        run("export", [sys.executable, "eng/Export-FhirDelivery.py", "--tool-package", str(package), "--output", str(output / "bundle")])
        run("external", [sys.executable, "eng/Test-FhirDelivery.py", "--bundle", str(output / "bundle"), "--output", str(output / "external")])
        manifest = json.loads((output / "bundle/delivery-manifest.json").read_text(encoding="utf-8"))
        delivery.validate_manifest(output / "bundle", manifest)
        if manifest["toolPackageSha256"] != summary["toolPackageSha256"]:
            raise ValueError("Delivery used another tool package")
        summary["deliveryManifestSha256"] = delivery.sha(output / "bundle/delivery-manifest.json")
        summary["deliverySourceFingerprint"] = manifest["sourceFingerprint"]
        expected_cases = cases((output / "neutral-sdk/k3/test-results").glob("*.trx"))
        with tempfile.TemporaryDirectory(prefix="myfhirsdk-d4-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT):
                raise ValueError("Fresh integration/rollback must run outside checkout")
            stage = work / "source"
            for name, data in sources.items():
                if name.startswith(("Generated/R4/", "Generated/R5/")):
                    continue
                path = delivery.safe_path(stage, name)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            if any(stage.rglob("bin")) or any(stage.rglob("obj")):
                raise ValueError("Fresh stage contains previous build outputs")
            for release in ("R4", "R5"):
                shutil.copytree(output / "external/generated" / release, stage / "Generated" / release)
            before = delivery.inventory(stage / "Generated")
            fresh_env = dict(env, NUGET_PACKAGES=str(work / "packages"), DOTNET_CLI_HOME=str(work / "cli-home"),
                             NUGET_HTTP_CACHE_PATH=str(work / "http-cache"))
            fresh_results = output / "fresh-test-results"
            for assembly, project in FRESH_PROJECTS.items():
                run(assembly, ["dotnet", "test", project, "-c", "Release", "--logger", "trx;LogFileName=" + assembly + ".trx",
                     "--results-directory", str(fresh_results), "-p:NuGetAudit=false"], stage, fresh_env)
            fresh = cases(fresh_results.glob("*.trx"))
            summary["freshIntegrationTests"] = assert_fresh_cases(expected_cases, fresh)
            if delivery.inventory(stage / "Generated") != before:
                raise ValueError("Fresh integration changed generated inputs")
            summary["freshIntegration"] = {"outsideCheckout": True, "freshCache": True, "generatedByInstalledTool": True,
                                           "reusedBinObj": False, "generatedInputsUnchanged": True}
            restored = work / "rollback"
            restored.mkdir()
            archive_bytes = subprocess.check_output(["git", "-c", "core.autocrlf=false", "-c", "core.eol=lf",
                                                     "archive", "--format=tar", D3_REVISION], cwd=ROOT)
            with tarfile.open(fileobj=io.BytesIO(archive_bytes)) as archive:
                for member in archive:
                    if not member.isfile():
                        continue
                    path = delivery.safe_path(restored, member.name)
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(archive.extractfile(member).read())
            restored_files = delivery.inventory(restored)
            d3.d0.write(output / "rollback-source-hashes.json", restored_files)
            rollback_env = dict(fresh_env, NUGET_PACKAGES=str(work / "rollback-packages"))
            run("rollback", ["dotnet", "test", "MyFhirSdk.sln", "-c", "Release", "--logger", "trx;LogFilePrefix=d3-rollback",
                 "--results-directory", str(output / "rollback-test-results"), "-p:NuGetAudit=false"], restored, rollback_env)
            reports = d3.regression.regression_inventory(output / "rollback-test-results")
            d3.v1.assert_allowed_regression_skips(reports, "")
            if any(r["failed"] for r in reports) or any(delivery.sha(restored / p) != digest for p, digest in restored_files.items()):
                raise ValueError("Rollback regression or original source bytes changed")
            run("rollback-deployment", [sys.executable, "eng/Test-SdkDecouplingDeployment.py", "--output",
                str(restored / "artifacts/d4-rollback-deployment")], restored, rollback_env)
            shutil.copytree(restored / "artifacts/d4-rollback-deployment", output / "rollback-deployment")
            summary["rollback"] = {"status": "Passed", "sourceRevision": D3_REVISION, "sourceFiles": len(restored_files),
                "archiveSha256": hashlib.sha256(archive_bytes).hexdigest(), "restoredInputsUnchanged": True,
                "testCounts": {k: sum(r[k] for r in reports) for k in ("total", "passed", "failed", "skipped")}}
            required = d3.required_test_evidence((output / "rollback-test-results").glob("*.trx"))
            if required["errors"]:
                raise ValueError("Rollback lost accepted D3 regression cases")
            d3.d0.write(output / "rollback-required-tests.json", required)
        if d3.d0.source_fingerprint(d3.d0.current_sources()) != fingerprint:
            raise ValueError("Source changed during D4 acceptance")
        assert_historical_evidence()
        summary["status"] = "Passed"
    except Exception as error:
        summary.update(status="Failed", error=str(error))
    d3.d0.write(output / "summary.json", summary)
    d3.d0.write(output / "evidence-files.json", delivery.inventory(output))
    print(json.dumps(summary, indent=2))
    return 0 if summary["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
