# MyFhirSdk Architecture

## 1. Purpose

This document defines the technical architecture for the MyFhirSdk MVP.
It is intended to guide implementation work by humans and Codex agents.

MyFhirSdk is a .NET 9 SDK for working with FHIR R4 resources. The MVP focuses on:

- Creating common FHIR R4 resources.
- Serializing resources to FHIR JSON.
- Deserializing FHIR JSON into typed resource objects.
- Calling basic FHIR REST APIs.
- Validating required fields, primitive formats, and basic cardinality.

The SDK should be small, modular, testable, and easy to extend after the MVP.

## 2. Architectural Principles

- Support FHIR R4 only in the MVP.
- Keep resource models strongly typed.
- Keep FHIR primitives separate from .NET primitive values.
- Prefer clear SDK APIs over exposing internal serialization details.
- Keep modules loosely coupled.
- Avoid code generation in the MVP.
- Avoid profile, terminology, FHIRPath, and StructureDefinition validation in the MVP.
- Make behavior easy to test with unit tests and mocked HTTP clients.

## 3. Solution Structure

The intended solution layout is:

```text
MyFhirSdk
|-- src
|   |-- MyFhirSdk.Core
|   |-- MyFhirSdk.Primitives
|   |-- MyFhirSdk.Types
|   |-- MyFhirSdk.Resources
|   |-- MyFhirSdk.Serialization
|   |-- MyFhirSdk.Validation
|   `-- MyFhirSdk.Client
|-- tests
|   `-- MyFhirSdk.Tests
`-- docs
    |-- fhir_sdk_mvp_prd.md
    `-- architecture.md
```

If a single project is used during the earliest MVP bootstrap, the namespaces should still follow this structure so the SDK can be split into projects later without changing public API names.

## 4. Module Responsibilities

### MyFhirSdk.Core

Defines the shared base model and common abstractions.

Responsibilities:

- Base FHIR object hierarchy.
- Common metadata fields.
- Shared interfaces for resources and elements.
- Common SDK exceptions.
- Shared constants such as FHIR version.

Primary types:

- `Base`
- `Element`
- `DataType`
- `BackboneElement`
- `Resource`
- `DomainResource`
- `FhirObject`
- `FhirSdkException`

### MyFhirSdk.Primitives

Defines FHIR primitive wrappers.

Responsibilities:

- Wrap primitive FHIR values.
- Preserve FHIR-specific validation rules.
- Support serialization and deserialization through the serialization layer.
- Keep primitive value semantics separate from raw .NET primitives.

MVP primitives:

- `FhirBoolean`
- `FhirString`
- `FhirUri`
- `FhirCanonical`
- `FhirCode`
- `FhirId`
- `FhirInteger`
- `FhirDecimal`
- `FhirDate`
- `FhirDateTime`
- `FhirInstant`

Each primitive should expose:

- A nullable raw value.
- A constructor or factory from the matching .NET type.
- Basic validation hooks.
- A predictable `ToString()` representation for debugging.

### MyFhirSdk.Types

Defines reusable complex FHIR datatypes.

Responsibilities:

- Model general-purpose FHIR R4 datatypes.
- Use FHIR primitive types where applicable.
- Avoid embedding resource-specific behavior.

MVP datatypes:

- `Identifier`
- `HumanName`
- `Address`
- `ContactPoint`
- `Coding`
- `CodeableConcept`
- `Quantity`
- `Period`
- `Reference`

### MyFhirSdk.Resources

Defines supported FHIR R4 resources.

Responsibilities:

- Model MVP resource classes.
- Define `ResourceType`.
- Represent resource fields using FHIR primitives and complex datatypes.
- Avoid HTTP, serialization, and validation logic inside resource classes.

MVP resources:

- `Patient`
- `Organization`
- `Practitioner`
- `Encounter`
- `Coverage`
- `Claim`
- `Bundle`

Resource classes should inherit from `DomainResource` unless a FHIR R4 rule requires otherwise.

### MyFhirSdk.Serialization

Defines serialization contracts and format-specific implementations.

The serialization layer should expose small interfaces that other modules depend on.
JSON classes should implement those interfaces using `System.Text.Json` for the MVP.
This keeps the SDK open to other formats or implementations later without changing the client-facing API.

Responsibilities:

