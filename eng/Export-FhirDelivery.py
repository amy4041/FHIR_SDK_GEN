#!/usr/bin/env python3
"""Build a fresh local SDK/tool handoff bundle with DLL-only external examples."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("delivery_baseline", ROOT / "eng/Export-FhirVersionBaseline.py")
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)
DLLS = ("MyFhirSdk.dll", "MyFhirSdk.Runtime.dll", "MyFhirSdk.ModelContracts.dll",
        "MyFhirSdk.ModelSupport.dll", "MyFhirSdk.R4.Models.dll", "MyFhirSdk.R5.Models.dll")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, help="Fresh directory strictly under artifacts/")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output == artifacts or not output.is_relative_to(artifacts) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/.")
    environment = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_CLI_USE_MSBUILD_SERVER="0",
                       MSBUILDDISABLENODEREUSE="1")
    def run(arguments):
        result = subprocess.run(arguments, cwd=ROOT, env=environment, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace", timeout=900)
        if result.returncode:
            print(result.stdout)
            result.check_returncode()
        return result.stdout.strip()
    expected_sdk = json.loads((ROOT / "global.json").read_text())["sdk"]["version"]
    if run(["dotnet", "--version"]) != expected_sdk:
        raise ValueError("Use the SDK pinned in global.json.")
    files = baseline.source_inventory()
    source = baseline.fingerprint(files)
    revision = run(["git", "rev-parse", "HEAD"])
    dirty = bool(run(["git", "status", "--porcelain"]))
    tool_version = json.loads((ROOT / ".config/dotnet-tools.json").read_text())["tools"]["myfhirsdk.codegen.tool"]["version"]
    # Build first in an isolated working directory; never reuse historical verified/ DLLs or packages.
    with tempfile.TemporaryDirectory(prefix="FhirDelivery-") as temporary:
        work = Path(temporary)
        publish, packages = work / "publish", work / "packages"
        print("Publishing current R4/R5 implementations...", flush=True)
        run(["dotnet", "publish", "Tests/FhirVersions/V7/Dual/DualConsumer.csproj", "-c", "Release",
             "--self-contained", "false", "-p:UseAppHost=false", "-o", str(publish)])
        print("Packing current generator...", flush=True)
        run(["dotnet", "msbuild", "eng/MyFhirSdk.CodeGen.Build.proj", "/t:Pack", "/p:Configuration=Release",
             "/p:PackageOutputPath=" + str(packages), "/m:1", "/nr:false"])
        package = packages / f"MyFhirSdk.CodeGen.Tool.{tool_version}.nupkg"
        if not package.is_file() or any(not (publish / name).is_file() for name in DLLS):
            raise ValueError("Missing current package or SDK implementation.")
        if baseline.fingerprint(baseline.source_inventory()) != source:
            raise ValueError("Source changed during export; retry from stable sources.")
        output.mkdir(parents=True)
        for directory in ("sdk", "feed", "inputs"):
            (output / directory).mkdir()
        for name in DLLS:
            shutil.copy2(publish / name, output / "sdk" / name)
        shutil.copy2(package, output / "feed" / package.name)
        inputs = []
        for release in ("r4", "r5"):
            lock = json.loads((ROOT / f"CodeGen/Policy/{release}-package-lock.json").read_text())
            archive = ROOT / lock["archivePath"]
            if baseline.sha(archive) != lock["sha256"]:
                raise ValueError("FHIR input differs from its lock: " + release)
            shutil.copy2(archive, output / "inputs" / archive.name)
            shutil.copy2(ROOT / f"CodeGen/Policy/{release}-package-lock.json", output / "inputs")
            inputs.append({"release": release.upper(), "packageId": lock["packageId"],
                           "version": lock["packageVersion"], "sha256": lock["sha256"]})
        # Ship only inventoried source files, never an editor cache or an old example build.
        prefix = "examples/external/"
        for row in files:
            if row["path"].startswith(prefix):
                destination = output / "examples" / row["path"][len(prefix):]
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(ROOT / row["path"], destination)
        for template in (output / "examples").rglob("*.csproj.template"):
            template.rename(template.with_suffix(""))
        shutil.copy2(ROOT / "examples/external/README.md", output / "README.md")
        shutil.copy2(ROOT / "global.json", output / "global.json")
        (output / ".config").mkdir()
        shutil.copy2(ROOT / ".config/dotnet-tools.json", output / ".config/dotnet-tools.json")
        (output / "NuGet.Config").write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n<configuration><packageSources><clear />'
            '<add key="delivery-local" value="feed" /></packageSources></configuration>\n', encoding="utf-8")
        manifest = {"schemaVersion": 1, "sourceRevision": revision, "workingTreeDirty": dirty,
                    "sourceFingerprint": source, "dotnetSdk": expected_sdk, "targetFramework": "net9.0",
                    "toolVersion": tool_version, "fhirInputs": inputs,
                    "acceptance": "Exported; verify outside the repository before handing off",
                    "files": {p.relative_to(output).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                              for p in sorted(output.rglob("*")) if p.is_file()}}
        baseline.write(output / "delivery-manifest.json", manifest)
    print("Delivery exported: " + str(output))


if __name__ == "__main__":
    main()
