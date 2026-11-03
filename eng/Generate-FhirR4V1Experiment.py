#!/usr/bin/env python3
"""Isolated V1 prototype, not the production CodeGen CLI or an R4 policy.

Generate the entire structural dependency closure of three roots, including
bootstrap/Extension dependencies. No targetProfile expansion into unrelated
resources: Reference carries canonical targets, not embedded CLR resources.
Common primitive declarations/codecs reuse explicitly pinned R5 assets as an
experiment only; official R4 definitions determine membership and model fields.
"""
import argparse
import hashlib
import json
from pathlib import Path
import tarfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
MODELS = "Tests/FhirVersions/V1/Models/Generated"
COMPOSITION = "Tests/Architecture/V1/Generated/R4ExperimentComposition.g.cs"
MANIFEST = "Tests/FhirVersions/V1/closure.json"
NS = "MyFhirSdk.R4"
SEEDS = ["Patient", "Observation", "Attachment"]


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def pascal(name):
    return name[0].upper() + name[1:]


def code(t):
    name = t["code"]
    if name.startswith("http://hl7.org/fhirpath/System."):
        names = [e.get("valueUrl", e.get("valueUri")) for e in t.get("extension", [])
                 if e.get("url") == "http://hl7.org/fhir/StructureDefinition/structuredefinition-fhir-type"]
        if len(names) != 1 or not names[0]:
            raise ValueError(f"Unmapped FHIRPath system type: {name}")
        return names[0]
    return name