- Define format-neutral serializer and parser abstractions.
- Convert typed SDK resources to FHIR JSON through `FhirJsonSerializer`.
- Parse FHIR JSON into typed SDK resources through `FhirJsonParser`.
- Handle `resourceType`.
- Handle primitive values and complex datatypes.
- Preserve FHIR JSON naming conventions.
- Keep serialization and parsing behavior outside resource model classes.

Primary interfaces:

- `IFhirSerializer`
- `IFhirParser`

Primary implementations:

- `FhirJsonSerializer`
- `FhirJsonParser`

Recommended file layout:

```text
Serialization
|-- IFhirSerializer.cs
|-- IFhirParser.cs
`-- Json
    |-- FhirJsonSerializer.cs
    `-- FhirJsonParser.cs
```

MVP public contracts:

```csharp
public interface IFhirSerializer
{
    string Serialize<TResource>(TResource resource)
        where TResource : Resource;
}

public interface IFhirParser
{
    TResource Parse<TResource>(string json)
        where TResource : Resource;
}
```

MVP implementation classes:

```csharp
namespace MyFhirSdk.Serialization.Json;

public sealed class FhirJsonSerializer : IFhirSerializer
{
    public string Serialize<TResource>(TResource resource)
        where TResource : Resource
    {
        // Uses System.Text.Json and FHIR-specific converters.
    }
}

public sealed class FhirJsonParser : IFhirParser
{
    public TResource Parse<TResource>(string json)
        where TResource : Resource
    {
        // Uses System.Text.Json and validates resourceType.
    }
}
```

Implementation should use `System.Text.Json` unless there is a clear reason to choose another serializer.
FHIR-specific behavior such as primitive wrapper conversion, `resourceType` handling, and resource dispatch should be implemented with serializer options or custom converters in this module.

### MyFhirSdk.Validation

Validates SDK resources and datatypes.

Responsibilities:

- Required field validation.
- Basic cardinality validation.
- Primitive format validation.
- Return structured validation results.

MVP public API:

```csharp
public interface IFhirValidator
{
    ValidationResult Validate(Resource resource);
}
```

Validation should not call external terminology services or validate custom profiles in the MVP.

### MyFhirSdk.Client

Provides FHIR REST API operations.

Responsibilities:

- Configure FHIR server base URL.
- Execute read, create, update, and search operations.
- Use the serialization layer for request and response bodies.
- Return typed resources or bundles.
- Allow tests to inject `HttpClient` or `HttpMessageHandler`.

MVP public API:

```csharp
public interface IFhirClient
{
    Task<TResource?> ReadAsync<TResource>(string id, CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<TResource> CreateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<TResource> UpdateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<Bundle> SearchAsync<TResource>(string query, CancellationToken cancellationToken = default)
        where TResource : Resource;
}
```

Client behavior:

- `ReadAsync<Patient>("123")` should call `GET /Patient/123`.
- `CreateAsync(patient)` should call `POST /Patient`.
- `UpdateAsync(patient)` should call `PUT /Patient/{id}`.
- `SearchAsync<Patient>("name=amy")` should call `GET /Patient?name=amy`.

## 5. Type Hierarchy

The core hierarchy should follow the FHIR R4 model at a practical MVP level:

```text
Base
`-- Element
    |-- DataType
    |   |-- PrimitiveType<T>
    |   `-- Complex datatypes
    `-- BackboneElement

Resource
`-- DomainResource
    |-- Patient
    |-- Organization
    |-- Practitioner
    |-- Encounter
    |-- Coverage
    |-- Claim
    `-- Bundle
```

Recommended base shape:

```csharp
public abstract class Base
{
}

public abstract class Element : Base
{
    public string? Id { get; set; }
}

public abstract class DataType : Element
{
}

public abstract class PrimitiveType<T> : DataType
{
    public T? Value { get; set; }
}

public abstract class Resource : Base
{
    public string? Id { get; set; }
    public abstract string ResourceType { get; }
}

