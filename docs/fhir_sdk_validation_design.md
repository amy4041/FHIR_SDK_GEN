# FHIR SDK Validation Layer Design

This document proposes the MVP implementation order and scope for the validation layer.
It is intended to follow the existing SDK architecture and to keep validation independent
from the REST client until the standalone validation behavior is stable.

## 1. Goals

The MVP validation layer should provide:

- Required field validation.
- Primitive format validation.
- Basic cardinality validation.
- Structured validation results that SDK callers can inspect.
- Optional client-side validation before `CreateAsync` and `UpdateAsync` requests.

The validation layer should not become a profile validator in MVP. It should validate the
SDK object graph against the base FHIR R5 rules that are already represented in the current
model surface.

## 2. Out of Scope for MVP

Do not include the following in the MVP validation layer:

- Profile validation.
- StructureDefinition validation.
- FHIRPath rule execution.
- Terminology service calls.
- ValueSet expansion.
- External server-side validation.
- Cross-resource reference resolution.
- Business workflow rules.
- Automatic code generation from FHIR definitions.

These can be future enhancements after the SDK has a stable model, serialization, client,
and basic validation base.

## 3. Recommended Module Shape

Use a dedicated `MyFhirSdk.Validation` namespace so validation can be used independently
from the REST client.

Recommended folder shape:

```text
Validation
|-- IFhirValidator.cs
|-- FhirValidator.cs
|-- ValidationResult.cs
|-- ValidationIssue.cs
|-- ValidationSeverity.cs
|-- ValidationIssueCode.cs
|-- Rules
|   |-- IFhirValidationRule.cs
|   |-- RequiredFieldRule.cs
|   |-- CardinalityRule.cs
|   |-- ChoiceElementRule.cs
|   `-- ResourceRuleRegistry.cs
`-- Traversal
    |-- FhirObjectGraphWalker.cs
    `-- FhirPathFormatter.cs
```

The exact file split can be adjusted during implementation, but the responsibilities should
stay separate:

- Public API and result types.
- Object graph traversal.
- Primitive validation.
- Resource-specific rules.
- Client integration.

## 4. Public API

Start with a small API:

```csharp
public interface IFhirValidator
{
    ValidationResult Validate(Resource resource);
}
```

Recommended result model:

```csharp
public sealed class ValidationResult
{
    public bool IsValid => Issues.Count == 0;
    public IReadOnlyList<ValidationIssue> Issues { get; }
}

