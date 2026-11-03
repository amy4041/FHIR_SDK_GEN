#!/usr/bin/env python3
"""D0 pinned-source/API evidence and MSBuild inventory; never executes external consumers."""
import argparse
from collections import Counter
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tarfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
PIN = "f52c64a2b5c4b7db81a254a1286dda626f8c91e7"
BASELINE = ROOT / "docs/sdk_decoupling/baselines/d0"
PATTERNS = {
    "default-engine": r"\bnew\s+(?:[\w.]+\.)?Fhir(?:JsonParser|JsonSerializer|Validator|ResourceTypeResolver)\s*\(\s*\)",
    "r5-context": r"\bR5ModelContext\b",
    "metadata-adapter": r"\bR5ModelMetadataProvider\b",
    "registry-default": r"\bPrimitiveRegistry\s*\.\s*Default\b",
    "legacy-client": r"\b(?:IFhirClient|FhirClient)\b(?!\s*<|Exception|Options)",
    "legacy-handler": r"\b(?:IFhirResponseHandler|FhirResponseHandler)\b",
    "twcore": r"\b(?:TwCore\w*|TWCORE-PAT-\d+)\b",
    "source-compiler": r"\b(?:CSharpCompilation|ReadSources|ParseText|CreateSdk|RealSdkSourceCompiler)\b",
    "r5-binding": r"MyFhirSdk\.(?:Resources|DataTypes|ModelMetadata\.R5)\b|Models[/\\]R5|\bR5ModelProvider\b",
}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def text_bytes(data):
    data.decode("utf-8")  # Only the known UTF-8 contracts may normalize checkout EOL.
    return data.replace(b"\r\n", b"\n")


def source_fingerprint(sources):
    rows = []
    for path, data in sorted(sources.items()):
        try:
            data.decode("utf-8")
            if b"\0" not in data:
                data = data.replace(b"\r\n", b"\n")
        except UnicodeDecodeError:
            pass
        rows.append(f"{path}|{sha(data)}\n")
    return {"algorithm": "sha256-path-and-utf8-lf-bytes-v1", "fileCount": len(rows),
            "sha256": sha("".join(rows).encode())}


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def pinned_sources():
    # Disable checkout EOL conversion: this evidence has the same bytes on both OSes.
    archive = git("-c", "core.autocrlf=false", "-c", "core.eol=lf", "archive", PIN)
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        return {m.name: tar.extractfile(m).read() for m in tar if m.isfile()}


def inventory(sources):
    return [{"path": p, "bytes": len(b), "sha256": sha(b)} for p, b in sorted(sources.items())]


def category(path):
    if path.startswith("Tests/KernelMigration/BaselineConsumer/v1/"):
        return "frozen-consumer"
    if "/baselines/" in path:
        return "historical-evidence"
    if path.startswith("docs/") or path == "README.md":
        return "documentation"
    if path.startswith("Tests/"):
        return "current-test-or-fixture"
    if path.startswith("examples/"):
        return "current-example"
    if path.startswith("eng/"):
        return "current-harness"
    return "product"


def references(sources):
    rows = []
    for path, data in sorted(sources.items()):
        if Path(path).suffix not in {".cs", ".csproj", ".props", ".py", ".ps1", ".md"}:
            continue
        if path.startswith(("Generated/", "docs/sdk_decoupling/", "Tests/SdkDecoupling/")) or path == "eng/Test-SdkDecoupling.py":
            continue
        for line, text in enumerate(data.decode("utf-8-sig", errors="replace").splitlines(), 1):
            for kind, pattern in PATTERNS.items():
                if re.search(pattern, text):
                    rows.append({"path": path, "line": line, "category": category(path), "kind": kind})
    return rows


