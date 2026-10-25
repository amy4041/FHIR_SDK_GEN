#!/usr/bin/env python3
"""Publish four explicit binary compositions outside the checkout and execute them."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
NEUTRAL = {"MyFhirSdk", "MyFhirSdk.Runtime", "MyFhirSdk.ModelContracts", "MyFhirSdk.ModelSupport"}
SCENARIOS = {"r4": ("R4",), "r5": ("R5",), "dual": ("R4", "R5"), "r4-twcore": ("R4", "IG")}


def validate_inventory(case, expected, deployment):
    actual = {p.stem for p in deployment.glob("MyFhirSdk*.dll")}
    if actual != expected:
        raise ValueError(f"{case}: unexpected deployment inventory: {actual ^ expected}")
    deps = json.loads((deployment / "Probe.deps.json").read_text(encoding="utf-8"))
    runtime = {Path(name).stem for target in deps["targets"].values() for entry in target.values()
               for name in entry.get("runtime", {}) if Path(name).name.startswith("MyFhirSdk")}
    if runtime != expected:
        raise ValueError(f"{case}: dependency manifest differs: {runtime ^ expected}")


def validate_execution(case, expected, report, deployment):
    loaded = {a["name"] for a in report["assemblies"]}
    if report.get("status") != "Passed" or loaded != expected:
        raise ValueError(f"{case}: loaded SDK assemblies differ: {loaded ^ expected}")
    for assembly in report["assemblies"]:
        if Path(assembly["path"]).resolve().parent != deployment.resolve():
            raise ValueError("Assembly resolved outside deployment: " + assembly["path"])
        if assembly["name"] == "MyFhirSdk":
            refs = {r for r in assembly["references"] if r.startswith("MyFhirSdk")}
            if refs != NEUTRAL - {"MyFhirSdk"}:
                raise ValueError("SDK references concrete models or IG: " + repr(refs))
    releases = {r["fhirVersion"] for r in report["releases"]}
    expected_versions = {"4.0.1" if r == "R4" else "5.0.0" for r in SCENARIOS[case] if r != "IG"}
    if releases != expected_versions or any(not all(r[k] for k in ("roundTrip", "validation", "typedClient")) for r in report["releases"]):
        raise ValueError("Missing deployed release behavior")
    if "IG" in SCENARIOS[case] and (not report.get("ig") or not all(report["ig"][k] for k in ("positive", "negative", "replacement"))):
        raise ValueError("Missing deployed IG behavior")


def main():
    if os.name == "nt":
        import ctypes
        # Missing DLL probes intentionally crash; child processes inherit this mode.
        ctypes.windll.kernel32.SetErrorMode(0x0001 | 0x0002 | 0x8000)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists() or not output.is_relative_to((ROOT / "artifacts").resolve()):
        parser.error("Use a fresh directory below artifacts/")
    output.mkdir(parents=True)
    evidence = {"status": "Pending", "scenarios": {}}
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_NOLOGO="1")
    # An explicit isolated process avoids host resolution via inherited SDK probing settings.
    for key in ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS"):
        env.pop(key, None)

    def run(command, cwd, log, allow_failure=False):
        result = subprocess.run(command, cwd=cwd, env=env, capture_output=True, encoding="utf-8", errors="replace", timeout=180)
        log.write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode and not allow_failure:
            raise RuntimeError("Deployment command failed; see " + str(log))
        return result

    try:
        binaries = {n: ROOT / "bin/Release/net9.0" / (n + ".dll") for n in NEUTRAL}
        for release in ("R4", "R5"):
            binaries["MyFhirSdk." + release + ".Models"] = ROOT / "Models" / release / "bin/Release/net9.0" / ("MyFhirSdk." + release + ".Models.dll")
        binaries["MyFhirSdk.ImplementationGuides.TwCore"] = ROOT / "ImplementationGuides/TwCore/bin/Release/net9.0/MyFhirSdk.ImplementationGuides.TwCore.dll"
        sdk_hashes = set()
        with tempfile.TemporaryDirectory(prefix="myfhirsdk-d3-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT.resolve()):
                raise ValueError("Deployment must execute outside checkout")
            shutil.copyfile(ROOT / "global.json", work / "global.json")
            inputs = work / "binaries"
            inputs.mkdir()
            for name, path in binaries.items():
                shutil.copyfile(path, inputs / (name + ".dll"))
            for case, features in SCENARIOS.items():
                project = work / case
                project.mkdir()
                logs = output / case
                logs.mkdir()
                expected = NEUTRAL | {"MyFhirSdk." + f + ".Models" for f in features if f != "IG"}
                if "IG" in features:
                    expected |= {"MyFhirSdk.ImplementationGuides.TwCore"}
                for name in expected:
                    shutil.copyfile(inputs / (name + ".dll"), project / (name + ".dll"))
                shutil.copyfile(ROOT / "Tests/FhirVersions/V7/Program.cs", project / "Program.cs")
                if "IG" in features:
                    shutil.copyfile(ROOT / "Tests/SdkDecoupling/Deployment/IgProbe.cs", project / "IgProbe.cs")
                references = "".join('<Reference Include="' + name + '"><HintPath>' + escape(str(project / (name + ".dll"))) + '</HintPath></Reference>' for name in sorted(expected))
                symbols = ";".join("USE_" + f for f in features)
                (project / "Probe.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit><DefineConstants>' + symbols + '</DefineConstants></PropertyGroup><ItemGroup>' + references + '</ItemGroup></Project>', encoding="utf-8")
                # Fresh publish destination, no project reference or repository output probing.
                deployment = project / "publish"
                run(["dotnet", "publish", "Probe.csproj", "-c", "Release", "-o", str(deployment)], project, logs / "publish.log")
                validate_inventory(case, expected, deployment)
                report = json.loads(run(["dotnet", "Probe.dll"], deployment, logs / "run.log").stdout)
                validate_execution(case, expected, report, deployment)
                hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(deployment.glob("*.dll"))}
                for name in expected:
                    if hashes[name + ".dll"] != hashlib.sha256((inputs / (name + ".dll")).read_bytes()).hexdigest():
                        raise ValueError("Publish replaced an explicitly supplied assembly: " + name)
                sdk_hashes.add(hashes["MyFhirSdk.dll"])
                missing = []
                for name in sorted(expected):
                    target = deployment / (name + ".dll")
                    hidden = deployment / (name + ".missing")
                    # Both resolved paths are direct children of this case's temporary publish root.
                    if target.resolve().parent != deployment.resolve() or hidden.resolve().parent != deployment.resolve():
                        raise ValueError("Unsafe missing-dependency test path")
                    target.rename(hidden)
                    try:
                        result = run(["dotnet", "Probe.dll"], deployment, logs / (name + "-missing.log"), allow_failure=True)
                        diagnostic = result.stdout + result.stderr
                        if not result.returncode or name not in diagnostic or not any(s in diagnostic.lower() for s in ("not found", "could not load", "failed to load")):
                            raise ValueError("Missing dependency was not diagnosed: " + name)
                        missing.append({"assembly": name, "exitCode": result.returncode, "diagnosed": True})
                    finally:
                        hidden.rename(target)
                shutil.copyfile(deployment / "Probe.deps.json", logs / "Probe.deps.json")
                evidence["scenarios"][case] = {"execution": report, "dllSha256": hashes, "missingDependencies": missing, "outsideCheckout": True}
        if len(sdk_hashes) != 1:
            raise ValueError("Scenarios used different SDK binaries")
        evidence.update(status="Passed", sdkSha256=next(iter(sdk_hashes)))
    except Exception as error:
        evidence.update(status="Failed", error=str(error))
    (output / "evidence.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(evidence, indent=2))
    return 0 if evidence["status"] == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
