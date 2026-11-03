#!/usr/bin/env python3
"""V0 evidence runner. Python 3.10+, Git, pinned .NET SDK; no Python dependencies.

All generated evidence stays under artifacts/. Never accepts cached regression.
The R4 lock is input evidence only; this does not enable R4 in CodeGen.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
EXPECTED_TESTS = {
    "MyFhirSdk.Architecture.Tests", "MyFhirSdk.CodeGen.Tests",
    "MyFhirSdk.Client.Tests", "MyFhirSdk.Parser.Json.Tests",
    "MyFhirSdk.Serialization.Json.Tests", "MyFhirSdk.Validation.Tests",
    "MyFhirSdk.ImplementationGuides.TwCore.Tests",
    "MyFhirSdk.FhirVersions.V5.Tests",
    "MyFhirSdk.FhirVersions.V6.Tests",
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def run(arguments, log=None):
    result = subprocess.run(arguments, cwd=ROOT, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, encoding="utf-8", errors="replace")
    if log:
        log.write_text(result.stdout, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {' '.join(arguments)}; see {log.name if log else result.stdout}")
    return result.stdout.strip()


def source_inventory():
    names = run(["git", "ls-files", "--cached", "--others", "--exclude-standard", "--deduplicate", "-z"]).split("\0")
    # Portable published evidence must not change the fingerprint it describes.
    return [{"path": name, "sha256": sha(ROOT / name), "bytes": (ROOT / name).stat().st_size}
            for name in sorted(set(names)) if name and (ROOT / name).is_file()
            and not name.startswith("docs/fhir_versions/baselines/")
            and not any(p in {"artifacts", "bin", "obj", ".git", ".codex", ".agents", ".aws"}
                        for p in Path(name).parts)]


def fingerprint(files):
    text = "".join(f"{f['path']}|{f['sha256']}\n" for f in files)
    return {"algorithm": "sha256-path-and-bytes-v1", "fileCount": len(files),
            "sha256": hashlib.sha256(text.encode()).hexdigest()}


def package_inventory(lock):
    archive = ROOT / lock["archivePath"]
    if sha(archive) != lock["sha256"]:
        raise ValueError("Package SHA-256 does not match lock")
    rows = []
    # Match each release's definitions to the actual Runtime compile owner.
    # Root definitions may omit derivation (R4 Element/Resource and R5 Base).
    ownership_source = "Runtime/KernelCompileItems.props"
    bootstrap_names = {Path(item.attrib["Include"].replace("\\", "/")).stem
                       for item in ET.parse(ROOT / ownership_source).findall(".//RuntimeKernelSource")}
    with tarfile.open(archive, "r:gz") as tar:
        meta = json.load(tar.extractfile("package/package.json"))
        if (meta["name"], meta["version"], meta["fhirVersions"], meta["type"], meta["license"]) != (
                lock["packageId"], lock["packageVersion"], lock["fhirVersions"], lock.get("metadataPackageType", lock["packageType"]), lock["license"]):
            raise ValueError("Package metadata does not match lock")
        for member in tar.getmembers():
            if not member.name.startswith("package/StructureDefinition-") or not member.name.endswith(".json"):
                continue
            raw = tar.extractfile(member).read()
            d = json.loads(raw)
            if d.get("resourceType") != "StructureDefinition":
                raise ValueError(f"Unexpected resource: {member.name}")
            kind, derivation, name = d.get("kind"), d.get("derivation"), d.get("id")
            if name in bootstrap_names and kind in {"complex-type", "resource"} and derivation in {None, "specialization"}:
                disposition = "runtime-bootstrap-candidate-requires-V1"
                reason = "Existing Runtime compile owner; verify this release's hierarchy, JSON and validation in V1"
            elif name == "SimpleQuantity":
                disposition = "version-specific-auxiliary-required"
                reason = "Quantity constraint requires a version-specific auxiliary source"
            elif derivation == "specialization" and kind in {"primitive-type", "complex-type", "resource"}:
                disposition = "model-generation-candidate-requires-V4-policy"
                reason = "Core specialization without a Runtime compile owner; requires V4 generation policy"
            else:
                disposition = "excluded-from-base-models"
                reason = "Constraint profiles, logical models and other non-specializations require separate policy"
            elements = d.get("snapshot", {}).get("element", [])
            rows.append({"path": member.name, "sha256": hashlib.sha256(raw).hexdigest(),
                         "bytes": len(raw), "id": name, "url": d.get("url"), "type": d.get("type"),
                         "kind": kind, "derivation": derivation, "abstract": d.get("abstract", False),
                         "baseDefinition": d.get("baseDefinition"), "disposition": disposition,
                         "dispositionReason": reason,
                         "snapshotElements": len(elements),
                         "requiredElementOccurrences": sum(e.get("min", 0) > 0 for e in elements),
                         "choiceElementOccurrences": sum("[x]" in e.get("path", "") for e in elements),
                         "bindingOccurrences": sum("binding" in e for e in elements),
                         "constraintOccurrences": sum(len(e.get("constraint", [])) for e in elements),
                         "simpleQuantityUsage": [e["path"] for e in elements if any(
                             "SimpleQuantity" in t.get("code", "") or any("SimpleQuantity" in p for p in t.get("profile", []))
                             for t in e.get("type", []))],
                         "attachmentSizeTypes": [t["code"] for e in elements if e.get("path") == "Attachment.size"
                                                 for t in e.get("type", [])]})
    rows.sort(key=lambda d: d["path"])
    return {"metadata": meta, "archiveBytes": archive.stat().st_size, "sha256": sha(archive),
            "bootstrapOwnershipSource": ownership_source,
            "structureDefinitions": len(rows),
            "byKindAndDerivation": dict(sorted(Counter(f"{r['kind']}/{r['derivation']}" for r in rows).items())),
            "byDisposition": dict(sorted(Counter(r["disposition"] for r in rows).items())),
            "definitions": rows}


def project_inventory(project):
    """Evaluate references with the explicit implementation paths used by consumers.

    Historical fixture source is unchanged; its inventory describes a current
    implementation evaluation, not the historical binary compatibility run.
    Reference.FullPath identifies the MSBuild item, not its DLL; use HintPath.
    """
    paths = {"SdkAssemblyPath": "bin/Release/net9.0/MyFhirSdk.dll",
             "RuntimeAssemblyPath": "bin/Release/net9.0/MyFhirSdk.Runtime.dll",
             "ContractsAssemblyPath": "bin/Release/net9.0/MyFhirSdk.ModelContracts.dll",
             "SupportAssemblyPath": "bin/Release/net9.0/MyFhirSdk.ModelSupport.dll"}
    if (ROOT / "Models/R5/MyFhirSdk.R5.Models.csproj").is_file():
        paths["ModelsAssemblyPath"] = "Models/R5/bin/Release/net9.0/MyFhirSdk.R5.Models.dll"
    data = json.loads(run(["dotnet", "msbuild", project, "-p:Configuration=Release",
                           *[f"-p:{name}={ROOT / path}" for name, path in paths.items()],
                           "-getItem:Compile,ProjectReference,Reference", "-getProperty:TargetFramework,AssemblyName"]))

    def portable(path):
        absolute = Path(path).resolve()
        return absolute.relative_to(ROOT).as_posix() if absolute.is_relative_to(ROOT) else "external:" + absolute.name

    dll_references = []
    for item in data["Items"]["Reference"]:
        hint = item.get("HintPath")
        if item["Identity"] in {"MyFhirSdk", "MyFhirSdk.Runtime", "MyFhirSdk.R5.Models", "MyFhirSdk.ModelContracts", "MyFhirSdk.ModelSupport"} and not hint:
            raise ValueError(f"Unresolved implementation HintPath in {project}: {item['Identity']}")
        dll_references.append({"identity": item["Identity"],
                               "hintPath": portable((ROOT / project).parent / hint) if hint else None})
    return {"project": project, "properties": data["Properties"],
            "evaluation": {"Configuration": "Release", **paths},
            "dllReferences": dll_references,
            "items": {**{kind: sorted(portable(i["FullPath"]) for i in data["Items"][kind])
                         for kind in ("Compile", "ProjectReference")},
                      "Reference": sorted(r["hintPath"] or r["identity"] for r in dll_references)}}


def regression_inventory(directory):
    reports = []
    for path in sorted(directory.glob("*.trx")):
        root = ET.parse(path).getroot()
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        methods = root.findall(".//t:UnitTest/t:TestMethod", ns)
        assemblies = {Path(m.attrib["codeBase"].replace("\\", "/")).stem for m in methods}
        if len(assemblies) != 1:
            raise ValueError(f"Expected one test assembly in {path.name}")
        results = root.findall(".//t:UnitTestResult", ns)
        counts = Counter(r.attrib["outcome"] for r in results)
        if not results or any(k not in {"Passed", "Failed", "NotExecuted"} for k in counts):
            raise ValueError(f"Missing or unexpected test results: {path.name}")
        if not counts["Passed"]:
            raise ValueError(f"Test assembly executed no passing tests: {path.name}")
        counters = root.find("t:ResultSummary/t:Counters", ns).attrib
        if int(counters["total"]) != len(results) or int(counters["passed"]) != counts["Passed"] or int(counters["failed"]) != counts["Failed"]:
            raise ValueError(f"TRX counter mismatch: {path.name}")
        reports.append({"assembly": assemblies.pop(), "trx": path.relative_to(ROOT).as_posix(),
                        "sha256": sha(path), "total": len(results), "passed": counts["Passed"],
                        "failed": counts["Failed"], "skipped": counts["NotExecuted"],
                        "skips": [{"test": r.attrib["testName"], "reason": " ".join(r.itertext()).strip()}
                                  for r in results if r.attrib["outcome"] == "NotExecuted"]})
    if len(reports) != len(EXPECTED_TESTS) or {r["assembly"] for r in reports} != EXPECTED_TESTS:
        raise ValueError("Regression did not produce exactly the expected assemblies")
    return reports


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default="artifacts/fhir-versions/v0/run")
    parser.add_argument("--inventory-only", action="store_true", help="Skip regression; G0 stays Pending")
    args = parser.parse_args()
    output = (ROOT / args.output).resolve()
    if not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("Use a fresh directory strictly under artifacts/")
    output.mkdir(parents=True)
    summary = {"schemaVersion": 1, "gate": "G0", "status": "Pending", "r4ProductionSupport": False}
    try:
        summary["sourceRevision"] = run(["git", "rev-parse", "HEAD"])
        summary["branch"] = run(["git", "branch", "--show-current"])
        summary["workingTreeDirty"] = bool(run(["git", "status", "--porcelain"]))
        summary["createdAtUtc"] = datetime.now(timezone.utc).isoformat()
        summary["sdkVersion"] = run(["dotnet", "--version"])
        if summary["sdkVersion"] != json.loads((ROOT / "global.json").read_text())["sdk"]["version"]:
            raise ValueError("Pinned SDK mismatch")
        summary.update(os=platform.system(), architecture=platform.machine(), pythonVersion=platform.python_version(),
                       targetFramework="net9.0", configuration="Release")
        temp = Path(tempfile.gettempdir()).resolve()
        summary["testTempDirectory"] = temp.relative_to(ROOT).as_posix() if temp.is_relative_to(ROOT) else "system-temp"
        files = source_inventory()
        summary["sourceFingerprint"] = fingerprint(files)
        summary["runnerSha256"] = sha(Path(__file__))
        summary["powershellVersion"] = None if args.inventory_only else run([
            "pwsh", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"])
        decisions = ROOT / "docs/fhir_versions/V0_Decisions.md"
        if not decisions.is_file():
            raise ValueError("V0 decisions are required before recording G0 evidence")
        summary["decisionsSha256"] = sha(decisions)
        write(output / "source-inventory.json", files)
        write(output / "r5-generated-inventory.json", [f for f in files if f["path"].startswith("Generated/R5/")])
        write(output / "policy-inventory.json", [f for f in files if f["path"].startswith("CodeGen/Policy/")])
        write(output / "approved-api-inventory.json", [f for f in files if f["path"] in {
            "Tests/Architecture/ApprovedPublicApi.txt", "Tests/Architecture/ApprovedR5ModelApi.txt"}])
        for version in ("r4", "r5"):
            lock = json.loads((ROOT / f"CodeGen/Policy/{version}-package-lock.json").read_text())
            inventory = package_inventory(lock)
            write(output / f"{version}-definitions.json", inventory)
            summary[f"{version}Input"] = {k: inventory[k] for k in ("sha256", "archiveBytes", "structureDefinitions", "byKindAndDerivation", "byDisposition")}
        projects = []
        for file in files:
            if not file["path"].endswith(".csproj"):
                continue
            projects.append(project_inventory(file["path"]))
        write(output / "project-inventory.json", projects)
        findings = []
        pattern = re.compile(r"R5ModelMetadataProvider\.Default|PrimitiveRegistry\.Default|AssemblyQualifiedName|Type\.GetType|Assembly\.Load|SimpleQuantity|MyFhirSdk\.Resources|new Fhir(JsonParser|JsonSerializer|Validator)")
        for file in files:
            if file["path"].endswith((".cs", ".json", ".csproj")) and not file["path"].startswith(("Generated/", "docs/")):
                for number, line in enumerate((ROOT / file["path"]).read_text(encoding="utf-8-sig").splitlines(), 1):
                    if pattern.search(line):
                        findings.append({"path": file["path"], "line": number, "text": line.strip()})
        write(output / "consumer-findings.json", findings)
        run(["dotnet", "msbuild", "eng/MyFhirSdk.CodeGen.Build.proj", "/t:Pack", "/p:Configuration=Release",
             f"/p:PackageOutputPath={output / 'package'}"], output / "pack.log")
        reference = ROOT / "Runtime/obj/Release/net9.0/ref/MyFhirSdk.Runtime.dll"
        descriptor = json.loads((ROOT / "CodeGen/Policy/runtime-contract.json").read_text())
        tool_version = descriptor["compatibility"]["toolVersion"]
        package = output / f"package/MyFhirSdk.CodeGen.Tool.{tool_version}.nupkg"
        expected = descriptor["compilerReference"]["sha256"]
        if sha(reference) != expected:
            raise ValueError("Canonical compiler reference mismatch")
        summary["tool"] = {"packageId": "MyFhirSdk.CodeGen.Tool", "version": tool_version, "sha256": sha(package),
                           "descriptorSha256": sha(ROOT / "CodeGen/Policy/runtime-contract.json"),
                           "compilerReferenceSha256": sha(reference), "contract": descriptor["contractVersion"]}
        run(["dotnet", "build", "MyFhirSdk.csproj", "-c", "Release", "--no-restore"], output / "sdk-build.log")
        run(["dotnet", "build", "Tests/KernelMigration/Inventory/Inventory.csproj", "-c", "Release", "-p:NuGetAudit=false"], output / "api-build.log")
        summary["inventoryToolNuGetAudit"] = False  # No PackageReferences; avoid an unnecessary network request.
        api_tool = ROOT / "Tests/KernelMigration/Inventory/bin/Release/net9.0/Inventory.dll"
        sdk = ROOT / "bin/Release/net9.0/MyFhirSdk.dll"
        runtime = ROOT / "bin/Release/net9.0/MyFhirSdk.Runtime.dll"
        contracts = ROOT / "bin/Release/net9.0/MyFhirSdk.ModelContracts.dll"
        dependencies = [str(runtime)]
        if (ROOT / "ModelContracts/MyFhirSdk.ModelContracts.csproj").is_file():
            dependencies.append(str(contracts))
        if (ROOT / "Models/R5/MyFhirSdk.R5.Models.csproj").is_file():
            dependencies.extend(str(ROOT / "bin/Release/net9.0" / name) for name in ("MyFhirSdk.ModelSupport.dll", "MyFhirSdk.R5.Models.dll"))
        for name in ("public-api.txt", "public-api-repeat.txt"):
            run(["dotnet", str(api_tool), str(sdk), str(output / name), *dependencies], output / f"{name}.log")
        if sha(output / "public-api.txt") != sha(output / "public-api-repeat.txt"):
            raise ValueError("Non-deterministic API inventory")
        summary["publicApiSha256"] = sha(output / "public-api.txt")
        summary["implementationHashes"] = {p.name: sha(p) for p in (sdk, runtime)}
        summary["externalClientSmoke"] = {"endpointConfigured": bool(os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL")),
                                         "endpointSha256": hashlib.sha256(os.getenv("MYFHIRSDK_INTEGRATION_BASE_URL", "").encode()).hexdigest(),
                                         "environmentVariable": "MYFHIRSDK_INTEGRATION_BASE_URL"}
        if not args.inventory_only:
            test_error = None
            try:
                run(["dotnet", "test", "MyFhirSdk.sln", "-c", "Release", "--no-restore", "--logger", "trx",
                     "--results-directory", str(output / "regression")], output / "regression.log")
            except RuntimeError as error:
                test_error = error
            summary["regression"] = regression_inventory(output / "regression")
            summary["testCounts"] = {k: sum(r[k] for r in summary["regression"]) for k in ("total", "passed", "failed", "skipped")}
            if test_error or summary["testCounts"]["failed"]:
                raise RuntimeError("Regression failed; see regression.log and per-assembly results")
        if fingerprint(source_inventory()) != summary["sourceFingerprint"]:
            raise ValueError("Source changed during baseline run; rerun in a stable checkout")
        summary["status"] = "Pending" if args.inventory_only else "Passed"
        print(f"G0 {summary['status']}: {output.relative_to(ROOT).as_posix()}/summary.json")
    except Exception as error:
        summary["status"] = "Failed"
        summary["error"] = str(error).replace(str(ROOT), "<checkout>")
        raise
    finally:
        write(output / "summary.json", summary)


if __name__ == "__main__":
    main()
