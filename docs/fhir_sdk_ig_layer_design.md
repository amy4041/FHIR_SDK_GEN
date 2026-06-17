# FHIR SDK IG Layer Design

Last updated: 2026-06-17

## 1. Purpose

This document defines how MyFhirSdk should support Implementation Guides (IGs) and
profiles without mixing IG-specific behavior into the base FHIR SDK.

The design target is:

```text
FhirValidator = base FHIR validation
ProfileValidator = base validation plus selected profile validation
TwCorePackage = a concrete IG package that provides TW Core profile rules
```

The goal is to make IG support optional, replaceable, and extensible while keeping the
base SDK clean.

## 2. Design Goals

- Keep base FHIR validation independent from IG/profile validation.
- Keep concrete IG names such as TW Core out of base resource models, serialization, parsing,
  REST client behavior, and base rule registries.
- Allow one resource to be validated against one or more profiles.
- Allow one validator instance to register one or more IG packages.
- Run base validation once when validating against multiple profiles.
- Make validation issues explain where each issue came from.
- Support manual profile rules first; defer full StructureDefinition-driven validation.

## 3. Non-Goals for the First Version

- Full automatic IG package loading.
- Full StructureDefinition snapshot or differential validation.
- Full FHIRPath invariant execution.
- Remote terminology server integration.
- Automatic code generation from IG profiles.
- Modifying base resource classes for IG-specific requirements.
- Claiming conformance to an IG whose FHIR version is not compatible with the SDK model.

## 4. Layering Model

```text
Application / Local Business Rules
  -> IG/Profile Layer
  -> Base SDK
  -> FHIR JSON / REST Server
```

Base SDK responsibilities:

- FHIR resource models.
- FHIR primitives and datatypes.
- JSON serialization and parsing.
- REST client.
- Base FHIR validation.
- Generic extension representation.
- Generic profile validation extension points.

IG layer responsibilities:

- IG package metadata.
- Profile canonical URLs.
- IG/profile-specific validation rules.
- Slice and extension rules.
- Terminology binding references.
- Typed extension helpers.
- Typed search helpers.
- IG examples and fixtures.

Local business rule responsibilities:

- Project-specific workflow rules.
- Hospital/platform-specific required fields.
- Exchange-specific constraints that are not part of base FHIR or a published IG.

Dependency direction:

```text
Concrete IG package -> Base SDK
Base SDK -> no dependency on concrete IG packages
```

## 5. Key Boundary Rule

Do not place IG-specific rules directly into:

- Resource classes such as `Patient`, `Claim`, or `Coverage`.
- The base JSON serializer or parser.
- The base REST client.
- The base `ResourceRuleRegistry`.
- Base validation rules under `Validation/Rules`.

IG-specific behavior should live in concrete IG packages, profile rule registries, typed helpers,
or optional wrappers.

## 6. Core API Shape

### Base FHIR Validation

`FhirValidator` remains the base validator. It only answers:

```text
Is this a valid base FHIR resource according to the SDK's implemented base rules?
```

Example:

```csharp
var baseValidator = new FhirValidator();

ValidationResult result = baseValidator.Validate(patient);
```

`FhirValidator` should not know about TW Core, US Core, local hospital profiles, or any concrete
IG package.

### Profile Validation

`ProfileValidator` is an orchestration layer that runs base validation and then applies selected
profile rules from registered IG packages.

Example with one IG:

```csharp
var validator = new ProfileValidator(
    new FhirValidator(),
    TwCorePackage.Default);

ValidationResult result = validator.Validate(
    patient,
    TwCoreProfiles.Patient);
```

This means:

```text
1. Run base FHIR validation once.
2. Locate the package that supports TwCoreProfiles.Patient.
3. Run TW Core Patient profile rules.
4. Return one combined ValidationResult.
```

Example with multiple IGs:

```csharp
var validator = new ProfileValidator(
    new FhirValidator(),
    TwCorePackage.Default,
    UsCorePackage.Default);

ValidationResult result = validator.Validate(
    patient,
    [
        TwCoreProfiles.Patient,
        UsCoreProfiles.Patient
    ]);
```

This means:

```text
1. Run base FHIR validation once.
2. Run TW Core Patient rules.
3. Run US Core Patient rules.
4. Return one combined ValidationResult with source metadata on every issue.
```

The SDK should not silently resolve conflicts between profiles. If two profiles require conflicting
values, the resource is invalid against at least one of the requested profiles.

### IG Package

An IG package is not a validator by itself. It is a package of metadata and rules that
`ProfileValidator` can use.

Example:

