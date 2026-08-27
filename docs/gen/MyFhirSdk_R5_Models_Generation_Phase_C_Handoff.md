# MyFhirSdk R5 Models Generation Phase C Handoff

Version 1.0

- Status: ready after Primitive Generation Phase B
- FHIR baseline: R5 `5.0.0`, package `hl7.fhir.r5.core#5.0.0`
- Primitive policy: `CodeGen/Policy/primitive-generation-policy.json`
- Formal primitive output: `Generated/R5/Primitives/`
- Implementation guide:
  `docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md`

## 1. Purpose

This document hands the validated primitive-generation contract from Phase B to
Phase C. Phase C must reuse this contract while adding complete datatype,
Resource, dependency-graph, and metadata generation. It must not introduce a
second primitive-to-wrapper mapping.

## 2. Completed Phase B contract

Phase B provides:

- a complete official inventory of 21 R5 primitive StructureDefinitions;
- one validated policy decision for every official primitive;
- 17 supported generated wrappers and registry entries;
- four explicit unsupported decisions for `oid`, `time`, `uuid`, and `xhtml`;
- a deterministic manifest and committed generated output;
- `PrimitiveTypeMappingView`, derived only from
  `ValidatedPrimitiveGenerationPolicy`;
- a `CSharpTypeMapper` that requires the derived policy view and contains no
  static primitive mapping dictionary.

The policy is the authority for wrapper name, namespace, CLR value type, JSON
token, codec key, validator key, literal shape, support status, compatibility
members, and unsupported reason.

## 3. Phase C consumer flow

Phase C should obtain primitive mappings from the already validated coverage
model:

```csharp
var coverageResult = await new PrimitiveInventoryCoveragePipeline().BuildAsync(
    primitiveDefinitionsPath,
    primitivePolicyPath,
    expectedFhirVersion,
    cancellationToken);

var coverage = coverageResult.Value
    ?? throw new InvalidOperationException("Primitive coverage is invalid.");
var primitiveMappings = new PrimitiveTypeMappingView(coverage.Policy);
var typeMapper = new CSharpTypeMapper(
    primitiveMappings,
    knownComplexTypeNames);
```

Production code must propagate `GenerationResult` diagnostics instead of using
the example exception. The important ownership rule is:

```text
official primitive definitions + versioned policy
                    ↓ validate and join
 PrimitiveInventoryPolicyCoverage.Policy
                    ↓ derive
       PrimitiveTypeMappingView
                    ↓ inject
            CSharpTypeMapper
```

Phase C must not copy the 17 mappings into a dictionary, switch expression,
template, metadata provider, or dependency-graph special case.

## 4. Remaining Phase C ownership

| Item | Current state | Phase C exit criterion |
|---|---|---|
| Complex type whitelist | `CSharpTypeMapper.DefaultComplexTypeNames` remains as the MVP scope gate | Replace it with the validated complete definition inventory/dependency graph |
| R5 metadata provider | Handwritten/assembly-based metadata remains under `ModelMetadata/R5` | Generate deterministic model metadata entries without Runtime concrete-type branching |
| Base model shapes | `Element`, `BackboneType`, `Resource`, and `DomainResource` retain bootstrap properties | Decide ownership through the Phase C model-shape design and preserve Runtime contracts |
| Bootstrap datatypes | `Extension`, `Meta`, and `Narrative` remain in the current assembly | Generate or retain them according to an explicit ownership decision and API migration gate |
| Assembly layout | Runtime and R5 models still compile into one SDK assembly | Do not split assemblies without a separate ADR for the composition seam |
| CodeGen Runtime reference | CodeGen still references the current SDK project for Roslyn validation | Replace with an explicit Runtime contract reference before local-tool release if required |

No remaining item is ownerless. The complex whitelist and complete model graph
belong to Phase C; local-tool packaging belongs to Phase D.

## 5. Compatibility requirements

Phase C must preserve:

- the generated primitive public API snapshot;
- `decimal` and `integer64` lexical representation behavior;
- primitive JSON raw/metadata alignment;
- internal immutable primitive registry composition;
- Parser, Serializer, and Validator dispatch through Runtime contracts;
- deterministic UTF-8/LF generated artifacts;
- manifest FHIR, policy, CodeGen, and Runtime contract versions.

Adding support for `oid`, `time`, `uuid`, or `xhtml` requires a separately
approved Runtime CLR/codec/validator contract and a policy update. Phase C must
not infer those mappings.

## 6. Phase C entry gates

Before Phase C changes model generation, verify:

```powershell
dotnet test MyFhirSdk.sln -c Release --no-restore
dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -c Release --no-restore -- `
  --mode primitive `
  --input Tests/CodeGen/Fixtures/StructureDefinitions/Primitives/R5 `
  --policy CodeGen/Policy/primitive-generation-policy.json `
  --output Generated/R5/Primitives `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
git diff --exit-code -- Generated/R5/Primitives
```

Architecture tests must continue to reject a static primitive mapping dictionary
inside `CSharpTypeMapper`.
