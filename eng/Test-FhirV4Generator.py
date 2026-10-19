#!/usr/bin/env python3
"""G4: fresh regression, then installed R4/R5 generation outside the checkout.

Requires restored solution, pinned .NET SDK, Python and PowerShell on PATH.
Uses only the locally packed tool and the locked official package inputs.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("g3", ROOT / "eng/Test-FhirV3Migration.py")
g3 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g3)
baseline = g3.baseline


def inventory(root):
    return {p.relative_to(root).as_posix(): baseline.sha(p) for p in sorted(root.rglob("*")) if p.is_file()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v4/run")
    parser.add_argument("--export-generated", action="store_true", help="Retain verified installed-tool trees for G5 fresh compilation")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if output == ROOT / "artifacts" or not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G4", "status": "Pending", "completeR4Support": False}
    previous_ui = os.environ.get("DOTNET_CLI_UI_LANGUAGE")
    os.environ["DOTNET_CLI_UI_LANGUAGE"] = "en-US"
    try:
        sys.argv = ["Test-FhirV3Migration.py", "--output", str(output / "migration")]
        g3.main()
        regression = json.loads((output / "migration/summary.json").read_text())
        for key in ("sourceRevision", "sourceFingerprint", "createdAtUtc", "os", "sdkVersion", "testCounts",
                    "runtimeContract", "testedImplementationHashes", "preservedGeneratedSources"):
            summary[key] = regression[key]
        cases = []
        for report in (output / "migration/context/test-results").glob("*.trx"):
            for result in ET.parse(report).getroot().findall(".//{*}UnitTestResult"):
                if "FhirReleaseGenerationTests." in result.attrib["testName"]:
                    cases.append({"test": result.attrib["testName"], "outcome": result.attrib["outcome"]})
        required = {"DocumentedR4CommandsProduceCompleteModelsAndPrimitivesInOneTree",
                    "ForeignAuxiliaryClrIdentityIsRejectedInBothModesWithoutChangingOutput"}
        if (len(cases) < 16 or any(c["outcome"] != "Passed" for c in cases) or
                any(not any(method in c["test"] for c in cases) for method in required)):
            raise ValueError("Missing or failed V4 release/semantic composition/negative cases")
        baseline.write(output / "v4-test-cases.json", cases)
        version = json.loads((ROOT / "CodeGen/Policy/runtime-contract.json").read_text())["compatibility"]["toolVersion"]
        package = ROOT / f"Tests/CodeGen/bin/Release/net9.0/ToolPackages/first/MyFhirSdk.CodeGen.Tool.{version}.nupkg"
        summary.update(toolVersion=version, toolPackageSha256=baseline.sha(package), v4Tests=len(cases))
        with zipfile.ZipFile(package) as archive:
            names = archive.namelist()
            if any(Path(n).name in {"MyFhirSdk.dll", "MyFhirSdk.R4.Models.dll", "MyFhirSdk.R5.Models.dll", "MyFhirSdk.ModelSupport.dll"} for n in names):
                raise ValueError("Tool package contains a deployed SDK/model/support implementation")
        with tempfile.TemporaryDirectory(prefix="MyFhirSdk-V4-Installed-") as temporary:
            work = Path(temporary).resolve()
            if work.is_relative_to(ROOT):
                raise ValueError("Installed tool workspace must be outside checkout")
            shutil.copy2(ROOT / "global.json", work / "global.json")
            feed = work / "feed"
            feed.mkdir()
            shutil.copy2(package, feed / package.name)
            (work / ".config").mkdir()
            shutil.copy2(ROOT / ".config/dotnet-tools.json", work / ".config/dotnet-tools.json")
            config = ET.Element("configuration")
            sources = ET.SubElement(config, "packageSources")
            ET.SubElement(sources, "clear")
            ET.SubElement(sources, "add", {"key": "local", "value": str(feed)})
            ET.ElementTree(config).write(work / "NuGet.Config", encoding="utf-8", xml_declaration=True)
            env = dict(os.environ)
            for key, folder in {"DOTNET_CLI_HOME": "cli-home", "NUGET_PACKAGES": "packages", "NUGET_HTTP_CACHE_PATH": "http-cache",
                                "NUGET_PLUGINS_CACHE_PATH": "plugins", "NUGET_SCRATCH": "scratch"}.items():
                env[key] = str(work / folder)
            for key in ("DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "DOTNET_STARTUP_HOOKS"):
                env.pop(key, None)

            def run(name, arguments, expected=0, diagnostic=None):
                process = subprocess.run(["dotnet", *arguments], cwd=work, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=240)
                log = (process.stdout + process.stderr).replace(str(work), "<isolated>").replace(str(ROOT), "<checkout>")
                (output / f"{name}.log").write_text(log, encoding="utf-8", newline="\n")
                if process.returncode != expected or diagnostic and diagnostic not in log:
                    raise ValueError(f"Installed tool case failed: {name}, exit={process.returncode}")
                return process

            run("tool-restore", ["tool", "restore", "--configfile", str(work / "NuGet.Config")])
            run("help", ["myfhir-codegen", "--help"])
            r5_source = next((work / "packages").rglob("CompilerSources/SimpleQuantity.cs"))
            tool_root = r5_source.parents[2]
            summary["installedTool"] = {"outsideCheckout": True, "freshNuGetCache": True, "localFeedOnly": True, "releases": {}}
            for release, version in (("R4", "4.0.1"), ("R5", "5.0.0")):
                lock = json.loads((ROOT / f"CodeGen/Policy/{release.lower()}-package-lock.json").read_text())
                source = work / f"{release}.tgz"
                shutil.copy2(ROOT / lock["archivePath"], source)
                if baseline.sha(source) != lock["sha256"]:
                    raise ValueError("Package input lock mismatch")
                model_output = work / release / "models"
                primitive_output = model_output / f"Generated/{release}/Primitives"
                common = ["--input", str(source), "--fhir-version", version, "--package-id", lock["packageId"], "--package-version", version]
                model_hashes = None
                for mode, destination in (("model", model_output), ("primitive", primitive_output)):
                    command = ["myfhir-codegen", "--mode", mode, *common, "--output", str(destination)]
                    run(f"{release}-{mode}", command)
                    before = inventory(destination)
                    run(f"{release}-{mode}-repeat", command)
                    if inventory(destination) != before:
                        raise ValueError("Repeated installed generation differs")
                    baseline.write(output / f"{release}-{mode}-hashes.json", before)
                    if mode == "model":
                        model_hashes = before
                combined = inventory(model_output)
                if any(combined.get(path) != digest for path, digest in model_hashes.items()):
                    raise ValueError("Nested primitive generation changed model output")
                expected = dict(model_hashes)
                expected.update({f"Generated/{release}/Primitives/{path}": digest for path, digest in inventory(primitive_output).items()})
                if combined != expected:
                    raise ValueError("Combined model/primitive output tree is incomplete")
                baseline.write(output / f"{release}-combined-hashes.json", combined)
                root = model_output / f"Generated/{release}"
                model_manifest = json.loads((root / "model-generation-manifest.json").read_text())
                primitive_manifest = json.loads((primitive_output / "primitive-generation-manifest.json").read_text())
                if model_manifest["package"]["sha256"] != lock["sha256"] or primitive_manifest["fhirVersion"] != version:
                    raise ValueError("Installed manifest release provenance mismatch")
                if release == "R5":
                    for relative, digest in inventory(root).items():
                        if baseline.sha(ROOT / "Generated/R5" / relative) != digest:
                            raise ValueError(f"Installed R5 source drift: {relative}")
                    for relative, digest in inventory(primitive_output).items():
                        if baseline.sha(ROOT / "Generated/R5/Primitives" / relative) != digest:
                            raise ValueError(f"Installed R5 primitive drift: {relative}")
                model_files = [p for p in root.rglob("*.cs") if p.relative_to(root).parts[0] != "Primitives"]
                summary["installedTool"]["releases"][release] = {"fhirVersion": version, "packageSha256": lock["sha256"],
                    "modelSources": len(model_files), "primitiveSources": len(list(primitive_output.glob("*.cs"))),
                    "modelSourceBytes": sum(p.stat().st_size for p in model_files),
                    "primitiveSourceBytes": sum(p.stat().st_size for p in primitive_output.glob("*.cs")),
                    "deterministic": True, "combinedOutputComplete": True}
                descriptor = tool_root / "Contracts" / ("R4" if release == "R4" else "") / "runtime-contract.json"
                mixed = json.loads(descriptor.read_text(encoding="utf-8"))
                mixed["compilerSources"][0]["clrType"] = "MyFhirSdk.Types.SimpleQuantity" if release == "R4" else "MyFhirSdk.R4.Types.SimpleQuantity"
                mixed_path = work / f"{release}-mixed-contract.json"
                mixed_path.write_text(json.dumps(mixed, indent=2) + "\n", encoding="utf-8", newline="\n")
                for mode, destination in (("model", model_output), ("primitive", primitive_output)):
                    run(f"reject-{release}-{mode}-foreign-clr", ["myfhir-codegen", "--mode", mode, *common,
                        "--output", str(destination), "--runtime-contract", str(mixed_path)], 2, "FSG0131")
                    if inventory(model_output) != combined:
                        raise ValueError("Foreign auxiliary CLR identity changed existing output")
                summary["installedTool"]["releases"][release]["foreignAuxiliaryClrIdentityRejected"] = True
                if args.export_generated:
                    exported = output / "generated" / release
                    shutil.copytree(root, exported)
                    if inventory(exported) != inventory(root):
                        raise ValueError("Exported installed-tool sources changed")
                if release == "R4":
                    before = inventory(model_output)
                    command = ["myfhir-codegen", "--mode", "model", *common, "--output", str(model_output)]
                    r5_policy_root = r5_source.parents[2] / "Policy"
                    run("reject-r5-auxiliary", [*command, "--compiler-source", str(r5_source)], 2, "FSG0131")
                    run("reject-r5-policies", [*command, "--policy-root", str(r5_policy_root)], 2, "FSG0124")
                    run("reject-missing-auxiliary", [*command, "--compiler-source", str(work / "missing.cs")], 2, "FSG0130")
                    if inventory(model_output) != before:
                        raise ValueError("Rejected combination changed existing output")
                    summary["negativeOutputPreserved"] = True
            summary["installedTool"]["status"] = "Passed"
        if baseline.fingerprint(baseline.source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Source changed during G4 run")
        summary["status"] = "Passed"
        print(f"G4 Passed: {summary['v4Tests']} release tests; installed R4/R5 generation and determinism; {summary['testCounts']}")
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
