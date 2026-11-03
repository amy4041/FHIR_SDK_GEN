#!/usr/bin/env python3
"""Verify a handoff bundle in a temporary directory outside the repository."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import FhirDelivery as contract
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def main():
    if os.name == "nt":
        import ctypes
        ctypes.windll.kernel32.SetErrorMode(0x8003)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", required=True)
    parser.add_argument("--output", required=True, help="Fresh evidence directory under artifacts/")
    args = parser.parse_args()
    bundle = Path(args.bundle).resolve()
    output = (ROOT / args.output).resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output == artifacts or not output.is_relative_to(artifacts) or output.exists():
        parser.error("Use a fresh evidence directory strictly under artifacts/.")
    if output.is_relative_to(bundle):
        parser.error("Evidence must be outside the immutable delivery bundle")
    manifest = json.loads((bundle / "delivery-manifest.json").read_text(encoding="utf-8"))
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "status": "Running", "sourceRevision": manifest["sourceRevision"],
               "sourceFingerprint": manifest["sourceFingerprint"],
               "deliveryManifestSha256": hashlib.sha256((bundle / "delivery-manifest.json").read_bytes()).hexdigest(),
               "externalServer": "NotRun", "examples": {}, "generation": {}}
    try:
        contract.validate_manifest(bundle, manifest)
        descriptor = json.loads((bundle / "contracts/runtime-contract.json").read_text(encoding="utf-8"))
        contract.validate_tool(contract.safe_path(bundle, manifest["toolPackage"]), descriptor)
        with tempfile.TemporaryDirectory(prefix="FhirExternalVerification-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT):
                raise ValueError("Verification must run outside repository.")
            stage = work / "delivery"
            # Copy only declared delivery files; never import local caches or previous verification output.
            for name in manifest["files"]:
                destination = contract.safe_path(stage, name)
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(bundle / name, destination)
            environment = {k: v for k, v in os.environ.items()
                           if not k.startswith("MYFHIRSDK_") and k not in
                           ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS")}
            environment.update(NUGET_PACKAGES=str(work / "packages"), DOTNET_CLI_HOME=str(work / "cli-home"),
                               NUGET_HTTP_CACHE_PATH=str(work / "http-cache"), DOTNET_CLI_UI_LANGUAGE="en-US",
                               DOTNET_CLI_USE_MSBUILD_SERVER="0", MSBUILDDISABLENODEREUSE="1",
                               DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_ADD_GLOBAL_TOOLS_TO_PATH="false",
                               DOTNET_GENERATE_ASPNET_CERTIFICATE="false")

            def run(name, arguments, allow_failure=False, cwd=None):
                result = subprocess.run(["dotnet", *arguments], cwd=cwd or stage, env=environment,
                                        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=600)
                log = (result.stdout + result.stderr).replace(str(work), "<outside>").replace(str(ROOT), "<checkout>")
                (output / (name + ".log")).write_text(log, encoding="utf-8", newline="\n")
                if result.returncode and not allow_failure:
                    raise ValueError("External verification failed: " + name)
                return result

            if Path(environment["NUGET_PACKAGES"]).exists():
                raise ValueError("Verification package cache must start empty")
            for release, spec in manifest["compositions"].items():
                # Each example receives only its required DLLs; unused models/IG are absent even beside its inputs.
                case = work / "cases" / release
                case.mkdir(parents=True)
                for name in ("global.json", "NuGet.Config"):
                    shutil.copy2(stage / name, case / name)
                # NuGet.Config is local-only; examples have no PackageReference.
                (case / "sdk").mkdir()
                for name in spec["requiredDlls"]:
                    shutil.copy2(stage / "sdk" / name, case / "sdk" / name)
                shutil.copytree(stage / "examples/Shared", case / "examples/Shared")
                shutil.copy2(stage / "examples/SdkReferences.props", case / "examples/SdkReferences.props")
                shutil.copytree(stage / "examples" / release, case / "examples" / release)
                project = spec["exampleProject"]
                if ET.parse(case / project).getroot().findall(".//ProjectReference"):
                    raise ValueError("External examples must use DLLs, not ProjectReference")
                deployed = case / "publish"
                run(release + "-publish", ["publish", project, "-c", "Release", "--self-contained", "false",
                    "-p:UseAppHost=false", "-p:NuGetAudit=false", "-o", str(deployed)], cwd=case)
                result = run(release + "-run", [str(deployed / (release + "Example.dll"))], cwd=deployed)
                report = json.loads(result.stdout.strip().splitlines()[-1])
                contract.validate_execution(release, report, deployed)
                for assembly in report["assemblies"]:
                    if assembly["identity"] != manifest["assemblyIdentities"][assembly["name"] + ".dll"]:
                        raise ValueError("Executed assembly identity differs from manifest")
                    assembly["path"] = "<deployment>/" + Path(assembly["path"]).name
                negatives = []
                for name in spec["requiredDlls"]:
                    path = deployed / name
                    if contract.sha(path) != manifest["files"]["sdk/" + name]:
                        raise ValueError("Publish replaced delivered bytes: " + name)
                    hidden = path.with_suffix(".missing")
                    if path.resolve().parent != deployed.resolve() or hidden.resolve().parent != deployed.resolve():
                        raise ValueError("Unsafe negative-test path")
                    path.rename(hidden)
                    try:
                        failure = run(release + "-missing-" + name, [str(deployed / (release + "Example.dll"))],
                                      allow_failure=True, cwd=deployed)
                        diagnostic = failure.stdout + failure.stderr
                        if not failure.returncode or Path(name).stem not in diagnostic or not any(t in diagnostic.lower() for t in ("not found", "could not load", "failed to load")):
                            raise ValueError("Missing dependency not diagnosed: " + name)
                        negatives.append({"assembly": name, "exitCode": failure.returncode, "diagnosed": True})
                    finally:
                        hidden.rename(path)
                shutil.copytree(deployed, output / "deployments" / release)
                hashes = contract.inventory(deployed)
                (output / (release + "-execution.json")).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
                (output / (release + "-dll-inventory.json")).write_text(json.dumps(hashes, indent=2) + "\n", encoding="utf-8")
                summary["examples"][release] = {"status": "Passed", "versions": spec["fhirVersions"],
                    "requiredDlls": spec["requiredDlls"], "missingDependencies": negatives,
                    "dllReferencesOnly": True, "publishedExecution": True, "onlyRequiredInputs": True}
            run("tool-restore", ["tool", "restore", "--configfile", "NuGet.Config"])
            references = list(Path(environment["NUGET_PACKAGES"]).rglob("RuntimeReferences/net9.0/MyFhirSdk.Runtime.dll"))
            if len(references) != 1 or contract.sha(references[0]) != descriptor["compilerReference"]["sha256"]:
                raise ValueError("Fresh installed tool uses an unexpected Runtime reference")
            summary["canonicalInstall"] = {"emptyInitialCache": True, "localFeedOnly": True,
                "compilerReferenceSha256": contract.sha(references[0]), "toolPackageSha256": manifest["toolPackageSha256"]}
            for release in sorted({r for c in manifest["compositions"].values() for r in c["fhirVersions"]}):
                version = contract.VERSIONS[release]
                expected = json.loads((stage / ("contracts/generated-" + release + ".json")).read_text(encoding="utf-8"))
                count = sum(p.endswith(".cs") for p in expected)
                package_id = f"hl7.fhir.{release.lower()}.core"
                archive = f"inputs/{package_id}-{version}.tgz"
                destination = f"generated/{release.lower()}"
                common = ["myfhir-codegen", "--input", archive, "--fhir-version", version,
                          "--package-id", package_id, "--package-version", version]
                hashes = None
                for repeat in range(2):
                    run(f"{release}-model-{repeat}", [*common, "--mode", "model", "--output", destination])
                    run(f"{release}-primitive-{repeat}", [*common, "--mode", "primitive", "--output",
                        f"{destination}/Generated/{release}/Primitives"])
                    generated = stage / destination / "Generated" / release
                    current = {p.relative_to(generated).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                               for p in sorted(generated.rglob("*")) if p.is_file()}
                    if sum(name.endswith(".cs") for name in current) != count or not current:
                        raise ValueError("Unexpected generated C# count: " + release)
                    if hashes is not None and current != hashes:
                        raise ValueError("Repeated generation differs: " + release)
                    if current != expected:
                        raise ValueError("Installed tool differs from committed generated bytes: " + release)
                    hashes = current
                (output / (release + "-generated-hashes.json")).write_text(
                    json.dumps(hashes, indent=2) + "\n", encoding="utf-8", newline="\n")
                shutil.copytree(generated, output / "generated" / release)
                summary["generation"][release] = {"status": "Passed", "sourceCount": count,
                                                  "repeatedGenerationMatches": True, "matchesCommittedModels": True}
            summary.update(status="Passed", outsideRepository=True, isolatedCache=True,
                           inheritedEndpointsDisabled=True)
    except Exception as error:
        summary.update(status="Failed", error=str(error))
        raise
    finally:
        (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("External delivery Passed: selected DLL compositions, missing dependencies and canonical repeated generation.")


if __name__ == "__main__":
    main()