```csharp
public sealed class TwCorePackage : IImplementationGuidePackage
{
    public static TwCorePackage Default { get; } = new();

    public string PackageId => "tw.gov.mohw.twcore#1.0.0";

    public IReadOnlyCollection<string> SupportedProfiles =>
    [
        TwCoreProfiles.Patient,
        TwCoreProfiles.Organization,
        TwCoreProfiles.Practitioner
    ];

    public bool SupportsProfile(string profileUrl);

    public IEnumerable<IProfileValidationRule> GetRules(
        string profileUrl,
        Type resourceType);
}
```

Concrete packages should also declare their target FHIR release so callers can detect incompatible
combinations before validation.

### First IG Implementation Target - TW Core Patient POC

The first concrete IG implementation should use TW Core as the package and TW Core Patient as the
first profile.

Implementation target:

| Setting | Value |
|---|---|
| IG | TW Core |
| Package name | TW Core |
| PackageId | `tw.gov.mohw.twcore#1.0.0` |
| IG FHIR version | `R4.0.1` |
| Current SDK model | FHIR R5-oriented base models |
| Compatibility policy | Treat the first implementation as a TW Core Patient POC, not full TW Core conformance. |
| First profile | TW Core Patient |
| ProfileUrl | `https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore` |
| Target resource type | `Patient` |
| Validation level | L1 Profile Structural Validation |
| Rule source | `ValidationRuleSource.ImplementationGuide` |
| Default severity | `ValidationSeverity.Error` |
| Explicit unknown profile behavior | Error |
| Terminology validation | Deferred |
| FHIRPath invariant validation | Deferred |
| Full slice validation | Deferred |
| Package auto loading | Deferred |

Initial TW Core Patient rule scope:

| RuleId | Rule |
|---|---|
| `TWCORE-PAT-001` | Support `TwCoreProfiles.Patient` canonical URL in `TwCorePackage`. |
| `TWCORE-PAT-002` | `Patient.identifier` must contain at least one item for TW Core Patient. |
| `TWCORE-PAT-003` | Each `Patient.identifier[*].system` must be present for TW Core Patient. |
| `TWCORE-PAT-004` | Each `Patient.identifier[*].value` must be present for TW Core Patient. |

Expected boundary behavior:

```text
new FhirValidator().Validate(new Patient())
  -> base validation only; empty Patient may be valid under current base rules.

new ProfileValidator(new FhirValidator(), TwCorePackage.Default)
  .Validate(new Patient(), TwCoreProfiles.Patient)
  -> base validation plus TW Core Patient rules; missing identifier is invalid.
```

These rules are intentionally small. They prove that profile rules can be layered onto base
validation without adding TW Core-specific requirements to `Patient` or `ResourceRuleRegistry`.

## 7. Recommended File Structure / 建議檔案結構

The first implementation should stay inside the current single project. Use folders and namespaces
to protect the boundary before splitting NuGet packages.

Current repo root:

```text
D:\projects\MyFhirSdk
```

The main idea is:

```text
Validation/Rules
  = base FHIR validation rules only

Validation/Profiles
  = generic profile/IG validation framework

ImplementationGuides/TwCore
  = TW Core-specific package, constants, rules, helpers, and fixtures
```

Recommended current layout:

```text
Validation/
  FhirValidator.cs
  IFhirValidator.cs
  ValidationResult.cs
  ValidationIssue.cs
  ValidationIssueCode.cs
  ValidationRuleSource.cs

  Rules/
    ResourceRuleRegistry.cs             # base FHIR rules only
    IFhirValidationRule.cs
    RequiredFieldRule.cs
    CardinalityRule.cs
    ChoiceElementRule.cs
    PrimitiveFormatRule.cs

  Profiles/                             # new generic profile framework
    ProfileValidator.cs
    IImplementationGuidePackage.cs
    IProfileValidationRule.cs
    ProfileValidationContext.cs
    ProfileValidationOptions.cs

  Traversal/
    FhirObjectGraphWalker.cs
    FhirObjectGraphNode.cs
    FhirPathFormatter.cs

ImplementationGuides/
  TwCore/
    TwCorePackage.cs
    TwCoreProfiles.cs
    Validation/
      TwCorePatientRules.cs
      TwCoreOrganizationRules.cs
      TwCorePractitionerRules.cs
    Terminology/
      TwCoreValueSets.cs
    Extensions/
      TwCoreExtensions.cs
    Search/
      TwCoreSearch.cs

Tests/
  ImplementationGuides/
    TwCore/
      TwCorePackageTests.cs
      Validation/
        TwCorePatientValidationTests.cs
      Fixtures/
```

Responsibility map:

