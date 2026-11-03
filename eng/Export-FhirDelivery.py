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
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import FhirDelivery as contract

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("delivery_baseline", ROOT / "eng/Export-FhirVersionBaseline.py")
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)



def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, help="Fresh directory strictly under artifacts/")
    parser.add_argument("--composition", choices=["all", *contract.COMPOSITIONS], default="all")
    parser.add_argument("--tool-package", type=Path, help="Canonical nupkg; pack current tool when omitted")
    args = parser.parse_args()
    selected = list(contract.COMPOSITIONS) if args.composition == "all" else [args.composition]
    dlls = set().union(*(set(contract.composition(n)["requiredDlls"]) for n in selected))
    output = (ROOT / args.output).resolve()
    artifacts = (ROOT / "artifacts").resolve()
    if output == artifacts or not output.is_relative_to(artifacts) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/.")
    environment = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_CLI_USE_MSBUILD_SERVER="0",
                       MSBUILDDISABLENODEREUSE="1")
    def run(arguments, cwd=ROOT):
        result = subprocess.run(arguments, cwd=cwd, env=environment, stdout=subprocess.PIPE,
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
        if work.resolve().is_relative_to(ROOT.resolve()):
            raise ValueError("Fresh build workspace must be outside checkout")
        stage = work / "source"
        for row in files:
            destination = stage / row["path"]
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / row["path"], destination)
        publish, packages = work / "publish", work / "packages"
        print("Building fresh SDK, selected Models and optional IG...", flush=True)
        projects = ["MyFhirSdk.csproj"] + [f"Models/{r}/MyFhirSdk.{r}.Models.csproj" for r in ("R4", "R5") if f"MyFhirSdk.{r}.Models.dll" in dlls]
        if contract.IG_DLL in dlls:
            projects.append("ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj")
        for project in projects:
            run(["dotnet", "build", project, "-c", "Release", "-p:NuGetAudit=false"], stage)
        publish.mkdir()
        for name in dlls:
            if name == contract.IG_DLL:
                origin = stage / "ImplementationGuides/TwCore/bin/Release/net9.0" / name
            elif name.endswith(".Models.dll"):
                origin = stage / "Models" / name.split(".")[1] / "bin/Release/net9.0" / name
            else:
                origin = stage / "bin/Release/net9.0" / name
            shutil.copy2(origin, publish / name)
        if args.tool_package:
            package = args.tool_package.resolve()
        else:
            print("Packing current generator...", flush=True)
            run(["dotnet", "msbuild", "eng/MyFhirSdk.CodeGen.Build.proj", "/t:Restore;Pack", "/p:Configuration=Release",
                 "/p:PackageOutputPath=" + str(packages), "/m:1", "/nr:false"], stage)
            package = packages / f"MyFhirSdk.CodeGen.Tool.{tool_version}.nupkg"
        descriptor = json.loads((ROOT / "CodeGen/Policy/runtime-contract.json").read_text(encoding="utf-8"))
        contract.validate_tool(package, descriptor)
        run(["dotnet", "build", "Tests/KernelMigration/Inventory/Inventory.csproj", "-c", "Release", "-p:NuGetAudit=false"], stage)
        api = work / "assemblies-api.txt"
        names = sorted(dlls)
        run(["dotnet", str(stage / "Tests/KernelMigration/Inventory/bin/Release/net9.0/Inventory.dll"),
             str(publish / names[0]), str(api), *[str(publish / n) for n in names[1:]]], stage)
        identities = {}
        for line in api.read_text(encoding="utf-8").splitlines():
            if line.startswith("ASSEMBLY "):
                identity = line[len("ASSEMBLY "):]
                identities[identity.split(",")[0] + ".dll"] = identity
        if baseline.fingerprint(baseline.source_inventory()) != source:
            raise ValueError("Source changed during export; retry from stable sources.")
        output.mkdir(parents=True)
        for directory in ("sdk", "feed", "inputs", "contracts"):
            (output / directory).mkdir()
        for name in sorted(dlls):
            shutil.copy2(publish / name, output / "sdk" / name)
        shipped_package = contract.stage_tool(package, output / "feed", descriptor)
        inputs = []
        releases = sorted({r for n in selected for r in contract.COMPOSITIONS[n] if r != "IG"})
        shutil.copy2(ROOT / "CodeGen/Policy/runtime-contract.json", output / "contracts/runtime-contract.json")
        shutil.copy2(api, output / "contracts/assemblies-api.txt")
        for release_name in releases:
            release = release_name.lower()
            lock = json.loads((ROOT / f"CodeGen/Policy/{release}-package-lock.json").read_text())
            archive = ROOT / lock["archivePath"]
            if baseline.sha(archive) != lock["sha256"]:
                raise ValueError("FHIR input differs from its lock: " + release)
            shutil.copy2(archive, output / "inputs" / archive.name)
            shutil.copy2(ROOT / f"CodeGen/Policy/{release}-package-lock.json", output / "inputs")
            baseline.write(output / ("contracts/generated-" + release_name + ".json"), contract.inventory(ROOT / "Generated" / release_name))
            inputs.append({"release": release.upper(), "packageId": lock["packageId"],
                           "version": lock["packageVersion"], "sha256": lock["sha256"]})
        # Ship only inventoried source files, never an editor cache or an old example build.
        prefix = "examples/external/"
        for row in files:
            if row["path"].startswith(prefix):
                first = row["path"][len(prefix):].split("/")[0]
                if first in contract.COMPOSITIONS and first not in selected:
                    continue
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
        manifest = {"schemaVersion": 2, "sourceRevision": revision, "workingTreeDirty": dirty,
                    "sourceFingerprint": source, "dotnetSdk": expected_sdk, "targetFramework": "net9.0",
                    "toolVersion": tool_version, "toolPackage": "feed/" + shipped_package.name,
                    "toolPackageSha256": contract.sha(shipped_package), "fhirInputs": inputs,
                    "runtimeCompilerReferenceSha256": descriptor["compilerReference"]["sha256"],
                    "compositions": {n: contract.composition(n) for n in selected}, "assemblyIdentities": identities,
                    "acceptance": "Exported; verify outside the repository before handing off",
                    "files": {p.relative_to(output).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                              for p in sorted(output.rglob("*")) if p.is_file()}}
        baseline.write(output / "delivery-manifest.json", manifest)
        contract.validate_manifest(output, manifest)
        if baseline.fingerprint(baseline.source_inventory()) != source:
            raise ValueError("Source changed while creating delivery files")
    print("Delivery exported: " + str(output))


if __name__ == "__main__":
    main()
