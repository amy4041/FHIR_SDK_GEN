"""Current delivery contracts; historical gates use their pinned checkout."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import xml.etree.ElementTree as ET
import zipfile

NEUTRAL = {"MyFhirSdk.dll", "MyFhirSdk.Runtime.dll", "MyFhirSdk.ModelContracts.dll", "MyFhirSdk.ModelSupport.dll"}
COMPOSITIONS = {"R4": ("R4",), "R5": ("R5",), "Dual": ("R4", "R5"), "R4TwCore": ("R4", "IG")}
VERSIONS = {"R4": "4.0.1", "R5": "5.0.0"}
IG_DLL = "MyFhirSdk.ImplementationGuides.TwCore.dll"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_manifest(root):
    # Parse and fingerprint the same bytes, rather than two independent reads.
    data = (root / "delivery-manifest.json").read_bytes()
    return json.loads(data.decode("utf-8")), hashlib.sha256(data).hexdigest()


def assert_manifest_unchanged(root, expected_hash):
    if sha(root / "delivery-manifest.json") != expected_hash:
        raise ValueError("Delivery manifest changed during verification")


def inventory(root):
    return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob("*")) if p.is_file()}


def composition(name):
    features = COMPOSITIONS[name]
    dlls = NEUTRAL | {f"MyFhirSdk.{r}.Models.dll" for r in features if r != "IG"}
    if "IG" in features:
        dlls |= {IG_DLL}
    return {"fhirVersions": {r: VERSIONS[r] for r in features if r != "IG"},
            "requiredDlls": sorted(dlls), "exampleProject": f"examples/{name}/{name}Example.csproj",
            "igPackages": [{"id": "tw.gov.mohw.twcore#1.0.0", "fhirVersion": "4.0.1", "scope": "manual-three-rule-Patient-POC"}] if "IG" in features else []}


def safe_path(root, name):
    if (not isinstance(name, str) or not name or "\\" in name or ":" in name
            or PurePosixPath(name).is_absolute() or any(p in ("", ".", "..") for p in name.split("/"))):
        raise ValueError("Unsafe manifest path: " + str(name))
    path = (root / name).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("Manifest path escaped delivery: " + name)
    return path


def validate_manifest(root, manifest):
    if manifest.get("schemaVersion") != 2 or not manifest.get("compositions"):
        raise ValueError("Expected delivery schema 2 and explicit compositions")
    for name in manifest.get("files", {}):
        safe_path(root, name)
    required = set()
    for name, record in manifest["compositions"].items():
        if name not in COMPOSITIONS or record != composition(name):
            raise ValueError("Unapproved composition: " + name)
        required.update("sdk/" + n for n in record["requiredDlls"])
    files = manifest.get("files", {})
    if not required <= files.keys() or {p for p in files if p.startswith("sdk/")} != required:
        raise ValueError("Delivery DLL set differs from selected compositions")
    actual = inventory(root)
    actual.pop("delivery-manifest.json", None)
    if set(actual) != set(files):
        raise ValueError("Unlisted or missing delivery files")
    for name, digest in files.items():
        path = safe_path(root, name)
        if re.fullmatch(r"[0-9a-f]{64}", str(digest)) is None or sha(path) != digest:
            raise ValueError("Delivery integrity check failed: " + name)
    if set(manifest.get("assemblyIdentities", {})) != {Path(p).name for p in required}:
        raise ValueError("Missing assembly identities")
    for name, identity in manifest["assemblyIdentities"].items():
        if not identity.startswith(Path(name).stem + ", Version="):
            raise ValueError("Assembly identity mismatch: " + name)
    tool = safe_path(root, manifest["toolPackage"])
    if sha(tool) != manifest["toolPackageSha256"]:
        raise ValueError("Tool package hash mismatch")
    descriptor = json.loads((root / "contracts/runtime-contract.json").read_text(encoding="utf-8"))
    if (manifest["dotnetSdk"] != json.loads((root / "global.json").read_text(encoding="utf-8"))["sdk"]["version"]
            or manifest["targetFramework"] != descriptor["targetFramework"]
            or manifest["toolVersion"] != descriptor["compatibility"]["toolVersion"]
            or manifest["runtimeCompilerReferenceSha256"] != descriptor["compilerReference"]["sha256"]):
        raise ValueError("Delivery toolchain metadata differs from shipped contracts")
    releases = {r for name in manifest["compositions"] for r in COMPOSITIONS[name] if r != "IG"}
    if len(manifest.get("fhirInputs", [])) != len(releases) or {r["release"] for r in manifest["fhirInputs"]} != releases:
        raise ValueError("Missing FHIR input metadata")
    for record in manifest["fhirInputs"]:
        release = record["release"]
        lock = json.loads((root / "inputs" / (release.lower() + "-package-lock.json")).read_text(encoding="utf-8"))
        expected = {"release": release, "packageId": f"hl7.fhir.{release.lower()}.core", "version": VERSIONS[release], "sha256": lock["sha256"]}
        archive = root / "inputs" / (expected["packageId"] + "-" + expected["version"] + ".tgz")
        if record != expected or sha(archive) != record["sha256"]:
            raise ValueError("FHIR input metadata or archive mismatch")
    return required


def validate_tool(package, descriptor):
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        spec = ET.fromstring(archive.read(next(n for n in names if n.endswith(".nuspec"))))
        if (spec.find(".//{*}id").text != "MyFhirSdk.CodeGen.Tool" or
                spec.find(".//{*}version").text != descriptor["compatibility"]["toolVersion"]):
            raise ValueError("Unexpected tool package identity")
        references = [n for n in names if n.endswith("RuntimeReferences/net9.0/MyFhirSdk.Runtime.dll")]
        if len(references) != 1 or hashlib.sha256(archive.read(references[0])).hexdigest() != descriptor["compilerReference"]["sha256"]:
            raise ValueError("Tool compiler reference mismatch")
        if any(Path(n).name in NEUTRAL - {"MyFhirSdk.Runtime.dll"} or Path(n).name.endswith(".Models.dll") for n in names):
            raise ValueError("Tool embeds SDK or Models implementations")


def stage_tool(package, feed, descriptor):
    validate_tool(package, descriptor)
    # Local NuGet feeds discover packages by ID/version filename, not arbitrary input names.
    name = f'MyFhirSdk.CodeGen.Tool.{descriptor["compatibility"]["toolVersion"]}.nupkg'
    destination = safe_path(feed, name)
    feed.mkdir(parents=True, exist_ok=True)
    shutil.copy2(package, destination)
    return destination


def validate_execution(name, report, deployed):
    spec = composition(name)
    expected = set(spec["requiredDlls"])
    actual = {p.name for p in deployed.glob("MyFhirSdk*.dll")}
    if actual != expected or report.get("status") != "Passed":
        raise ValueError("Deployment DLL set or execution failed: " + name)
    deps = json.loads((deployed / (name + "Example.deps.json")).read_text(encoding="utf-8"))
    runtime = {Path(n).name for target in deps["targets"].values() for item in target.values()
               for n in item.get("runtime", {}) if Path(n).name.startswith("MyFhirSdk")}
    if runtime != expected or {a["name"] + ".dll" for a in report["assemblies"]} != expected:
        raise ValueError("Dependency manifest/loaded assemblies mismatch: " + name)
    versions = {r["fhirVersion"] for r in report["releases"]}
    if versions != set(spec["fhirVersions"].values()) or len(report["releases"]) != len(versions):
        raise ValueError("Missing deployed release: " + name)
    for record in report["releases"]:
        release = next(r for r, v in VERSIONS.items() if v == record["fhirVersion"])
        if record["modelAssembly"] != f"MyFhirSdk.{release}.Models" or any(record.get(k) is not True for k in ("roundTrip", "validation", "typedClient")):
            raise ValueError("Incomplete release behavior: " + name)
    for assembly in report["assemblies"]:
        path = Path(assembly["path"]).resolve()
        if path != (deployed / (assembly["name"] + ".dll")).resolve():
            raise ValueError("Assembly loaded outside deployment")
        if assembly["name"] == "MyFhirSdk" and {r + ".dll" for r in assembly["references"] if r.startswith("MyFhirSdk")} != NEUTRAL - {"MyFhirSdk.dll"}:
            raise ValueError("SDK references concrete Models or IG")
    if "IG" in COMPOSITIONS[name] and (not report.get("ig") or any(report["ig"].get(k) is not True for k in ("positive", "negative", "replacement"))):
        raise ValueError("Incomplete IG replacement behavior")
