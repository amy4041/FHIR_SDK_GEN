#!/usr/bin/env python3
"""G7 platform leg: canonical installed tool, fresh integration/deployment and V6 source rollback.

Cross-platform acceptance is a separate Compare-FhirV7Delivery.py invocation.
Requires restored solution, pinned .NET SDK, Python 3.13 and PowerShell 7 on PATH.
"""
import argparse
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
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "eng"))
spec = importlib.util.spec_from_file_location("g6", ROOT / "eng/Test-FhirV6Client.py")
g6 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g6)
baseline = g6.baseline
ROLLBACK_REVISION = "99017eff8c608aa1e34292ddaf10eab60912bbed"
SHARED_DLLS = {"MyFhirSdk.dll", "MyFhirSdk.Runtime.dll", "MyFhirSdk.ModelContracts.dll", "MyFhirSdk.ModelSupport.dll"}
CONSUMERS = {"R4": {"4.0.1"}, "R5": {"5.0.0"}, "Dual": {"4.0.1", "5.0.0"}}


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def inventory(root):
    return {p.relative_to(root).as_posix(): baseline.sha(p) for p in sorted(root.rglob("*")) if p.is_file()}


def archive_rollback_source():
    # git archive applies checkout conversion; do not inherit a runner's CRLF policy.
    return subprocess.check_output(["git", "-c", "core.autocrlf=false", "-c", "core.eol=lf",
        "archive", "--format=tar", ROLLBACK_REVISION], cwd=ROOT)


def portable_fingerprint(files):
    """Normalize UTF-8 text line endings only; keep raw per-platform evidence separately."""
    rows = []
    for row in files:
        data = (ROOT / row["path"]).read_bytes()
        try:
            if b"\0" not in data:
                data.decode("utf-8")
                data = data.replace(b"\r\n", b"\n")
        except UnicodeDecodeError:
            pass
        rows.append({"path": row["path"], "sha256": hashlib.sha256(data).hexdigest()})
    result = baseline.fingerprint(rows)
    result["algorithm"] = "sha256-path-and-utf8-lf-bytes-v1"
    return result


def isolated_environment(work):
    env = dict(os.environ)
    for key in ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS", "DOTNET_ROOT", "DOTNET_ROOT_X64"):
        env.pop(key, None)
    for key, folder in {"DOTNET_CLI_HOME": "cli-home", "NUGET_PACKAGES": "packages", "NUGET_HTTP_CACHE_PATH": "http-cache",
                        "NUGET_PLUGINS_CACHE_PATH": "plugins", "NUGET_SCRATCH": "scratch"}.items():
        env[key] = str(work / folder)
    env.update(DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1",
               DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_NOLOGO="1")
    return env


def check_deployment(result, release, directory):
    if result.get("status") != "Passed" or {r["fhirVersion"] for r in result["releases"]} != CONSUMERS[release]:
        raise ValueError("Deployment release/result mismatch")
    for row in result["releases"]:
        expected = "MyFhirSdk.R4.Models" if row["fhirVersion"] == "4.0.1" else "MyFhirSdk.R5.Models"
        if row["modelAssembly"] != expected or not all(row[k] is True for k in ("roundTrip", "validation", "typedClient")):
            raise ValueError("Deployment did not exercise its version-owned engines/client")
    expected = SHARED_DLLS | {"MyFhirSdk.R4.Models.dll" if v == "4.0.1" else "MyFhirSdk.R5.Models.dll" for v in CONSUMERS[release]}
    rows = result["assemblies"]
    if not expected <= {r["name"] + ".dll" for r in rows}:
        raise ValueError("Missing executed implementation assemblies")
    for row in rows:
        path = Path(row["path"]).resolve()
        if path.parent != directory.resolve() or path.name != row["name"] + ".dll" or not path.is_file():
            raise ValueError("Deployment resolved an implementation outside its own directory")
    return expected