| Path | Responsibility | May reference |
|---|---|---|
| `Validation/FhirValidator.cs` | Base FHIR validation entry point. | `Validation/Rules`, `Validation/Traversal` |
| `Validation/Rules/` | Base FHIR rules only. | Base SDK types |
| `Validation/Profiles/` | Generic IG/profile validation framework. | Base validator, base SDK types |
| `ImplementationGuides/TwCore/` | TW Core-specific package and rules. | Base SDK, `Validation/Profiles` |
| `ImplementationGuides/TwCore/Validation/` | TW Core profile validation rules. | TW Core constants, base SDK models |
| `ImplementationGuides/TwCore/Terminology/` | TW Core ValueSet/CodeSystem references. | Base SDK terminology abstractions later |
| `ImplementationGuides/TwCore/Extensions/` | Typed TW Core extension helpers. | Base `Extension` model |
| `ImplementationGuides/TwCore/Search/` | Typed TW Core search helpers. | Base client/search query APIs |
| `Tests/ImplementationGuides/TwCore/` | TW Core-specific tests and fixtures. | SDK test helpers |

Boundary notes:

- `Validation/Rules` remains base FHIR only.
- `Validation/Profiles` contains generic IG/profile infrastructure, not TW Core-specific rules.
- `ImplementationGuides/TwCore` contains TW Core-specific constants, rules, helpers, and fixtures.
- `Core`, `Resources`, `Serialization`, `Client`, and `Validation/Rules` must not reference
  `ImplementationGuides/TwCore`.
- The existing `ResourceRuleRegistry` can remain as-is initially, but it should be treated as the
  base rule registry. A later rename to `BaseResourceRuleRegistry` may make the boundary clearer.

Future project split:

```text
src/
  MyFhirSdk/
    Core/
    Resources/
    Serialization/
    Client/
    Validation/

  MyFhirSdk.TwCore/
    TwCorePackage.cs
    TwCoreProfiles.cs
    Validation/
    Terminology/
    Extensions/
    Search/

tests/
  MyFhirSdk.Tests/
  MyFhirSdk.TwCore.Tests/
```

Future dependency direction:

```text
MyFhirSdk.TwCore -> MyFhirSdk
MyFhirSdk -> does not reference MyFhirSdk.TwCore
```

## 8. Core Interfaces

The base SDK should expose generic abstractions. Concrete IG packages should provide
implementations.

```csharp
public interface IImplementationGuidePackage
{
    string PackageId { get; }

    string Name { get; }

    string FhirVersion { get; }

    IReadOnlyCollection<string> SupportedProfiles { get; }

    bool SupportsProfile(string profileUrl);

    IEnumerable<IProfileValidationRule> GetRules(
        string profileUrl,
        Type resourceType);
}
```

```csharp
public interface IProfileValidationRule
{
    string RuleId { get; }

    void Validate(
        ProfileValidationContext context,
        ICollection<ValidationIssue> issues);
}
```

```csharp
public sealed class ProfileValidationContext
{
    public required Resource Resource { get; init; }

    public required string PackageId { get; init; }

    public required string ProfileUrl { get; init; }

    public required string RuleId { get; init; }
}
```

```csharp
public sealed class ProfileValidator
{
    public ProfileValidator(
        IFhirValidator baseValidator,
        params IImplementationGuidePackage[] packages);

    public ValidationResult Validate(Resource resource, string profileUrl);

    public ValidationResult Validate(Resource resource, IEnumerable<string> profileUrls);

    public ValidationResult ValidateDeclaredProfiles(Resource resource);
}
```

Open design choice:

- `ProfileValidator` can be concrete first. Add `IProfileValidator` later only if another
  implementation is needed.

## 9. ValidationIssue Metadata

To support IG validation, multiple IGs, and future business rules, `ValidationIssue` carries
rule source metadata.

Recommended model:

```csharp
public enum ValidationRuleSource
{
    BaseFhir,
    ImplementationGuide,
    BusinessRule
}
```

```csharp
public sealed class ValidationIssue
{
    public string Path { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;

    public ValidationIssueCode Code { get; init; }

    public ValidationRuleSource Source { get; init; } = ValidationRuleSource.BaseFhir;

    public string? PackageId { get; init; }

    public string? ProfileUrl { get; init; }

    public string? RuleId { get; init; }
}
```

Field meanings:

| Field | Meaning | Example |
|---|---|---|
| `Source` | Which validation layer produced the issue. | `BaseFhir`, `ImplementationGuide`, `BusinessRule` |
| `PackageId` | Which IG package produced the issue. Null for base rules. | `tw.gov.mohw.twcore#1.0.0` |
| `ProfileUrl` | Which profile produced the issue. Null for base rules. | `https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore` |
| `RuleId` | Stable identifier for the specific rule. | `TWCORE-PAT-002` |