public abstract class DomainResource : Resource
{
}
```

The hierarchy may be expanded later for extensions, contained resources, metadata, narratives, and modifier extensions.

## 6. Public API Style

The SDK should prefer straightforward C# object creation:

```csharp
var patient = new Patient
{
    Id = "123",
    Name =
    [
        new HumanName
        {
            Family = new FhirString("Chen"),
            Given = [new FhirString("Amy")]
        }
    ]
};
```

Serialization:

```csharp
string json = serializer.Serialize(patient);
```

Deserialization:

```csharp
Patient patient = parser.Parse<Patient>(json);
```

REST client:

```csharp
Patient? patient = await client.ReadAsync<Patient>("123");
Bundle result = await client.SearchAsync<Patient>("name=amy");
```

Validation:

```csharp
ValidationResult result = validator.Validate(patient);
```

## 7. Serialization Rules

MVP serialization should follow these rules:

- Emit `resourceType` for all resources.
- Use FHIR JSON property names.
- Omit null values.
- Serialize primitive wrappers as their raw primitive values.
- Serialize repeated fields as arrays.
- Deserialize unknown JSON properties leniently unless they prevent parsing required MVP fields.
- Throw a clear SDK exception when `resourceType` is missing or mismatched.

Example:

```json
{
  "resourceType": "Patient",
  "id": "123",
  "name": [
    {
      "family": "Chen",
      "given": ["Amy"]
    }
  ]
}
```

## 8. Validation Rules

Validation should return structured results rather than throwing for normal validation failures.

Recommended shape:

```csharp
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues { get; }
}

public sealed class ValidationIssue
{
    public string Path { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ValidationSeverity Severity { get; init; }
}
```

MVP validation should include:

- Required fields defined by the MVP scope.
- Basic cardinality such as single value vs. repeated value.
- Primitive format checks for `id`, `uri`, `date`, `dateTime`, `instant`, and `code`.

Validation should not include:

- Profile validation.
- StructureDefinition validation.
- Terminology validation.
- FHIRPath rules.
- ValueSet expansion.

## 9. REST Client Design

The REST client should be built around `HttpClient`.

Configuration:

```csharp
public sealed class FhirClientOptions
{
    public Uri BaseAddress { get; init; }
}
```

Recommended constructor:

```csharp
public sealed class FhirClient : IFhirClient
{
    public FhirClient(
        HttpClient httpClient,
        IFhirSerializer serializer,
        IFhirParser parser,
        FhirClientOptions options)
    {
    }
}
```

HTTP expectations:

- Send `Content-Type: application/fhir+json` for JSON request bodies.
- Send `Accept: application/fhir+json`.
- Treat non-success responses as SDK client exceptions.
- Preserve server response bodies in exceptions when practical.
- Support cancellation tokens on all async methods.

Authentication is out of scope for the MVP unless a consuming application configures `HttpClient` externally.

## 10. Error Handling

Use SDK-specific exceptions for infrastructure and protocol failures.

Recommended exceptions:

- `FhirSdkException`
- `FhirSerializationException`
- `FhirParseException`
- `FhirClientException`

Validation failures should normally be returned as `ValidationResult`, not thrown.

## 11. Testing Strategy

Use unit tests as the primary test layer.

Required test areas:

- Primitive construction and validation.
- Complex datatype serialization.
- Resource serialization and deserialization.
- Resource type mismatch handling.
- Required field validation.
- REST read, create, update, and search request construction.
- Bundle parsing for search results.

Recommended tooling:

- xUnit or NUnit.
- `System.Text.Json` test assertions through parsed JSON documents.
- Mocked `HttpMessageHandler` for client tests.

Acceptance targets:

- Unit test coverage should be at least 80% for MVP implementation.
- Standard resource serialization should complete under 100ms in normal local test conditions.
- Search response parsing should complete under 200ms in normal local test conditions.

## 12. MVP Boundaries

In scope:

- FHIR R4.
- FHIR JSON.
- Patient, Organization, Practitioner, Encounter, Coverage, Claim, and Bundle.
- Read, create, update, and search REST operations.
- Required, cardinality, and primitive format validation.

Out of scope:

- FHIR R5.
- XML serialization.
- FHIRPath.
- Profile validation.
- StructureDefinition validation.
- Terminology service integration.
- ValueSet expansion.
- SMART on FHIR.
- GraphQL.
- Subscription.
- Batch and transaction bundles.
- Code generation.
- Multi-version support.

## 13. Implementation Order

Recommended implementation sequence:

1. Create solution and project structure.
2. Implement core base classes.
3. Implement primitive types.
4. Implement complex datatypes.
5. Implement resource models.
6. Implement JSON serialization.
7. Implement JSON deserialization.
8. Implement validation.
9. Implement REST client.
10. Add sample JSON files and integration-style tests.

This order keeps dependencies flowing from low-level model types toward higher-level SDK features.
