# FHIR SDK IG Layer Design

Last updated: 2026-06-10

## 1. Purpose

This document describes how MyFhirSdk should support Implementation Guides (IGs) and
profiles without mixing IG-specific behavior into the base FHIR R5 SDK.

The goal is to make IG support optional, replaceable, and extensible.

## 2. Design Goals

- Keep the base SDK FHIR R5 compliant and IG-neutral.
- Allow multiple IGs to be registered at the same time.
- Allow one resource to be validated against one or more profiles.
- Keep base validation separate from profile validation.
- Support IG-specific validation rules, extensions, terminology, search helpers, and examples over time.
- Avoid full StructureDefinition-driven validation in the first IG implementation.

## 3. Non-Goals for the First Version

- Full automatic IG package loading.
- Full StructureDefinition snapshot or differential validation.
- Full FHIRPath invariant execution.
- Remote terminology server integration.
- Automatic code generation from IG profiles.
- Modifying base resource classes for IG-specific requirements.

## 4. Relationship Between the Base SDK and IG Layer

```text
Application
  -> IG Layer
  -> Base SDK
  -> FHIR JSON / REST Server
```

Base SDK responsibilities:

- FHIR R5 resource models.
- FHIR primitives and datatypes.
- JSON serialization and parsing.
- REST client.
- Base FHIR validation.

IG layer responsibilities:

- Profile metadata and canonical URLs.
- IG/profile-specific validation rules.
- Extension helpers.
- Terminology bindings.
- Search helpers.
- IG examples and fixtures.
- Future package loading.

Dependency direction:

```text
IG layer depends on the Base SDK.
Base SDK does not depend on any concrete IG.
```

## 5. Key Boundary Rule

Do not place IG-specific rules directly into:

- Resource classes such as `Patient`, `Claim`, or `Coverage`.
- The base JSON serializer or parser.
- The base REST client.
- The base `ResourceRuleRegistry`.

IG-specific behavior should live in replaceable IG providers, registries, helpers, or wrapper APIs.

## 6. Proposed Modules

Initial conceptual modules:

```text
MyFhirSdk
  Validation
    FhirValidator
    ResourceRuleRegistry

MyFhirSdk.ImplementationGuides
  IFhirImplementationGuide
  IFhirProfileValidator
  IFhirProfileValidationRule
  FhirProfileValidator
  FhirImplementationGuideRegistry

MyFhirSdk.Ig.ClaimExchange
  ClaimExchangeImplementationGuide
  ClaimExchangeProfiles
  ClaimExchangeValidationRules
  ClaimExchangeExtensions
  ClaimExchangeSearch
```

The exact project split can happen later. The first version may keep abstractions inside the main SDK
and add concrete IG packages after the base extension points are stable.

## 7. Core Interfaces

The base SDK should expose abstractions. Concrete IG packages should provide implementations.

```csharp
public interface IFhirImplementationGuide
{
    string Id { get; }
    string Name { get; }
    IReadOnlyCollection<string> SupportedProfiles { get; }

    bool SupportsProfile(string profileUrl);

    IEnumerable<IFhirProfileValidationRule> GetRules(
        string profileUrl,
        Type resourceType);
}
```

```csharp
public interface IFhirProfileValidator
{
    ValidationResult Validate(Resource resource, string profileUrl);

    ValidationResult Validate(Resource resource, IEnumerable<string> profileUrls);

    ValidationResult ValidateByDeclaredProfiles(Resource resource);
}
```

```csharp
public interface IFhirProfileValidationRule
{
    void Validate(
        FhirProfileValidationContext context,
        IList<ValidationIssue> issues);
}
```

## 8. Validation Flow

### Explicit Profile Validation

```csharp
var result = validator.Validate(
    claim,
    ClaimExchangeProfiles.Claim);
```

Flow:

```text
1. Run base FHIR validation.
2. Find an IG provider by profile URL.
3. Get profile rules for the resource type.
4. Run profile rules.
5. Return a combined ValidationResult.
```

Explicit profile validation is best for send-time validation, when the application knows the target
profile even if the resource has not yet declared `meta.profile`.

### Declared Profile Validation

```csharp
var result = validator.ValidateByDeclaredProfiles(claim);
```

Flow:

```text
1. Read resource.meta.profile.
2. For each declared profile, find a matching IG provider.
3. Run base validation once.
4. Run all matching profile rules.
5. Return a combined ValidationResult.
```

Declared profile validation is best for received resources, parsed server responses, and Bundles that
may contain resources from different profiles.

Open design choices:

- Unknown declared profile should be ignored, warning, or error.
- Empty `meta.profile` should return base validation only, warning, or error.
- Multiple profile conflicts should be reported as validation issues.
- Profile validation should probably run base validation first by default.

## 9. Multiple IG Support

The SDK should allow multiple IGs to be registered:

```csharp
var validator = new FhirProfileValidator(
    baseValidator,
    [
        new TwCoreImplementationGuide(),
        new ClaimExchangeImplementationGuide()
    ]);
```

