# MyFhirSdk

MyFhirSdk is a .NET SDK for working with a focused FHIR R5 resource set. It currently provides typed resource models, FHIR JSON serialization and parsing, a small REST client, base validation, and an optional implementation-guide validation path.

The SDK is still in pre-release shape. It is useful for MVP flows and SDK development, but it is not a complete FHIR R5 implementation yet.

## Current Scope

- Target framework: `net9.0`
- FHIR version target: R5
- Resource models: `Patient`, `Bundle`, `Claim`, `Coverage`, `Encounter`, `Organization`, and `Practitioner`
- Common datatypes and primitives used by the supported resources
- FHIR JSON serializer and parser
- REST client operations: read, create, update, and search
- Base FHIR validation rules for the supported model surface
- Optional profile validation framework
- Initial TW Core Patient validation proof of concept

## Repository Layout

| Path | Purpose |
| --- | --- |
| `MyFhirSdk.csproj` | SDK project |
| `MyFhirSdk.sln` | Solution containing the SDK and test projects |
| `Resources/` | Typed FHIR resource models |
| `Types/` | FHIR complex datatypes |
| `Primitives/` | FHIR primitive wrappers |
| `Serialization/Json/` | FHIR JSON parser and serializer |
| `Client/` | REST client, search, authentication, request, and response handling |
| `Validation/` | Base validation and profile validation framework |
| `ImplementationGuides/TwCore/` | Initial TW Core profile support |
| `Tests/` | xUnit test projects and JSON fixtures |
| `docs/` | Architecture, validation, client, IG, and roadmap notes |

## Getting Started

Install the .NET 9 SDK, then restore and build from the repository root:

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln
```

Until NuGet publishing is prepared, reference the SDK project directly from another .NET project:

```powershell
dotnet add reference path\to\MyFhirSdk\MyFhirSdk.csproj
```

## Basic JSON Usage

```csharp
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Types;

var patient = new Patient
{
    Id = "patient-1",
    Active = new FhirBoolean(true),
    BirthDate = new FhirDate("1974-12-25"),
    Identifier =
    {
        new Identifier
        {
            System = new FhirUri("http://hospital.example.org/patients"),
            Value = new FhirString("MRN-12345")
        }
    },
    Name =
    {
        new HumanName
        {
            Family = new FhirString("Chalmers"),
            Given =
            {
                new FhirString("Peter"),
                new FhirString("James")
            }
        }
    }
};

var serializer = new FhirJsonSerializer();
var json = serializer.Serialize(patient);

var parser = new FhirJsonParser();
var parsed = parser.Parse<Patient>(json);
```

The serializer emits the FHIR `resourceType` property and omits empty optional values. The parser validates the incoming `resourceType` against the requested resource type.

## Base Validation

```csharp
using MyFhirSdk.Resources;
using MyFhirSdk.Validation;

var patient = new Patient();
var validator = new FhirValidator();

var result = validator.Validate(patient);

if (!result.IsValid)
{
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"{issue.Severity} {issue.Path}: {issue.Message}");
    }
}
```

Base validation is kept separate from implementation-guide and local business rules.

## TW Core Profile Validation

```csharp
using MyFhirSdk.ImplementationGuides.TwCore;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Profiles;

var patient = new Patient
{
    Identifier =
    {
        new Identifier
        {
            System = new FhirUri("https://www.moi.gov.tw/"),
            Value = new FhirString("A123456789")
        }
    }
};

var profileValidator = new ProfileValidator(
    new FhirValidator(),
    TwCorePackage.Default);

var result = profileValidator.Validate(patient, TwCoreProfiles.Patient);
```

The initial TW Core Patient proof of concept checks that `Patient.identifier` exists and that each identifier has both `system` and `value`.

## REST Client Usage

```csharp
using MyFhirSdk.Client;
using MyFhirSdk.Client.Authentication;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Search;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;

using var httpClient = new HttpClient();

var client = new FhirClient(
    httpClient,
    new FhirJsonSerializer(),
    new FhirJsonParser(),
    new FhirClientOptions
    {
        BaseAddress = new Uri("https://example.org/fhir"),
        Timeout = TimeSpan.FromSeconds(30),
        ValidateBeforeSend = true
    },
    authProvider: new BearerTokenAuthProvider("token"));

var created = await client.CreateAsync(new Patient
{
    Active = new FhirBoolean(true)
});

var read = await client.ReadAsync<Patient>(created.Id!);

var query = FhirSearchQuery.Create()
    .Where("name", "Peter")
    .Count(10);

var bundle = await client.SearchAsync<Patient>(query);

created.Active = new FhirBoolean(false);
var updated = await client.UpdateAsync(created);
```

Pass `authProvider: null` or omit the parameter for unauthenticated servers. `ReadAsync` returns `null` when the server responds with `404 Not Found`.

## Tests

Run the full test suite from the repository root:

```powershell
dotnet test MyFhirSdk.sln
```

Integration smoke tests are skipped by default. To run the Patient create/read/search/update smoke flow against a FHIR R5 server:

```powershell
$env:MYFHIRSDK_INTEGRATION_BASE_URL="https://hapi.fhir.org/baseR5"
dotnet test Tests\Client\MyFhirSdk.Client.Tests.csproj --filter Category=Integration
```

For servers that require bearer authentication:

```powershell
$env:MYFHIRSDK_INTEGRATION_BEARER_TOKEN="your-token"
```

## Continuous Integration

The GitHub Actions workflow at `.github/workflows/ci.yml` runs on push, pull request, and manual dispatch. It restores the solution, builds in `Release`, runs the xUnit suite, creates a NuGet package, and uploads the package as a workflow artifact.

The workflow does not set `MYFHIRSDK_INTEGRATION_BASE_URL`, so integration smoke tests remain skipped in regular CI runs.

## Packaging

Create a local NuGet package:

```powershell
dotnet pack MyFhirSdk.csproj -c Release
```

The package is written under `bin/Release`. Package metadata and publishing configuration are part of the release-readiness roadmap and may still change before the first public release.

## Development Commands

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln
dotnet test MyFhirSdk.sln
dotnet pack MyFhirSdk.csproj -c Release
```

## Design Notes

- Base FHIR validation and IG/profile validation are intentionally separate.
- JSON fixture tests cover serializer and parser behavior end to end.
- The REST client is designed around replaceable serializer, parser, HTTP sender, response handler, validator, and authentication collaborators.
- Current IG work starts with TW Core Patient validation and can be extended by adding packages and profile rules under `Validation/Profiles/`.

See the `docs/` folder for deeper architecture notes and the active roadmap.