Relationship:

```text
Package
  contains many ProfileUrls
    contains many Rules
      may produce ValidationIssues
```

Example base issue:

```text
Path: Coverage.status
Code: Required
Source: BaseFhir
PackageId: null
ProfileUrl: null
RuleId: VAL-REQ-002
```

Example IG issue:

```text
Path: Patient.identifier
Code: Required
Source: ImplementationGuide
PackageId: tw.gov.mohw.twcore#1.0.0
ProfileUrl: https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore
RuleId: TWCORE-PAT-002
```

Rules:

- Do not use `ValidationIssueCode` to distinguish base, IG, and business sources.
- Keep `PackageId`, `ProfileUrl`, and `RuleId` nullable so base validation remains simple.
- Base rules may add `RuleId` over time using existing validation inventory IDs.
- IG rules should always set `Source`, `PackageId`, `ProfileUrl`, and `RuleId`.

## 10. Explicit Profile Validation

Explicit profile validation is best for send-time validation, when the application knows the target
profile even if the resource has not declared `meta.profile`.

Example:

```csharp
var result = validator.Validate(
    patient,
    TwCoreProfiles.Patient);
```

Flow:

```text
1. Reject null resource.
2. Run base validation once.
3. Find the registered IG package that supports the requested profile URL.
4. Check package FHIR version compatibility.
5. Get rules for the profile URL and resource type.
6. Run profile rules.
7. Return combined ValidationResult.
```

Unknown profile behavior should be configurable:

```text
Ignore unknown profiles
Warn on unknown profiles
Error on unknown profiles
```

The recommended default for explicit validation is error, because the caller specifically requested
that profile.

## 11. Declared Profile Validation

Declared profile validation reads `Resource.Meta.Profile`.

Example:

```csharp
patient.Meta.Profile =
[
    TwCoreProfiles.Patient
];

var result = validator.ValidateDeclaredProfiles(patient);
```

Flow:

```text
1. Read resource.meta.profile.
2. Run base validation once.
3. For each declared profile, find a matching registered IG package.
4. Run all matching profile rules.
5. Return combined ValidationResult.
```

Declared profile validation is best for received resources, parsed server responses, and Bundles that
may contain resources from different profiles.

Recommended defaults:

- Empty `meta.profile`: run base validation only.
- Unknown declared profile: warning or ignored by option.
- Duplicate profile URL: validate once.
- Multiple profiles: validate in deterministic input order after de-duplication.

## 12. Multiple IG Support

The SDK should allow multiple IG packages to be registered:

```csharp
var validator = new ProfileValidator(
    new FhirValidator(),
    TwCorePackage.Default,
    ClaimExchangePackage.Default);
```

Supported scenarios:

- National core IG plus business exchange IG.
- Local hospital IG plus external exchange IG.
- Bundle containing resources that declare different profiles.
- One resource declaring more than one profile.

Conflict handling:

- The SDK should not silently resolve conflicting rules.
- Conflicts should surface as validation issues from the relevant profile rules.
- The caller decides how to handle invalid resources.
- Validation issue metadata must identify which package/profile/rule produced each issue.

## 13. Package, Profile, and Rule Concepts

Hierarchy:

```text
Package
  contains profiles
    contain rules
```

Package:

- A published or local IG unit.
- Contains profiles, extensions, ValueSets, CodeSystems, examples, SearchParameters, and other
  artifacts.
- Represented in code by `IImplementationGuidePackage`.

Profile URL:

- Canonical URL for one profile inside a package.
- Represents constraints for a resource or datatype.
- Represented in code by constants such as `TwCoreProfiles.Patient`.

Rule:

- A concrete validation check derived from a base rule, profile constraint, terminology binding,
  FHIRPath invariant, or business rule.
- Represented in code by `IProfileValidationRule` for profile rules.
- Identified in results by `RuleId`.

## 14. Profile Metadata

Each IG package should define profile canonical URLs as constants.

```csharp
public static class TwCoreProfiles
{
    public const string Patient =
        "https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore";

    public const string Organization =
        "https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Organization-twcore";
}
```

Resources can declare conformance with `Resource.Meta.Profile`.

The base serializer and parser should continue to handle `meta.profile` as normal FHIR data. They
should not contain IG-specific logic.

## 15. Slices

Slices are profile-specific classifications of repeated elements.

Example:

```text
Patient.identifier 0..*
  slice: nationalId
  slice: medicalRecordNumber
```