def evidence(sources):
    files = inventory(sources)
    generated = [r for r in files if r["path"].startswith("Generated/")]
    contracts = [r for r in files if (
        Path(r["path"]).name.startswith("Approved") or
        r["path"].endswith(("runtime-contract.json", "compatibility-matrix.json")) or
        r["path"] in {"global.json", ".config/dotnet-tools.json", "CodeGen/MyFhirSdk.CodeGen.csproj",
                      "CodeGen/Assets/RuntimeReferenceSet.cs", "CodeGen/Assets/RuntimeReferenceService.cs",
                      "CodeGen/Assets/RuntimeReferenceMetadataReader.cs", "Runtime/KernelCompileItems.props",
                      "eng/Test-CodeGenToolchainContract.ps1", "eng/Test-CompilerReferenceIsolation.ps1"})]
    return {"schemaVersion": 1, "revision": PIN, "files": files, "generated": generated,
            "contracts": contracts, "references": references(sources)}


def current_sources():
    paths = git("ls-files", "--cached", "--others", "--exclude-standard", "--deduplicate", "-z").decode().split("\0")
    return {p: (ROOT / p).read_bytes() for p in sorted(set(paths)) if p and (ROOT / p).is_file()
            and not p.startswith("docs/sdk_decoupling/baselines/")}


def validate_ci(value):
    if (value.get("head_sha") != PIN or value.get("status") != "completed" or
            value.get("conclusion") != "success" or not value.get("jobs") or
            any(j.get("conclusion") != "success" for j in value["jobs"])):
        raise ValueError("CI evidence is not a successful run of the pinned revision")


def protected_drift(frozen, current, pinned):
    expected = {r["path"]: r["sha256"] for r in frozen["generated"] + frozen["contracts"]}
    contracts = {r["path"] for r in frozen["contracts"]}
    expected.update({p: sha(text_bytes(pinned[p])) for p in contracts})
    paths = set(expected) | {p for p in current if p.startswith("Generated/")}
    return sorted(p for p in paths if p not in current or p not in expected or
                  sha(text_bytes(current[p]) if p in contracts else current[p]) != expected[p])