Supported scenarios:

- National core IG plus business exchange IG.
- Local hospital IG plus external exchange IG.
- Bundle containing resources that declare different profiles.
- One resource declaring more than one profile.

Conflict handling:

- The SDK should not silently resolve conflicting rules.
- Conflicts should surface as validation issues.
- The caller decides how to handle invalid resources.

## 10. Profile Metadata

Each IG package should define profile canonical URLs as constants.

```csharp
public static class ClaimExchangeProfiles
{
    public const string Claim =
        "https://example.org/fhir/StructureDefinition/claim-exchange-claim";

    public const string Coverage =
        "https://example.org/fhir/StructureDefinition/claim-exchange-coverage";

    public const string Patient =
        "https://example.org/fhir/StructureDefinition/claim-exchange-patient";
}
```

Resources can declare conformance with `Resource.Meta.Profile`.

The base serializer and parser should continue to handle `meta.profile` as normal FHIR data. They
should not contain IG-specific logic.

## 11. Extension Helpers

The base SDK supports generic extensions:

```text
Extension.url
Extension.value[x]
```

The IG layer may provide typed helpers:

```csharp
ClaimExchangeExtensions.SetAuthorizationNumber(claim, "AUTH-123");
ClaimExchangeExtensions.GetAuthorizationNumber(claim);
```

Rules:

- Extension helpers should use the base SDK `Extension` model.
- Serializer and parser should remain IG-neutral.
- Extension URLs should be constants in the IG layer.
- Extension validation should live in profile validation rules, not in base resources.

## 12. Terminology Bindings

Future IG validation may validate codes against ValueSets.

Suggested abstraction:

```csharp
public interface ITerminologyValidator
{
    ValidationResult ValidateCode(
        string? system,
        string? code,
        string valueSetUrl);
}
```

The first version may use local static ValueSets only.

Out of scope initially:

- Remote terminology server calls.
- ValueSet expansion.
- Required binding validation across full StructureDefinition definitions.

## 13. Search Helpers

The base SDK already supports generic search query construction:

```csharp
FhirSearchQuery.Create().Where("identifier", "123");
```

The IG layer may provide typed search helpers:

```csharp
ClaimExchangeSearch.ClaimsByPatient("Patient/123");
TwCoreSearch.PatientByIdentifier("MRN-123");
```

Search helpers should return `FhirSearchQuery` and delegate execution to the base SDK `FhirClient`.

## 14. IG Examples and Fixtures

IG support should be tested with JSON examples.

Recommended test flow:

```text
IG fixture JSON
  -> Base parser
  -> IG profile validator
  -> Base serializer
  -> JSON comparison
```

Fixtures should be separated from base FHIR fixtures when possible.

Example folder:

```text
Tests/ImplementationGuides/ClaimExchange/Fixtures/
```

## 15. Client Integration

Initial client behavior:

- Base `FhirClient` remains IG-neutral.
- The caller performs IG validation before sending.
- A future optional wrapper may apply profile validation before send.

Possible future options:

```csharp
public bool ValidateProfilesBeforeSend { get; init; }
public IReadOnlyList<string> ProfilesToValidateBeforeSend { get; init; }
```

Alternative future shape:

```text
ClaimExchangeFhirClient
  -> validates profile
  -> sets meta.profile
  -> delegates HTTP to base FhirClient
```

Recommended first version:

```text
Keep base FhirClient unchanged.
Perform IG validation outside the client or in an optional wrapper.
```

## 16. Implementation Phases

### Phase 1 - Design and Inventory

- Add this design document.
- Add rule source classification to the validation inventory:
  - FHIR R5 Base
  - IG/Profile
  - Local Business Rule

### Phase 2 - Manual IG Provider

- Add IG abstractions.
- Add one sample/manual IG provider.
- Add explicit profile validation.

### Phase 3 - Declared Profile Validation

- Read `meta.profile`.
- Validate against all known declared profiles.
- Define unknown-profile behavior.

### Phase 4 - IG Fixtures

- Add IG-specific JSON fixtures.
- Validate parser and serializer compatibility.
- Validate profile rules.

### Phase 5 - Extension and Search Helpers

- Add typed extension helpers.
- Add typed search helpers.

### Phase 6 - Terminology

- Add local ValueSet support.
- Add terminology validation abstraction.

### Phase 7 - Package Loading

- Consider loading official IG package artifacts.
- Parse StructureDefinition, ValueSet, and CodeSystem resources.
- Keep this out of the first IG implementation.

## 17. Open Questions

- Which IG should be implemented first?
- Should unknown declared profiles be warnings or errors?
- Should profile validation always run base validation first?
- Should profile validators set `meta.profile`, or only validate?
- Should IG support live in the main package or separate NuGet packages?
- How should validation issue codes distinguish base rules from profile rules?
- Should profile validation return one combined result or grouped results by profile?

