# Phase C9 cleanup and Phase D handoff

## Completed cleanup

- Removed the obsolete MVP `datatype-preview` CLI mode and implicit no-mode dispatch.
- Removed `FhirSdkGenerator`, its preview DTO/parser/renderer path, preview mapping overload, and
  obsolete preview-only tests and golden files.
- Reduced `CSharpTypeMapper` to validated `PrimitiveTypeMappingView` and
  `DefinitionTypeMappingView` inputs; it no longer invents mappings from a selected name set.
- Removed the default R5 provider's unregistered-resource factory fallback.
- Removed handwritten concrete Resource, Backbone, datatype, and R5 metadata entry sources that
  generated artifacts replaced in C8.
- Simplified `MyFhirSdk.csproj`; generated models and retained `SimpleQuantity` now use the normal
  compile glob without transitional exclusions.

## Regression gates

`PhaseCModelGenerationArchitectureTests` prevents the preview pipeline, mapper overload, preview
CLI mode, and unregistered default-provider fallback from returning. Existing integration gates
continue to require generated metadata owners and committed full-batch drift equality.

CodeGen production source contains no `DefaultComplexTypeNames`, `PrimitiveTypeNames`, preview type
set, assembly `GetTypes()` inventory, or concrete handwritten Resource/Type dependency. The only
direct SDK model namespace identities in CodeGen are deterministic namespace mapping and the FHIR
`Range` qualification needed to avoid `System.Range`; neither reads a handwritten declaration.

## Documentation outcome

- The boundary document now records Phase A-C as complete.
- The Phase C input handoff is marked consumed.
- README and C8 operating instructions describe generated ownership and the two supported CLI modes.
- `MyFhirSdk_R5_Models_Generation_Phase_D_Handoff.md` owns the remaining dependency seam, explicit
  Roslyn Runtime reference, repository-independent input discovery, tool packaging, and version
  compatibility work.

## Verification

```powershell
dotnet test MyFhirSdk.sln -c Release --no-restore
dotnet test Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj -c Release --no-restore `
  --filter FullyQualifiedName~CommittedModelGenerationTests
git diff --check
```