The base model should remain:

```text
Patient.Identifier : IList<Identifier>
```

The IG validator is responsible for:

- Determining which list item belongs to which slice.
- Applying slice-specific required fields.
- Checking required slices.
- Reporting profile-specific issues with `Source`, `PackageId`, `ProfileUrl`, and `RuleId`.

Discriminators may use values such as:

- `identifier.system`
- `identifier.type.coding.system`
- `identifier.type.coding.code`
- `extension.url`

Do not create base model properties such as `Patient.NationalIdIdentifier` for profile slices.

## 16. Extension Helpers

The base SDK supports generic extensions:

```text
Extension.url
Extension.value[x]
```

The IG layer may provide typed helpers:

```csharp
TwCoreExtensions.SetSomeExtension(patient, value);
TwCoreExtensions.GetSomeExtension(patient);
```

Rules:

- Extension helpers should use the base SDK `Extension` model.
- Serializer and parser should remain IG-neutral.
- Extension URLs should be constants in the IG layer.
- Extension validation should live in profile validation rules, not in base resources.

## 17. Terminology Bindings

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

The first IG implementation may store terminology references but defer membership checks.

Out of scope initially:

- Remote terminology server calls.
- ValueSet expansion.
- Required binding validation across full StructureDefinition definitions.

## 18. Search Helpers

The base SDK should remain responsible for generic search query construction and HTTP execution.

The IG layer may provide typed search helpers:

```csharp
TwCoreSearch.PatientByIdentifier("MRN-123");
```

Search helpers should return generic search query objects and delegate execution to the base SDK
`FhirClient`.

## 19. IG Examples and Fixtures

IG support should be tested with JSON examples.

Recommended test flow:

```text
IG fixture JSON
  -> Base parser
  -> ProfileValidator
  -> Base serializer
  -> JSON comparison
```

Fixtures should be separated from base FHIR fixtures when possible.

Example folder:

```text
Tests/ImplementationGuides/TwCore/Fixtures/
```

## 20. Client Integration

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
TwCoreFhirClient
  -> validates selected profiles
  -> optionally sets meta.profile
  -> delegates HTTP to base FhirClient
```

Recommended first version:

```text
Keep base FhirClient unchanged.
Perform IG validation outside the client or in an optional wrapper.
```

## 21. Implementation Phases

### Phase 1 - Validation Issue Metadata (implemented)

- `ValidationRuleSource` exists.
- `ValidationIssue` carries nullable `PackageId`, `ProfileUrl`, and `RuleId`.
- Default `Source` is `BaseFhir`.
- Result contract tests cover metadata preservation and default source behavior.

### Phase 2 - Generic Profile Framework

- Add `Validation/Profiles`.
- Add `IImplementationGuidePackage`.
- Add `IProfileValidationRule`.
- Add `ProfileValidationContext`.
- Add `ProfileValidator`.
- Support explicit single-profile validation.

### Phase 3 - Multiple Profiles and Packages

- Allow multiple IG packages in one `ProfileValidator`.
- Allow multiple profile URLs in one validation call.
- Run base validation once.
- Add unknown-profile behavior options.

### Phase 4 - TW Core Manual Package POC

- Add `ImplementationGuides/TwCore`.
- Add `TwCorePackage`.
- Add `TwCoreProfiles`.
- Add one or two manual TW Core Patient rules.
- Add TW Core profile validation tests.

### Phase 5 - Declared Profile Validation

- Read `meta.profile`.
- Validate against known declared profiles.
- De-duplicate profile URLs.
- Define unknown declared profile behavior.

### Phase 6 - Fixtures, Extensions, and Search Helpers

- Add IG-specific JSON fixtures.
- Add typed extension helpers when needed.
- Add typed search helpers when needed.

### Phase 7 - Terminology and StructureDefinition Loading

- Add local ValueSet support.
- Add terminology validation abstraction.
- Consider loading official IG package artifacts.
- Keep full StructureDefinition-driven validation out of the first implementation.

## 22. Open Questions

- Which concrete IG should be implemented first?
- What FHIR version compatibility policy should `ProfileValidator` enforce?
- Should unknown declared profiles be ignored, warnings, or errors by default?
- Should explicit unknown profiles always be errors?
- Should profile validators set `meta.profile`, or only validate?
- Should IG support stay in the main package initially or move to separate NuGet packages earlier?
- Should base rules start assigning `RuleId` now, or only after IG support is added?
- Should `PackageId` remain `name#version`, or split into `PackageId` and `PackageVersion` later?
- Should profile validation return one combined result or grouped results by profile?
