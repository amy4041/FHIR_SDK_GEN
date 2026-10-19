"""Compute G5 scope from the locked official package and verify every generated declaration."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import tarfile

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(root=ROOT):
    lock = read(root / "CodeGen/Policy/r4-package-lock.json")
    archive = root / lock["archivePath"]
    if sha(archive) != lock["sha256"]:
        raise ValueError("Official R4 package hash differs from lock")
    ownership = read(root / "CodeGen/Policy/R4/r4-model-ownership-policy.json")
    external = {d["canonical"]: d for d in ownership["externalDefinitionNodes"]}
    primitive = read(root / "Generated/R4/Primitives/primitive-generation-manifest.json")
    wrappers = {p["canonical"]: p for p in primitive["primitives"]}
    manifest = read(root / "Generated/R4/model-generation-manifest.json")
    for item in manifest["artifactInventory"]["artifacts"]:
        if sha(root / item["path"]) != item["sha256"]:
            raise ValueError("Model manifest artifact mismatch: " + item["path"])
    if manifest["package"]["sha256"] != lock["sha256"] or manifest["generationScope"] != {"mode": "full", "selectedCanonicals": []}:
        raise ValueError("A complete locked R4 model batch is required")
    declarations = {}
    for path in sorted((root / "Generated/R4").rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        namespace = re.search(r"namespace ([\w.]+);", text)[1]
        declaration = re.search(r"public (?:sealed |abstract )?class (\w+)", text)
        if declaration:
            name = namespace + "." + declaration[1]
            if name in declarations:
                raise ValueError("Duplicate declaration: " + name)
            declarations[name] = path.relative_to(root).as_posix()
    rows, generated = [], []
    with tarfile.open(archive, "r:gz") as package:
        for member in package:
            if not member.name.startswith("package/StructureDefinition-") or not member.name.endswith(".json"):
                continue
            raw = package.extractfile(member).read()
            definition = json.loads(raw)
            canonical = definition["url"]
            kind, derivation = definition.get("kind"), definition.get("derivation")
            row = {"id": definition["id"], "canonical": canonical, "kind": kind, "derivation": derivation,
                   "sourceSha256": hashlib.sha256(raw).hexdigest()}
            if canonical in external:
                row.update(disposition="runtime-bootstrap", reason="Explicit release ownership policy; shared Runtime declaration",
                           clrType=external[canonical]["clrType"])
            elif canonical == "http://hl7.org/fhir/StructureDefinition/SimpleQuantity":
                row.update(disposition="constraint-auxiliary", reason="Explicit profile-narrowed Quantity asset",
                           clrType="MyFhirSdk.R4.Types.SimpleQuantity", path="Models/R4/SimpleQuantity.cs")
            elif kind == "primitive-type" and derivation == "specialization":
                wrapper = wrappers[canonical]
                if wrapper["supportStatus"] == "supported":
                    clr = "MyFhirSdk.R4.Primitives." + wrapper["wrapperName"]
                    row.update(disposition="generated-primitive", reason="Approved primitive policy", clrType=clr, path=declarations[clr])
                else:
                    row.update(disposition="unsupported-primitive", reason=wrapper["unsupportedReason"])
            elif kind in {"resource", "complex-type"} and derivation == "specialization":
                namespace = "Resources" if kind == "resource" else "Types"
                clr = "MyFhirSdk.R4." + namespace + "." + definition["type"]
                row.update(disposition="generated-model", reason="Full specialization scope", clrType=clr, path=declarations[clr])
                generated.append(definition)
            elif derivation == "constraint":
                row.update(disposition="excluded-constraint", reason="Profile generation and validation deferred; SimpleQuantity is the explicit exception")
            elif kind == "logical":
                row.update(disposition="excluded-logical", reason="Logical workflow models do not declare concrete JSON resources or datatypes")
            else:
                raise ValueError("Unclassified official definition: " + definition["id"])
            rows.append(row)
    # Top-level definitions and every nested backbone are independently accounted for.
    roots = {r["clrType"] for r in rows if r["disposition"] in {"generated-model", "generated-primitive"}}
    nested = [{"clrType": clr, "path": path, "reason": "Snapshot backbone declaration owned by its generated root"}
              for clr, path in sorted(declarations.items()) if clr not in roots]
    if len(rows) != 655 or len(declarations) != 674 or len(nested) != 473:
        raise ValueError("Unexpected complete R4 definition/declaration inventory")
    choices = read(root / "CodeGen/Policy/R4/r4-choice-open-type-policy.json")
    open_ids = set(choices["classification"]["openTypeElementIds"])
    # The official differential is the authoritative direct declaration inventory,
    # including backbone elements; effective inherited occurrences remain in snapshot.
    direct = [e for d in generated for e in d["differential"]["element"][1:]]
    effective = [e for d in generated for e in d["snapshot"]["element"][1:]]
    choice_elements = [e for e in direct if e["path"].endswith("[x]")]
    open_elements = [e for e in choice_elements if e["id"] in open_ids]
    ordinary = [e for e in choice_elements if e["id"] not in open_ids]
    stats = {
        "generatedDefinitions": len(generated), "generatedResourceDefinitions": sum(d["kind"] == "resource" for d in generated),
        "generatedDatatypeDefinitions": sum(d["kind"] == "complex-type" for d in generated),
        "directElements": len(direct), "effectiveElementsExcludingRoots": len(effective),
        "directRequiredElements": sum(e.get("min", 0) > 0 for e in direct),
        "directConstraints": sum(len(e.get("constraint", [])) for e in direct),
        "directBindings": sum("binding" in e for e in direct),
        "directBindingStrengths": dict(sorted(Counter(e["binding"]["strength"] for e in direct if "binding" in e).items())),
        "directChoices": len(choice_elements), "directChoiceAlternatives": sum(len(e.get("type", [])) for e in choice_elements),
        "ordinaryChoices": len(ordinary), "openChoices": len(open_elements),
        "requiredOrdinaryChoices": sum(e.get("min", 0) > 0 for e in ordinary),
        "requiredOpenChoices": sum(e.get("min", 0) > 0 for e in open_elements),
        "directFixedValues": sum(any(k.startswith("fixed") for k in e) for e in direct),
        "directPatternValues": sum(any(k.startswith("pattern") for k in e) for e in direct),
        "directSlicing": sum("slicing" in e for e in direct),
        "finiteCollectionMaxima": sum(e.get("max") not in (None, "0", "1", "*") for e in direct),
    }
    for name in ("r4-choice-open-type-policy.json", "r4-validation-capability-policy.json"):
        approved = read(root / "CodeGen/Policy/R4" / name)["approvedR4Inventory"]
        if {k: v for k, v in approved.items() if k != "source"} != stats:
            raise ValueError("R4 policy inventory differs from locked official metadata: " + name)
    return {"schemaVersion": 1, "packageSha256": lock["sha256"], "structureDefinitions": len(rows),
            "byDisposition": dict(sorted(Counter(r["disposition"] for r in rows).items())),
            "generatedModelDeclarations": len(declarations), "modelOwnedDeclarationsIncludingAuxiliary": len(declarations) + 1,
            "definitions": sorted(rows, key=lambda r: r["canonical"]), "nestedDeclarations": nested, "officialMetadataStatistics": stats}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
