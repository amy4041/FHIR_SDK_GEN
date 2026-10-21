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
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", required=True)
    parser.add_argument("--output", required=True, help="Fresh evidence directory under artifacts/")
    args = parser.parse_args()
    bundle = Path(args.bundle).resolve()
    output = (ROOT / args.output).resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output == artifacts or not output.is_relative_to(artifacts) or output.exists():
        parser.error("Use a fresh evidence directory strictly under artifacts/.")
    manifest = json.loads((bundle / "delivery-manifest.json").read_text(encoding="utf-8"))
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "status": "Running", "sourceRevision": manifest["sourceRevision"],
               "sourceFingerprint": manifest["sourceFingerprint"],
               "deliveryManifestSha256": hashlib.sha256((bundle / "delivery-manifest.json").read_bytes()).hexdigest(),
               "externalServer": "NotRun", "examples": {}, "generation": {}}
    try:
        for name, digest in manifest["files"].items():
            path = (bundle / name).resolve()
            if not path.is_relative_to(bundle) or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                raise ValueError("Delivery file failed integrity check: " + name)
        with tempfile.TemporaryDirectory(prefix="FhirExternalVerification-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT):
                raise ValueError("Verification must run outside repository.")
            stage = work / "delivery"
            # Copy only declared delivery files; never import local caches or previous verification output.
            for name in manifest["files"]:
                destination = stage / name
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

            def run(name, arguments):
                result = subprocess.run(["dotnet", *arguments], cwd=stage, env=environment,
                                        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=600)
                log = (result.stdout + result.stderr).replace(str(work), "<outside>").replace(str(ROOT), "<checkout>")
                (output / (name + ".log")).write_text(log, encoding="utf-8", newline="\n")
                if result.returncode:
                    raise ValueError("External verification failed: " + name)
                return result.stdout

            for release in ("R4", "R5", "Dual"):
                project = f"examples/{release}/{release}Example.csproj"
                if ET.parse(stage / project).getroot().findall(".//ProjectReference"):
                    raise ValueError("External examples must use DLLs, not ProjectReference.")
                deployed = stage / "publish" / release
                run(release + "-publish", ["publish", project, "-c", "Release", "--self-contained", "false",
                    "-p:UseAppHost=false", "-p:NuGetAudit=false", "-o", str(deployed)])
                result = run(release + "-run", [str(deployed / (release + "Example.dll"))])
                expected = ("R4", "R5") if release == "Dual" else (release,)
                for version in expected:
                    if version + ": JSON, validation and controlled HTTP read passed." not in result:
                        raise ValueError("Missing expected SDK example result: " + version)
                summary["examples"][release] = {"status": "Passed", "versions": expected,
                                                "dllReferencesOnly": True, "publishedExecution": True}
            run("tool-restore", ["tool", "restore", "--configfile", "NuGet.Config"])
            for release, version, count in (("R4", "4.0.1", 677), ("R5", "5.0.0", 852)):
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
                    hashes = current
                (output / (release + "-generated-hashes.json")).write_text(
                    json.dumps(hashes, indent=2) + "\n", encoding="utf-8", newline="\n")
                summary["generation"][release] = {"status": "Passed", "sourceCount": count,
                                                  "repeatedGenerationMatches": True}
            summary.update(status="Passed", outsideRepository=True, isolatedCache=True,
                           inheritedEndpointsDisabled=True)
    except Exception as error:
        summary.update(status="Failed", error=str(error))
        raise
    finally:
        (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("External delivery Passed: R4/R5/Dual DLL consumers and repeated R4/R5 generation.")


if __name__ == "__main__":
    main()
