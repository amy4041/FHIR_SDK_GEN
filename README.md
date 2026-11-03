# MyFhirSdk

MyFhirSdk is a pre-release .NET 9 SDK for FHIR R4 `4.0.1` and R5 `5.0.0`. It provides generated models, JSON parsing/serialization, base validation, a typed REST client and an injectable profile validation framework. It is not a complete FHIR implementation.

## Getting started

Use the .NET SDK pinned in `global.json` (`9.0.317`):

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln
dotnet test MyFhirSdk.sln
```

An application references `MyFhirSdk.csproj` plus only the Models project it uses: `Models/R4/MyFhirSdk.R4.Models.csproj` or `Models/R5/MyFhirSdk.R5.Models.csproj`. Dual applications reference both. DLL consumers use the [current delivery and migration guide](docs/sdk_decoupling/Delivery_and_Migration.md) and [external examples](examples/external/README.md).

The shared SDK depends only on Runtime, ModelContracts and ModelSupport. It has no dependency on R4/R5 Models or a concrete IG. Applications choose the model version by composing an immutable context. Parameterless engines, `R5ModelContext.Default`, non-generic `FhirClient`/`IFhirClient` and the old response handler have been removed. Rebuild applications and dependent libraries together; this is a source and binary breaking change with no type forwarders.

## JSON and base validation

This R5 example requires R5 Models. For R4, use `R4ModelProvider` and the `MyFhirSdk.R4.Resources` namespace.

```csharp
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

var context = new FhirModelContext(
    R5ModelProvider.Metadata, R5ModelProvider.Primitives, R5ModelProvider.Validation);
var parser = new FhirJsonParser(context);
var serializer = new FhirJsonSerializer(context);
var validator = new FhirValidator(context);
var patient = parser.Parse<Patient>("""{"resourceType":"Patient","id":"synthetic-example"}""");
var json = serializer.Serialize(patient);
var result = validator.Validate(patient);
```

Use a separate context for each version in a Dual process. Context ownership checks reject a model from another version. Validation traverses the model using the supplied rules; `Validate` returns issues. Client validation before sending can throw `FhirValidationException`.

## Typed REST client

Continue from the R5 context above:

```csharp
using MyFhirSdk.Client;
using MyFhirSdk.Client.Configuration;

using var http = new HttpClient();
var client = new FhirClient<Bundle, OperationOutcome>(http, context,
    new FhirClientOptions
    {
        BaseAddress = new Uri("https://your-r5-server.example/fhir/"),
        Timeout = TimeSpan.FromSeconds(30),
        ValidateBeforeSend = true
    });
// Execute only against an explicitly selected test environment.
var existing = await client.ReadAsync<Patient>("synthetic-example");
```

Read/create/update/search/delete are supported. The type arguments must be the selected context's Bundle and OperationOutcome. The caller owns HttpClient; authentication is supplied through the optional `authProvider` argument. A read returning HTTP 404 produces null.

## Optional TW Core R4 Patient POC

Reference `ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj` and R4 Models in addition to the SDK:

```csharp
using MyFhirSdk.ImplementationGuides.TwCore;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.R4.Resources;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Profiles;

var r4Context = new FhirModelContext(
    R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);
var validator = new ProfileValidator(new FhirValidator(r4Context), TwCorePackage.Default);
var result = validator.Validate(new Patient(), TwCoreProfiles.Patient);
```

The independent `MyFhirSdk.ImplementationGuides.TwCore.dll` owns three handwritten Patient rules: identifier must exist, and each identifier must have system and value. It supports R4 `4.0.1` only and is not full official TW Core validation. IG implementations can be replaced through constructor injection without changing the SDK or rebuilding Models. A custom base validator used with profiles implements `IContextualFhirValidator`.

## Delivery and acceptance

```powershell
python eng/Test-SdkDecouplingD5.py --output artifacts/sdk-decoupling/d5/local
```

Run from a clean committed checkout. This exports current sources, then publishes and executes R4/R5/Dual/R4TwCore consumers outside the repository, checks missing dependencies and IG replacement, and verifies fresh installed-tool repeated generation. Output includes a manifest with file hashes and source identity. Development previews may use `--allow-working-tree`; they leave clean-source acceptance Pending. See the [delivery guide](docs/sdk_decoupling/Delivery_and_Migration.md) for individual export and verify commands.

Current CI runs the D4 Windows/Ubuntu delivery gates and cross-platform comparison, plus D5 offline delivery verification. Historical K/V/D gates run against their pinned sources and contracts. The [D5 record](docs/sdk_decoupling/D5_Implementation.md) distinguishes local verification, clean-source CI and actual server acceptance. Actual server smoke remains Pending until explicitly configured and executed; regular CI does not contact a FHIR server. Historical [V7 records](docs/fhir_versions/V7_Closeout.md) describe the old API and do not establish acceptance for the current API.

## Repository layout and model generation

| Path | Purpose |
| --- | --- |
| `Runtime/` | Shared kernel and model base hierarchy |
| `ModelContracts/`, `ModelSupport/` | Neutral provider contracts and model execution support |
| `Models/R4/`, `Models/R5/`, `Generated/R4/`, `Generated/R5/` | Version-owned models, primitives, providers and generated rules |
| `ModelMetadata/`, `Serialization/`, `Validation/`, `Client/` | SDK context, engines, HTTP and IG framework |
| `ImplementationGuides/TwCore/` | Independent R4 Patient POC |
| `CodeGen/` | Repository-local generator and pinned policies |
| `examples/external/` | DLL-only delivery templates |
| `Tests/`, `eng/` | Regression suites and acceptance tools |

Build the compiler-only Runtime reference and package the repository-local generator:

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
dotnet tool restore --add-source artifacts/packages --ignore-failed-sources
dotnet myfhir-codegen --help
```

The tool manifest pins `MyFhirSdk.CodeGen.Tool` `1.3.1`. The tool ships policies, the Runtime contract, compiler-only Runtime reference and SimpleQuantity auxiliary source. That reference is a compiler asset, not a runtime implementation DLL. Generated C# alone is not a complete Models DLL; normal applications use delivered Models. See [CodeGen operations](CodeGen/README.md) for generation details.

Delivery uses verified implementation DLLs or project references. Public/private NuGet publishing, licensing, full terminology/FHIRPath, official IG compilation and cross-version conversion remain separate work. Exporting a local tool package does not publish it.