public sealed class ValidationIssue
{
    public string Path { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;
    public ValidationIssueCode Code { get; init; }
}

public enum ValidationSeverity
{
    Information,
    Warning,
    Error
}

public enum ValidationIssueCode
{
    Required,
    Cardinality,
    PrimitiveFormat,
    ChoiceElement
}
```

Normal validation failures should be returned as `ValidationResult`, not thrown. Exceptions
should be reserved for invalid SDK usage, such as calling `Validate(null)`.

## 5. Implementation Order

### Step 1 - Add validation result model

Create the public result model before adding rules.

Deliverables:

- `IFhirValidator`.
- `ValidationResult`.
- `ValidationIssue`.
- `ValidationSeverity`.
- `ValidationIssueCode`.
- Tests proving an empty issue list is valid and issue lists are preserved.

This gives the rest of the implementation a stable contract.

### Step 2 - Implement primitive format validation

Primitive validation should be the first real validation behavior because primitive types
already expose local `IsValid()` methods.

Initial primitive scope:

- `FhirString`.
- `FhirUri`.
- `FhirCanonical`.
- `FhirCode`.
- `FhirId`.
- `FhirInteger`.
- `FhirDecimal`.
- `FhirDate`.
- `FhirDateTime`.
- `FhirInstant`.
- `FhirBoolean`.

Recommended behavior:

- Walk the resource object graph recursively.
- Visit nested `DataType`, `BackboneElement`, and `Resource` objects.
- Visit list items with indexed paths, for example `Patient.name[0].given[1]`.
- Call the primitive's existing `IsValid()` method.
- Emit a `PrimitiveFormat` issue when a primitive value is invalid.

Example issue paths:

```text
Patient.id
Patient.birthDate
Claim.created
Bundle.timestamp
Patient.name[0].given[0]
```

Implementation note:

- Prefer using reflection only for traversal and keep validation decisions in explicit
  rule classes.
- Avoid adding validation logic directly into resource classes.
- Guard against cycles or repeated references during traversal.

### Step 3 - Add explicit resource rule registry

Required field and cardinality rules should be explicit. Do not infer required fields from
nullable reference types or list initialization alone.

Recommended approach:

```text
ResourceRuleRegistry
  Patient -> rules
  Organization -> rules
  Practitioner -> rules
  Encounter -> rules
  Coverage -> rules
  Claim -> rules
  Bundle -> rules
```

Why explicit rules:

- The current model classes do not carry cardinality metadata.
- Some FHIR fields are optional in base resources but required by local workflows.
- Explicit rules are easier to review against the FHIR R5 specification.
- MVP avoids code generation and StructureDefinition parsing.

Before implementing resource-specific required fields, verify each rule against the official
FHIR R5 base resource definition. Keep profile-specific requirements out of this registry.

### Step 4 - Implement required field validation

Required field rules should check that a singleton value is not null and a repeated value has
at least one item.

Recommended first resource targets:

- `Bundle`, because search parsing and client search already depend on Bundle.
- `Claim`, because PRD success criteria include claim exchange.
- `Coverage`, because Claim exchange usually depends on coverage data.
- `Encounter`, because it is one of the MVP clinical/administrative resources.

Patient, Organization, and Practitioner should still be covered by primitive traversal even
if their base-resource required rules are minimal.

Example rule shape:

```csharp
RequiredFieldRule.For<Bundle>("type", bundle => bundle.Type);
RequiredFieldRule.For<Claim>("status", claim => claim.Status);
RequiredFieldRule.ForList<Claim>("item", claim => claim.Item);
```

Example issues:

```text
Bundle.type is required.
Claim.status is required.
Claim.item must contain at least one item.
```

### Step 5 - Implement basic cardinality validation

Many cardinality constraints are already represented by the model shape:

- Singleton fields are single properties.
- Repeated fields are `IList<T>`.

MVP cardinality should therefore focus on constraints that are not naturally enforced by
the current model:

- Required lists must not be empty.
- Required lists must not be null if a caller manually assigns null.
- Repeated fields should not contain null entries.
- Primitive wrapper objects with no value but with extensions should be treated as present
  only when that aligns with FHIR primitive extension rules.

Avoid adding broad max-count rules in MVP unless the current model exposes a field with a
known max greater than one that cannot be represented by the type system.

### Step 6 - Implement choice element validation

FHIR choice elements use names such as `deceased[x]` and `multipleBirth[x]`. The current
model represents these as separate properties.

MVP should verify that only one option in a choice group is populated.

Initial choice groups:

- `Patient.DeceasedBoolean` / `Patient.DeceasedDateTime`.
- `Patient.MultipleBirthBoolean` / `Patient.MultipleBirthInteger`.
- `Practitioner.DeceasedBoolean` / `Practitioner.DeceasedDateTime`.

Example issue:

```text
Patient.deceased[x] allows only one value, but DeceasedBoolean and DeceasedDateTime were both set.
```

Choice validation should be implemented with explicit rules in the registry, not by naming
convention alone.

### Step 7 - Add client opt-in integration

Only after standalone validation is stable, connect it to the REST client.

Existing client option:

```csharp
public bool ValidateBeforeSend { get; init; } = false;
```

Recommended client behavior:

- Validation is disabled by default.
- When enabled, validate before `CreateAsync` and `UpdateAsync`.
- Do not validate `ReadAsync`, because it only sends a resource id.
- Do not validate `SearchAsync` resource instances, because it does not send a resource body.
- If validation fails, do not send the HTTP request.
- Throw a client-side `FhirValidationException` containing the full `ValidationResult`.

Recommended exception shape:

```csharp
public sealed class FhirValidationException : FhirSdkException
{
    public ValidationResult Result { get; }
}
```

Standalone validation should still return `ValidationResult`; the exception is only for
client operations where the public API cannot return both a resource response and validation
issues.

## 6. Test Strategy

Create a validation test inventory similar to the client test inventory once implementation
starts. Keep tests small and rule-focused.

Recommended test areas:

| Area | Scenario | Priority |
|---|---|---|
| Result Model | Empty result is valid | MVP |
| Result Model | Issues make result invalid | MVP |
| Primitive Format | Invalid `FhirId` reports path | MVP |
| Primitive Format | Invalid `FhirDate` reports path | MVP |
| Primitive Format | Nested primitive path includes list index | MVP |
| Required Fields | Required singleton missing reports issue | MVP |
| Required Fields | Required list empty reports issue | MVP |
| Cardinality | Null item inside repeated field reports issue | MVP |
| Choice Elements | Two populated choice values report issue | MVP |
| Traversal | Null optional fields are ignored | MVP |
| Traversal | Nested datatypes are visited | MVP |
| Client Integration | `ValidateBeforeSend=false` does not validate | MVP |
| Client Integration | `CreateAsync` invalid resource does not send HTTP | MVP |
| Client Integration | `UpdateAsync` invalid resource does not send HTTP | MVP |

Test style should follow the current lightweight console-runner approach unless the project
moves to xUnit or NUnit later.

## 7. Suggested MVP Milestones

### Milestone 1 - Standalone validation contract

Outcome:

- Public validation API exists.
- Result and issue model exists.
- Tests cover result behavior.

### Milestone 2 - Primitive validation

Outcome:

- Validator can walk a full resource graph.
- Existing primitive `IsValid()` methods are used.
- Invalid nested primitive values produce useful paths.

### Milestone 3 - Resource required rules

Outcome:

- Rule registry exists.
- MVP resource required rules are explicit and test-covered.
- Bundle and Claim have initial required-field coverage.

### Milestone 4 - Cardinality and choice rules

Outcome:

- Empty required lists are caught.
- Null list entries are caught.
- Initial choice groups are caught.

### Milestone 5 - Client integration

Outcome:

- `ValidateBeforeSend` is wired into `CreateAsync` and `UpdateAsync`.
- Invalid resources stop before serialization/request send.
- Full validation details are preserved in a client-side exception.

## 8. Design Principles

- Keep validation independent from serialization and HTTP.
- Return structured validation results for normal validation failures.
- Keep resource classes simple data models.
- Prefer explicit rules over hidden convention.
- Start with base FHIR R5 rules only.
- Make every issue path useful to SDK callers.
- Keep MVP validation deterministic and offline.

## 9. Future Enhancements

After MVP, the validation layer can grow in these directions:

- Profile validation.
- StructureDefinition-driven validation.
- FHIRPath invariant execution.
- Terminology validation.
- ValueSet expansion.
- Remote `$validate` operation helper.
- Rule generation from official FHIR definitions.
- Severity customization.
- Validation options for strict vs. lenient modes.