def evaluate_sdk(root):
    result = subprocess.run(["dotnet", "msbuild", str(root / "MyFhirSdk.csproj"), "-nologo",
                             "-p:Configuration=Release", "-getItem:Compile,ProjectReference"],
                            cwd=root, capture_output=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError("MSBuild item evaluation failed: " + result.stdout + result.stderr)
    items = json.loads(result.stdout)["Items"]
    def paths(name):
        return sorted(Path(i["FullPath"]).resolve().relative_to(root.resolve()).as_posix()
                      for i in items[name])
    return {"compile": paths("Compile"), "projectReferences": paths("ProjectReference")}


def target_gaps(sources, evaluated):
    # Structural observations, not a substitute for D3 assembly/deployment tests.
    gaps = []
    if any(p.startswith("Models/") for p in evaluated["projectReferences"]):
        gaps.append("sdk-references-version-models")
    if any(p.startswith("ImplementationGuides/") for p in evaluated["compile"]):
        gaps.append("sdk-compiles-concrete-ig")
    for path in ("ModelMetadata/R5/R5ModelContext.cs", "ModelMetadata/R5/R5ModelMetadataProvider.cs",
                 "Client/FhirClient.cs", "Client/Abstractions/IFhirClient.cs",
                 "Client/Responses/FhirResponseHandler.cs", "Client/Abstractions/IFhirResponseHandler.cs"):
        if path in sources:
            gaps.append("retire:" + path)
    for path, name in (("Serialization/Json/FhirJsonParser.cs", "FhirJsonParser"),
                       ("Serialization/Json/FhirJsonSerializer.cs", "FhirJsonSerializer"),
                       ("Validation/FhirValidator.cs", "FhirValidator"),
                       ("Client/Requests/FhirResourceTypeResolver.cs", "FhirResourceTypeResolver")):
        if re.search(r"public\s+" + name + r"\s*\(\s*\)", sources[path].decode()):
            gaps.append("default-constructor:" + name)
    if "PrimitiveRegistry.Default" in sources["Validation/FhirValidator.cs"].decode():
        gaps.append("internal-registry-fallback")
    if "ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj" not in sources:
        gaps.append("missing-independent-twcore-project")
    return gaps


def capture_api(pinned, output):
    root = output / "pinned-source"
    root.mkdir()
    for name, data in pinned.items():
        path = root / name
        if not path.resolve().is_relative_to(root.resolve()):
            raise ValueError("Archive path escapes pinned source")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def run(args, log):
        result = subprocess.run(args, cwd=root, capture_output=True, encoding="utf-8", errors="replace")
        (output / log).write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode:
            raise RuntimeError(f"API capture failed; see {output / log}")

    for project, log in (("MyFhirSdk.csproj", "api-sdk-build.log"),
                         ("Tests/KernelMigration/Inventory/Inventory.csproj", "api-tool-build.log")):
        run(["dotnet", "build", project, "-c", "Release", "-p:NuGetAudit=false", "--nologo"], log)
    binary = root / "bin/Release/net9.0"
    run(["dotnet", str(root / "Tests/KernelMigration/Inventory/bin/Release/net9.0/Inventory.dll"),
         str(binary / "MyFhirSdk.dll"), str(output / "all-assemblies-api.txt"),
         *[str(binary / (n + ".dll")) for n in
           ("MyFhirSdk.Runtime", "MyFhirSdk.ModelContracts", "MyFhirSdk.ModelSupport", "MyFhirSdk.R5.Models")]],
        "api-inventory.log")
    lines = (output / "all-assemblies-api.txt").read_text(encoding="utf-8").splitlines()
    # Keep all SDK exported types/members, with defining-assembly qualifiers.
    sdk = [s for s in lines if s.startswith(("ASSEMBLY MyFhirSdk,", "TYPE [MyFhirSdk]", "[MyFhirSdk]"))]
    twcore = [s for s in sdk if s.startswith(("ASSEMBLY MyFhirSdk,",
              "TYPE [MyFhirSdk]MyFhirSdk.ImplementationGuides.TwCore.",
              "[MyFhirSdk]MyFhirSdk.ImplementationGuides.TwCore."))]
    if not any(s.startswith("TYPE ") for s in twcore):
        raise ValueError("Full API capture omitted TW Core exported types")
    return {"full-sdk-api.txt": ("\n".join(sdk) + "\n").encode(),
            "twcore-api.txt": ("\n".join(twcore) + "\n").encode()}


def verify_full_api(captured, pinned, record=False):
    metadata_path = BASELINE / "full-api.json"
    metadata = {"schemaVersion": 1, "revision": PIN,
                "inventoryToolSources": inventory({p: b for p, b in pinned.items()
                    if p.startswith("Tests/KernelMigration/Inventory/")}),
                "snapshots": inventory(captured)}
    if record:
        if metadata_path.exists() or any((BASELINE / p).exists() for p in captured):
            raise ValueError("Full API baseline already exists; refusing to overwrite")
        for name, data in captured.items():
            (BASELINE / name).write_bytes(data)
        write(metadata_path, metadata)
    if json.loads(metadata_path.read_text(encoding="utf-8")) != metadata:
        raise ValueError("Full API metadata differs from the pinned rebuild")
    for name, data in captured.items():
        if (BASELINE / name).read_bytes() != data:
            raise ValueError("Full API snapshot drift: " + name)


def external_inventory(root):
    # Allowlist only source/projects and generation manifests; never serialize source lines,
    # endpoints, credentials, appsettings, binaries, or execute the application.
    rows = []
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root)
        if any(p.lower() in {"bin", "obj", ".git", ".vs"} for p in relative.parts):
            continue
        if any(p.startswith(".") for p in relative.parts):
            continue
        if path.is_file() and (path.suffix == ".csproj" or
                (path.suffix == ".cs" and "out" not in relative.parts) or
                path.name in {"model-generation-manifest.json", "primitive-generation-manifest.json"}):
            data = path.read_bytes()
            refs = references({relative.as_posix(): data})
            rows.append({"path": relative.as_posix(), "bytes": len(data), "sha256": sha(data),
                         "references": [{"line": r["line"], "kind": r["kind"]} for r in refs]})
    if not rows:
        raise ValueError("External inventory found no allowed source files")
    return {"mode": "read-only-no-execution", "files": rows}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/sdk-decoupling/d0")
    parser.add_argument("--freeze", action="store_true", help="Create immutable D0 evidence once")
    parser.add_argument("--require-decoupled", action="store_true", help="Fail on observed target gaps; additional D3 runtime checks remain required")
    parser.add_argument("--external-root", type=Path)
    parser.add_argument("--record-api-baseline", action="store_true",
                        help="Record the full SDK/TW Core API from a pinned-source rebuild once")
    args = parser.parse_args()
    # Evidence must never be written into the external read-only project.
    output = args.output.resolve()
    if output == (ROOT / "artifacts").resolve() or not output.is_relative_to((ROOT / "artifacts").resolve()) or output.exists():
        parser.error("--output must be a fresh directory strictly below repository artifacts/")
    output.mkdir(parents=True)
    summary = {"gate": "D0", "status": "Pending", "baselineRevision": PIN,
               "startedAtUtc": datetime.now(timezone.utc).isoformat()}
    write(output / "summary.json", summary)
    try:
        return run_checks(args, output, summary)
    except Exception as error:
        summary.update(status="Failed", error=str(error))
        write(output / "summary.json", summary)
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 1


