# MyFhirSdk CodeGen Tool

`MyFhirSdk.CodeGen.Tool` is the repository-local .NET tool for deterministic FHIR R5
primitive and model source generation.

The command name is `myfhir-codegen`. Run the following after restoring the repository
tool manifest:

```text
dotnet myfhir-codegen --help
```

The tool package owns its default model policies, Runtime contract descriptor, and
compiler-only Runtime reference. Primitive mode continues to require an explicit
`--policy` input. Explicit CLI asset overrides take precedence over package assets and
must satisfy the packaged compatibility contract.

This package is intended for repository-local development and intentionally does not
declare public license metadata. An approved license is a mandatory gate before any
public NuGet release; signing and SBOM policy also remain deferred to the release phase.