def generate():
    lock = json.loads((ROOT / "CodeGen/Policy/r4-package-lock.json").read_text())
    raw = (ROOT / lock["archivePath"]).read_bytes()
    if digest(raw) != lock["sha256"]:
        raise ValueError("R4 archive hash mismatch")
    definitions, hashes = {}, {}
    with tarfile.open(ROOT / lock["archivePath"]) as tar:
        metadata = json.load(tar.extractfile("package/package.json"))
        if (metadata["name"], metadata["version"], metadata["fhirVersions"]) != (
                "hl7.fhir.r4.core", "4.0.1", ["4.0.1"]):
            raise ValueError("R4 package identity mismatch")
        for member in tar.getmembers():
            if member.name.startswith("package/StructureDefinition-") and member.name.endswith(".json"):
                data = tar.extractfile(member).read()
                d = json.loads(data)
                definitions[d["id"]] = d
                hashes[d["id"]] = digest(data)
    bootstrap = {Path(e.attrib["Include"].replace("\\", "/")).stem
                 for e in ET.parse(ROOT / "Runtime/KernelCompileItems.props").findall(".//RuntimeKernelSource")}
    primitive_policy_path = "CodeGen/Policy/primitive-generation-policy.json"
    primitive_policy = json.loads((ROOT / primitive_policy_path).read_text())
    primitives = {p["fhirTypeName"]: p for p in primitive_policy["primitives"] if p["supportStatus"] == "supported"}
    closure, edges, queue = set(), set(), list(SEEDS)
    while queue:
        name = queue.pop()
        if name in closure:
            continue
        if name not in definitions:
            raise ValueError(f"Missing dependency: {name}")
        d = definitions[name]
        closure.add(name)
        if name == "xhtml":
            continue  # Retained explicitly as shared Narrative.Div, not a wrapper.
        dependencies = set()
        if d.get("baseDefinition"):
            dependencies.add(d["baseDefinition"].rsplit("/", 1)[-1])
        for e in d.get("snapshot", {}).get("element", []):
            if d.get("kind") == "primitive-type" and e["path"] == name + ".value":
                continue  # Primitive raw CLR value is provided by the pinned template.
            for t in e.get("type", []):
                dependencies.add(code(t))
                dependencies.update(p.rsplit("/", 1)[-1] for p in t.get("profile", []))
        for dependency in dependencies:
            edges.add((name, dependency))
            queue.append(dependency)
    supported_primitives = sorted(n for n in closure if definitions[n].get("kind") == "primitive-type" and n != "xhtml")
    if any(n not in primitives for n in supported_primitives):
        raise ValueError("R4 primitive missing an explicit common codec mapping")
    models = sorted(n for n in closure if n not in bootstrap and n not in supported_primitives and n != "xhtml")
    if any(definitions[n].get("derivation") != "specialization" and n != "SimpleQuantity" for n in models):
        raise ValueError("Unsupported constraint in selected closure")
    outputs, adaptations, members, class_types = {}, [], {}, {}
    templates = []
    for name in supported_primitives:
        p = primitives[name]
        template_path = f"Generated/R5/Primitives/{p['wrapperName']}.g.cs"
        template = (ROOT / template_path).read_bytes()
        templates.append({"path": template_path, "sha256": digest(template)})
        outputs[f"{MODELS}/Primitives/{p['wrapperName']}.g.cs"] = template.decode().replace(
            "namespace MyFhirSdk.Primitives;", f"namespace {NS}.Primitives;")

    def clr(name):
        if name in supported_primitives:
            return f"global::{NS}.Primitives.{primitives[name]['wrapperName']}"
        if name == "xhtml":
            return "string"  # Only shared Narrative.Div; no xhtml primitive wrapper.
        if name in bootstrap:
            return f"global::MyFhirSdk.Core.{name}"
        if name not in models:
            raise ValueError(f"Type outside closure: {name}")
        category = "Resources" if definitions[name]["kind"] == "resource" else "Types"
        return f"global::{NS}.{category}.{name}"

    for name in models:
        d = definitions[name]
        elements = d.get("snapshot", {}).get("element", [])
        category = "Resources" if d["kind"] == "resource" else "Types"
        paths = {name: name}
        for e in elements:
            if any(code(t) in {"BackboneElement", "Element"} for t in e.get("type", [])) and any(
                    x["path"].startswith(e["path"] + ".") for x in elements):
                paths[e["path"]] = "".join(pascal(p) for p in e["path"].split("."))
        for path, class_name in sorted(paths.items()):
            class_type = f"global::{NS}.{category}.{class_name}"
            class_types[class_type] = name if path == name else path
            if path == name:
                base_name = d["baseDefinition"].rsplit("/", 1)[-1]
                if d["kind"] == "complex-type" and base_name in {"Element", "BackboneElement"}:
                    runtime_base = "DataType" if base_name == "Element" else "BackboneType"
                    base = f"global::MyFhirSdk.Core.{runtime_base}"
                    adaptations.append({"definition": name, "officialBase": base_name, "runtimeBase": runtime_base,
                                        "reason": "Shared abstract bridge supplies the Extension value marker; verify V1 JSON shape"})
                else:
                    base = clr(base_name)
            else:
                owner = next(e for e in elements if e["path"] == path)
                base = clr(code(owner["type"][0]))
            fields = []
            property_names = set()
            for e in elements:
                if not e["path"].startswith(path + ".") or e["path"].count(".") != path.count(".") + 1:
                    continue
                if e.get("base", {}).get("path", e["path"]) != e["path"] or e.get("max") == "0":
                    continue
                wire = e["path"].split(".")[-1]
                alternatives = e.get("type", [])
                if e.get("contentReference"):
                    reference = e["contentReference"].lstrip("#")
                    if reference not in paths:
                        raise ValueError(f"Unresolved contentReference: {reference}")
                    typed = [(wire, f"global::{NS}.{category}.{paths[reference]}")]
                else:
                    typed = []
                    for t in alternatives:
                        type_name = code(t)
                        if "[x]" in wire:
                            json_name = wire.replace("[x]", pascal(type_name))
                        else:
                            json_name = wire
                        target = e["path"]
                        value_type = f"global::{NS}.{category}.{paths[target]}" if target in paths else clr(
                            "SimpleQuantity" if any(p.endswith("/SimpleQuantity") for p in t.get("profile", [])) else type_name)
                        typed.append((json_name, value_type))
                if not typed:
                    raise ValueError(f"No concrete type for {e['path']}")
                fields_for_element = []
                for json_name, value_type in typed:
                    property_name = pascal(json_name)
                    if property_name == class_name:
                        property_name += "Value"
                    if property_name in property_names:
                        raise ValueError(f"Duplicate property: {class_name}.{property_name}")
                    property_names.add(property_name)
                    collection = e["max"] != "1"
                    shape = f"IList<{value_type}>" if collection else f"{value_type}?"
                    initial = f" = new List<{value_type}>();" if collection else ""
                    fields.append(f'    [JsonPropertyName("{json_name}")]\n    public {shape} {property_name} {{ get; set; }}{initial}')
                    fields_for_element.append(property_name)
                members.setdefault(class_type, []).append({"element": e["path"], "wire": wire, "min": e.get("min", 0),
                                                          "max": e["max"], "properties": fields_for_element})
            synthetic = f'    public override string ResourceType => "{name}";\n' if path == name and category == "Resources" else ""
            outputs[f"{MODELS}/{category}/{class_name}.g.cs"] = (
                "// <auto-generated /> V1 experiment only.\n#nullable enable\nusing System.Text.Json.Serialization;\n"
                f"namespace {NS}.{category};\n\npublic class {class_name} : {base}\n{{\n{synthetic}" + "\n\n".join(fields) + "\n}\n")

    resource_names = sorted(n for n in models if definitions[n]["kind"] == "resource")
    concrete = sorted(t for t in class_types if t.startswith(f"global::{NS}.Types.") and "." not in class_types[t])
    extension_types = [clr(code(t)) for e in definitions["Extension"]["snapshot"]["element"]
                       if e["path"] == "Extension.value[x]" for t in e["type"]]
    extension_codes = [code(t) for e in definitions["Extension"]["snapshot"]["element"]
                       if e["path"] == "Extension.value[x]" for t in e["type"]]
    composition = ["// <auto-generated /> V1 experiment only.", "#nullable enable", "using MyFhirSdk.Core;",
                   "using MyFhirSdk.ModelMetadata;", "using MyFhirSdk.Primitives;", "using MyFhirSdk.Validation.Rules;",
                   "namespace MyFhirSdk.Tests.Architecture.V1;", "internal static class R4ExperimentComposition", "{",
                   '    internal const string PackageId = "hl7.fhir.r4.core";', '    internal const string FhirVersion = "4.0.1";',
                   "    internal static IReadOnlyList<Type> ModelTypes { get; } = [" +
                   ", ".join(f"typeof({t})" for t in sorted(class_types)) + "];",
                   "    internal static PrimitiveRegistry CreatePrimitives() => PrimitiveRegistry.Create(["]
    codec_names = {"string": "String", "boolean": "Boolean", "integer": "Integer", "decimal-literal": "Decimal"}
    for n in supported_primitives:
        p = primitives[n]
        composition.append(f'        new PrimitiveDefinition("{n}", typeof({clr(n)}), typeof({p["clrValueType"]}), '
                           f'PrimitiveCodecs.{codec_names[p["codecKey"]]}, PrimitiveValidators.{pascal(p["validatorKey"])}),')
    composition += ["    ]);", "    internal static ImmutableModelMetadataProvider CreateMetadata() => new(", "        ["]
    composition += [f'            new ResourceTypeMetadata("{n}", typeof({clr(n)}), () => new {clr(n)}()),' for n in resource_names]
    composition += ["        ],", "        [" + ", ".join(f"typeof({t})" for t in concrete) + "],",
                    "        [new DeclaredDataTypeMetadata(typeof(Meta), \"tag\", typeof(global::MyFhirSdk.R4.Types.Coding)),",
                    "         new DeclaredDataTypeMetadata(typeof(Meta), \"security\", typeof(global::MyFhirSdk.R4.Types.Coding))],", "        ["]
    composition += [f'            new ExtensionValueMetadata(typeof({t}), "value{pascal(n)}"),' for t, n in zip(extension_types, extension_codes)]
    composition += ["        ]);", "    internal static IValidationRuleProvider CreateRules() => ResourceRuleRegistry.Create(["]
    for class_type, entries in sorted(members.items()):
        rules = []
        for e in entries:
            if "[x]" in e["wire"]:
                method = "ExactlyOne" if e["min"] else "AtMostOne"
                access = ", ".join(f"x => x.{p}" for p in e["properties"])
                rules.append(f'ChoiceElementRule<{class_type}>.{method}("{e["wire"]}", {access})')
            elif e["min"]:
                method = "For" if e["max"] == "1" else "ForList"
                rules.append(f'RequiredFieldRule<{class_type}>.{method}("{e["wire"]}", x => x.{e["properties"][0]})')
        if rules:
            composition.append(f"        new KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>(typeof({class_type}), [{', '.join(rules)}]),")
    composition += ["        new KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>(typeof(Narrative),",
                    '            [RequiredFieldRule<Narrative>.For("status", x => x.Status), RequiredFieldRule<Narrative>.For("div", x => x.Div)]),',
                    "        new KeyValuePair<Type, IReadOnlyList<IFhirValidationRule>>(typeof(Extension),",
                    '            [RequiredFieldRule<Extension>.For("url", x => x.Url)]),', "    ]);", "}"]
    outputs[COMPOSITION] = "\n".join(composition) + "\n"
    manifest = {"schemaVersion": 1, "experiment": "V1-selected-closure", "productionR4Support": False,
                "package": lock, "seeds": SEEDS, "definitions": [
                    {"id": n, "sha256": hashes[n], "kind": definitions[n]["kind"],
                     "disposition": "runtime-bootstrap" if n in bootstrap else "xhtml-via-Narrative" if n == "xhtml" else
                                    "common-primitive-prototype" if n in supported_primitives else "version-auxiliary" if n == "SimpleQuantity" else "generated-model"}
                    for n in sorted(closure)],
                "dependencyEdges": [{"from": a, "to": b} for a, b in sorted(edges)],
                "primitiveTemplatePolicy": {"path": primitive_policy_path, "sha256": digest((ROOT / primitive_policy_path).read_bytes())},
                "primitiveTemplates": templates, "abstractBridgeAdaptations": adaptations,
                "unsupported": ["xhtml primitive wrapper; shared Narrative.Div is a raw string"],
                "deferred": ["terminology", "FHIRPath invariants", "fixed/pattern", "targetProfile enforcement", "general constraint generator"],
                "sources": [{"path": p, "sha256": digest(text.encode())} for p, text in sorted(outputs.items())]}
    outputs[MANIFEST] = json.dumps(manifest, indent=2) + "\n"
    return outputs


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify committed prototype bytes without writing")
    args = parser.parse_args()
    generated = generate()
    expected_paths = set(generated)
    actual_paths = {p.relative_to(ROOT).as_posix() for p in (ROOT / MODELS).rglob("*.cs")}
    if actual_paths - expected_paths:
        raise ValueError(f"Unexpected prototype sources: {sorted(actual_paths - expected_paths)}")
    for path, content in generated.items():
        target = ROOT / path
        raw = content.replace("\r\n", "\n").encode()
        if args.check:
            if not target.is_file() or target.read_bytes() != raw:
                raise ValueError(f"Prototype drift: {path}")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
    print(f"V1 prototype {'verified' if args.check else 'generated'}: {len(generated)} files")