def run_checks(args, output, summary):
    pinned = pinned_sources()
    validate_ci(json.loads((BASELINE / "ci.json").read_text(encoding="utf-8")))
    frozen = evidence(pinned)
    manifest = BASELINE / "inventory.json"
    if args.freeze:
        if manifest.exists():
            raise ValueError("D0 baseline already exists; refusing to overwrite")
        write(manifest, frozen)
        for row in frozen["contracts"]:
            if Path(row["path"]).name.startswith("Approved"):
                target = BASELINE / "snapshots" / row["path"]
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(pinned[row["path"]])
    if json.loads(manifest.read_text(encoding="utf-8")) != frozen:
        raise ValueError("Pinned baseline evidence drift")
    for row in frozen["contracts"]:
        if Path(row["path"]).name.startswith("Approved"):
            if sha((BASELINE / "snapshots" / row["path"]).read_bytes()) != row["sha256"]:
                raise ValueError("Frozen API snapshot drift: " + row["path"])
    captured = capture_api(pinned, output)
    verify_full_api(captured, pinned, args.record_api_baseline)
    for name, data in captured.items():
        (output / name).write_bytes(data)
    current = current_sources()
    refs = references(current)
    write(output / "source-inventory.json", inventory(current))
    write(output / "references.json", refs)
    if args.external_root:
        write(output / "external-inventory.json", external_inventory(args.external_root.resolve(strict=True)))
    evaluated = evaluate_sdk(ROOT)
    write(output / "sdk-msbuild-items.json", evaluated)
    gaps = target_gaps(current, evaluated)
    # D0 protects product and contracts; later D stages intentionally update these.
    drift = protected_drift(frozen, current, pinned)
    summary.update({"gate": "D0", "sourceRevision": git("rev-parse", "HEAD").decode().strip(),
               "baselineRevision": PIN, "dirty": bool(git("status", "--porcelain")),
               "sourceFingerprint": source_fingerprint(current),
               "status": "Failed" if drift or (args.require_decoupled and gaps) else "Passed",
               "scope": "source-pin-and-inventory-only", "fullyDecoupled": False,
               "fullApiVerifiedAgainstPinnedRebuild": True,
               "referenceCounts": dict(sorted(Counter(r["category"] for r in refs).items())),
               "protectedDrift": drift, "targetGaps": gaps})
    write(output / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 1 if summary["status"] == "Failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())