def validate_prerequisite(directory, fingerprint):
    summary = load(directory / "summary.json")
    if summary.get("status") != "Passed" or summary.get("gate") != "G6" or summary["sourceFingerprint"] != fingerprint:
        raise ValueError("G6 prerequisite is failed or belongs to another source fingerprint")
    reports = baseline.regression_inventory(directory / "sdk/generator/migration/context/test-results")
    g6.g5.g4.g3.v2.v1.assert_allowed_regression_skips(reports, os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", ""))
    counts = {key: sum(r[key] for r in reports) for key in ("total", "passed", "failed", "skipped")}
    if counts != summary["testCounts"] or counts["failed"] or len(g6.test_cases(directory / "fresh-test-results")) < 92:
        raise ValueError("G6 prerequisite reports do not match summary")
    return summary, reports


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tool-package", required=True, type=Path, help="Exact canonical nupkg shared by both OS legs")
    parser.add_argument("--output", default="artifacts/fhir-versions/v7/run")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if not output.is_relative_to((ROOT / "artifacts").resolve()) or output == (ROOT / "artifacts").resolve() or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    package = args.tool_package.resolve()
    if not package.is_file():
        parser.error("Canonical tool package does not exist")
    output.mkdir(parents=True)
    files = baseline.source_inventory()
    summary = {"schemaVersion": 1, "gate": "G7-platform", "status": "Pending", "crossPlatformAcceptance": "Pending",
        "externalServerAcceptance": "Pending", "publicNuGetRelease": "OutOfScope", "sourceRevision": baseline.run(["git", "rev-parse", "HEAD"]),
        "sourceFingerprint": baseline.fingerprint(files), "portableSourceFingerprint": portable_fingerprint(files),
        "os": platform.system(), "architecture": platform.machine(), "sdkVersion": baseline.run(["dotnet", "--version"]),
        "targetFramework": "net9.0", "configuration": "Release", "createdAtUtc": datetime.now(timezone.utc).isoformat()}
    baseline.write(output / "source-inventory.json", files)
    previous_ui = os.environ.get("DOTNET_CLI_UI_LANGUAGE")
    os.environ["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
    try:
        if summary["sdkVersion"] != load(ROOT / "global.json")["sdk"]["version"]:
            raise ValueError("Pinned SDK mismatch")
        tool_version = load(ROOT / "CodeGen/Policy/runtime-contract.json")["compatibility"]["toolVersion"]
        with zipfile.ZipFile(package) as archive:
            nuspec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith(".nuspec"))))
            if nuspec.find(".//{*}version").text != tool_version or nuspec.find(".//{*}id").text != "MyFhirSdk.CodeGen.Tool":
                raise ValueError("Canonical package identity mismatch")
            if any(Path(n).name in SHARED_DLLS - {"MyFhirSdk.Runtime.dll"} or Path(n).name.endswith(".Models.dll") for n in archive.namelist()):
                raise ValueError("Canonical tool embeds an SDK/model implementation")
        summary["toolVersion"] = tool_version
        summary["toolPackageSha256"] = baseline.sha(package)
        shutil.copy2(package, output / "canonical-tool.nupkg")
        sys.argv = ["Test-FhirV6Client.py", "--output", str(output / "prerequisite")]
        g6.main()
        prerequisite, reports = validate_prerequisite(output / "prerequisite", summary["sourceFingerprint"])
        summary.update(testCounts=prerequisite["testCounts"], externalServerAcceptance=prerequisite["externalServerAcceptance"],
                       runtimeContract=prerequisite["runtimeContract"], rollbackHarness=prerequisite["rollbackRegression"])
        baseline.write(output / "prerequisite-reports.json", reports)
        summary["prerequisiteSha256"] = baseline.sha(output / "prerequisite/summary.json")
        with tempfile.TemporaryDirectory(prefix="MyFhirSdk-V7-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT):
                raise ValueError("G7 workspace must be outside checkout")
            env = isolated_environment(work / "cache")

            def run(name, arguments, cwd, expected=0, timeout=360):
                result = subprocess.run(arguments, cwd=cwd, env=env, capture_output=True, text=True,
                    encoding="utf-8", errors="replace", timeout=timeout)
                log = (result.stdout + result.stderr).replace(str(work), "<isolated>").replace(str(ROOT), "<checkout>")
                (output / (name + ".log")).write_text(log, encoding="utf-8", newline="\n")
                if (expected == 0 and result.returncode != 0) or (expected != 0 and result.returncode == 0):
                    raise ValueError("G7 command failed: " + name)
                return result

            tool = work / "tool"
            tool.mkdir()
            shutil.copy2(ROOT / "global.json", tool / "global.json")
            feed = tool / "feed"
            feed.mkdir()
            shutil.copy2(package, feed / package.name)
            (tool / ".config").mkdir()
            shutil.copy2(ROOT / ".config/dotnet-tools.json", tool / ".config/dotnet-tools.json")
            config = ET.Element("configuration")
            sources = ET.SubElement(config, "packageSources")
            ET.SubElement(sources, "clear")
            ET.SubElement(sources, "add", {"key": "canonical-local-only", "value": str(feed)})
            ET.ElementTree(config).write(tool / "NuGet.Config", encoding="utf-8", xml_declaration=True)
            if Path(env["NUGET_PACKAGES"]).exists():
                raise ValueError("Tool cache was not empty")
            run("tool-restore", ["dotnet", "tool", "restore", "--configfile", str(tool / "NuGet.Config")], tool)
            installed = next(Path(env["NUGET_PACKAGES"]).rglob("CompilerSources/SimpleQuantity.cs")).parents[2]
            reference = next(installed.rglob("RuntimeReferences/net9.0/MyFhirSdk.Runtime.dll"))
            if baseline.sha(reference) != summary["runtimeContract"]["compilerReferenceSha256"]:
                raise ValueError("Canonical package compiler reference differs from accepted contract")
            summary["policies"] = {p.relative_to(ROOT).as_posix(): baseline.sha(p) for p in sorted((ROOT / "CodeGen/Policy").rglob("*.json"))}
            summary["generation"] = {}
            for release, version in (("R4", "4.0.1"), ("R5", "5.0.0")):
                lock = load(ROOT / f"CodeGen/Policy/{release.lower()}-package-lock.json")
                source = tool / (release + ".tgz")
                shutil.copy2(ROOT / lock["archivePath"], source)
                if baseline.sha(source) != lock["sha256"]:
                    raise ValueError("Locked FHIR package input mismatch")
                destination = tool / release
                command = ["dotnet", "myfhir-codegen", "--input", str(source), "--fhir-version", version,
                    "--package-id", lock["packageId"], "--package-version", version]
                for repeat in range(2):
                    run(f"{release}-models-{repeat}", [*command, "--mode", "model", "--output", str(destination)], tool)
                    root = destination / "Generated" / release
                    run(f"{release}-primitives-{repeat}", [*command, "--mode", "primitive", "--output", str(root / "Primitives")], tool)
                    current = inventory(root)
                    if repeat and current != first:
                        raise ValueError("Canonical repeated generation differs: " + release)
                    first = current
                if current != inventory(ROOT / "Generated" / release):
                    raise ValueError("Canonical generation differs from committed models: " + release)
                baseline.write(output / (release + "-generated-hashes.json"), current)
                shutil.copytree(root, output / "generated" / release)
                summary["generation"][release] = {"fhirVersion": version, "packageSha256": lock["sha256"],
                    "sourceCount": sum(p.endswith(".cs") for p in current), "fileCount": len(current),
                    "deterministic": True, "matchesCommittedModels": True, "hashesSha256": baseline.sha(output / (release + "-generated-hashes.json"))}
            summary["canonicalInstall"] = {"outsideCheckout": True, "emptyInitialCache": True, "localFeedOnly": True}

            stage = work / "source"
            for row in files:
                if row["path"].startswith(("Generated/R4/", "Generated/R5/")):
                    continue
                destination = stage / row["path"]
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(ROOT / row["path"], destination)
            if any(stage.rglob("bin")) or any(stage.rglob("obj")) or (stage / "Generated").exists():
                raise ValueError("Fresh stage reused build/generated outputs")
            for release in ("R4", "R5"):
                shutil.copytree(output / "generated" / release, stage / "Generated" / release)
            before = inventory(stage / "Generated")
            # The tool install's cache contains only the canonical tool. Integration restores test dependencies afresh.
            for version, runner in ((5, g6.g5), (6, g6)):
                results = output / f"fresh-v{version}-results"
                run(f"fresh-v{version}", ["dotnet", "test", f"Tests/FhirVersions/V{version}/MyFhirSdk.FhirVersions.V{version}.Tests.csproj",
                    "-c", "Release", "--logger", f"trx;LogFileName=v{version}.trx", "--results-directory", str(results)], stage, timeout=600)
                cases = runner.test_cases(results)
                baseline.write(output / f"v{version}-test-cases.json", cases)
                summary[f"freshV{version}Tests"] = len(cases)
            summary["deployments"] = {}
            negative_cases = []
            for release in CONSUMERS:
                assembly = release + "Consumer"
                publish = work / "publish" / release
                run(release + "-publish", ["dotnet", "publish", f"Tests/FhirVersions/V7/{release}/{assembly}.csproj",
                    "-c", "Release", "--self-contained", "false", "-p:UseAppHost=false", "-o", str(publish)], stage)
                for suffix in (".dll", ".deps.json", ".runtimeconfig.json"):
                    if not (publish / (assembly + suffix)).is_file():
                        raise ValueError("Incomplete publish output")
                deployed = work / "deployed" / release
                shutil.copytree(publish, deployed)
                result = run(release + "-deployment", ["dotnet", str(deployed / (assembly + ".dll"))], deployed)
                data = json.loads(result.stdout)
                required = check_deployment(data, release, deployed)
                for row in data["assemblies"]:
                    row["path"] = "<deployment>/" + Path(row["path"]).name
                baseline.write(output / (release + "-deployment.json"), data)
                hashes = inventory(deployed)
                baseline.write(output / (release + "-deployment-hashes.json"), hashes)
                shutil.copytree(deployed, output / "deployments" / release)
                summary["deployments"][release] = {"status": "Passed", "requiredDlls": sorted(required),
                    "ownDirectoryOnly": True, "files": len(hashes), "hashesSha256": baseline.sha(output / (release + "-deployment-hashes.json"))}
                for missing in sorted(required):
                    broken = work / "missing" / release / missing
                    shutil.copytree(deployed, broken)
                    (broken / missing).unlink()
                    result = run(release + "-missing-" + missing, ["dotnet", str(broken / (assembly + ".dll"))], broken, expected=1)
                    if Path(missing).stem not in result.stdout + result.stderr:
                        raise ValueError("Missing dependency failed without identifying the missing assembly")
                    negative_cases.append({"consumer": release, "missing": missing, "exitCode": result.returncode, "status": "Passed"})
            baseline.write(output / "missing-dependencies.json", negative_cases)
            summary["missingDependencyCases"] = len(negative_cases)
            if inventory(stage / "Generated") != before:
                raise ValueError("Build changed generated inputs")
            summary["freshIntegration"] = {"outsideCheckout": True, "reusedBinObj": False,
                "reusedCommittedGeneratedSources": False, "freshDependencyCache": True, "generatedInputsUnchanged": True}

            # Restore a whole pinned source tree; never reset/move the active branch or main.
            restored = work / "rollback"
            restored.mkdir()
            archive_bytes = archive_rollback_source()
            with tarfile.open(fileobj=io.BytesIO(archive_bytes)) as archive:
                for member in archive:
                    if not member.isfile():
                        continue
                    path = (restored / member.name).resolve()
                    if not path.is_relative_to(restored):
                        raise ValueError("Unsafe rollback archive path")
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(archive.extractfile(member).read())
            restored_files = inventory(restored)
            baseline.write(output / "rollback-source-hashes.json", restored_files)
            rollback_results = output / "rollback-test-results"
            run("rollback-regression", ["dotnet", "test", "MyFhirSdk.sln", "-c", "Release", "--logger", "trx;LogFilePrefix=rollback-v6",
                "--results-directory", str(rollback_results)], restored, timeout=900)
            rollback_reports = baseline.regression_inventory(rollback_results)
            g6.g5.g4.g3.v2.v1.assert_allowed_regression_skips(rollback_reports, os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", ""))
            if any(r["failed"] for r in rollback_reports):
                raise ValueError("Restored V6 source regression failed")
            for relative, digest in restored_files.items():
                if baseline.sha(restored / relative) != digest:
                    raise ValueError("Rollback source changed during rebuild")
            baseline.write(output / "rollback-regression.json", rollback_reports)
            summary["rollback"] = {"status": "Passed", "sourceRevision": ROLLBACK_REVISION, "archiveSha256": hashlib.sha256(archive_bytes).hexdigest(),
                "archiveNormalization": {"core.autocrlf": "false", "core.eol": "lf"},
                "sourceFiles": len(restored_files), "freshRebuild": True, "currentBranchChanged": False, "mainChanged": False,
                "testCounts": {key: sum(r[key] for r in rollback_reports) for key in ("total", "passed", "failed", "skipped")},
                "assemblies": sorted(r["assembly"] for r in rollback_reports), "restoredInputsUnchanged": True}
        if baseline.fingerprint(baseline.source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Current source changed during G7")
        summary["status"] = "Passed"
        print(f"G7 platform Passed ({summary['os']}): canonical generation, {summary['freshV5Tests']} SDK + {summary['freshV6Tests']} HTTP cases, "
              f"3 deployments, {summary['missingDependencyCases']} negative dependencies, V6 rollback. Cross-platform Pending.")
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
