# FHIR SDK Validation Rule Inventory

This document tracks MVP validation rules and their planned test coverage.
It combines validation rule inventory and test coverage inventory so each rule can move from
`Planned` to `Covered` as implementation progresses.

## Columns

| Column | Description |
|---|---|
| ID | Stable validation rule identifier, for example `VAL-PRIM-001`. |
| Area | Validation area, such as `Primitive Format`, `Required Field`, `Cardinality`, `Choice Element`, `Traversal`, or `Client Integration`. |
| Rule Type | Expected `ValidationIssueCode`, or `Contract` when the row is about API behavior. |
| Target | Resource, datatype, primitive, or SDK API under validation. |
| Rule / Cardinality | FHIR base rule or SDK rule being enforced. |
| Invalid Condition | Input condition that should trigger the rule. |
| Expected Issue | Expected issue code, severity, and path. |
| Source | Rule source/origin. For rows that emit or construct `ValidationIssue`, this maps to `ValidationIssue.Source`; use `N/A` when no issue source is expected. |
| Covered By | Test method name that covers the rule. |
| Test File | Test file where coverage should live. |
| Priority | `MVP`, `Next`, or `Future`. |
| Status | `Planned`, `Covered`, `Deferred`, or `Blocked`. |
| Notes | Extra context, assumptions, boundaries, or follow-up details. |

## Status Values

| Status | Meaning |
|---|---|
| Planned | Intended rule or test, not implemented yet. |
| Covered | Rule is implemented and has test coverage. |
| Deferred | Known rule or behavior, intentionally outside the current MVP. |
| Blocked | Rule cannot be implemented until a model/API gap is resolved. |

## Rule Source Values

Use the `Source` column to identify where a rule comes from. For rows that emit a
`ValidationIssue`, this value should match `ValidationIssue.Source`.

| Source | ValidationIssue value | Meaning |
|---|---|---|
| BaseFhir | `ValidationRuleSource.BaseFhir` | Rule comes from the base FHIR SDK validation layer. |
| ImplementationGuide | `ValidationRuleSource.ImplementationGuide` | Rule comes from a concrete Implementation Guide or profile. |
| BusinessRule | `ValidationRuleSource.BusinessRule` | Rule comes from a project, workflow, hospital, or exchange-specific requirement. |
| N/A | N/A | Contract or documentation row that does not expect a validation issue source. |

## Result Contract

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-CON-001 | Result Contract | Contract | `ValidationResult` | Empty result is valid | No issues are present | N/A | N/A | `EmptyIssuesIsValid` | `Tests/Validation/ValidationResultTests.cs` | MVP | Covered | Establishes the result contract before rules are added. |
| VAL-CON-002 | Result Contract | Contract | `ValidationResult` | Any issue makes result invalid | One or more issues are present | N/A | N/A | `IssuesMakeResultInvalid` | `Tests/Validation/ValidationResultTests.cs` | MVP | Covered | `IsValid` should reflect issue count. |
| VAL-CON-003 | Result Contract | Contract | `ValidationIssue` | Issue preserves path, message, severity, code, source, package, profile, and rule metadata | Issue is created with all fields populated | N/A | ImplementationGuide | `PreservesIssueDetails` | `Tests/Validation/ValidationResultTests.cs` | MVP | Covered | Keeps issue output predictable for SDK callers and IG/profile validation. |
| VAL-CON-004 | Result Contract | Contract | `ValidationIssue` | Issue defaults to base FHIR source with no IG metadata | Issue is created without source metadata | N/A | BaseFhir | `IssueDefaultsToBaseFhirSource` | `Tests/Validation/ValidationResultTests.cs` | MVP | Covered | Keeps existing base validation rules simple while allowing IG metadata later. |
| VAL-CON-005 | Result Contract | Contract | `IFhirValidator.Validate` | Null resource is invalid SDK usage | `Validate(null)` is called | Throws `ArgumentNullException` | N/A | `ValidateRejectsNullResource` | `Tests/Validation/FhirValidatorTests.cs` | MVP | Covered | Normal validation failures return `ValidationResult`; null input can throw. |

## Traversal and Generic Cardinality

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-TRAV-001 | Traversal | Contract | Resource object graph | Validator visits nested `DataType`, `BackboneElement`, and `Resource` objects | Nested invalid primitive exists | Primitive issue emitted at nested path | BaseFhir | `ValidateReportsIndexedPathForNestedPrimitive` | `Tests/Validation/Traversal/FhirObjectGraphWalkerTests.cs` | MVP | Covered | Required before most rule coverage is meaningful. |
| VAL-TRAV-002 | Traversal | Contract | Repeated fields | List item paths include indexes | `Patient.Name[0].Given[0]` contains invalid primitive | `PrimitiveFormat/Error/Patient.name[0].given[0]` | BaseFhir | `ValidateReportsIndexedPathForNestedPrimitive` | `Tests/Validation/Traversal/FhirObjectGraphWalkerTests.cs` | MVP | Covered | Path formatting should use FHIR-style lower camel case. |
| VAL-TRAV-003 | Traversal | Contract | Optional fields | Null optional fields are ignored | Optional property is null | No issue | N/A | `ValidateIgnoresNullOptionalFields` | `Tests/Validation/Traversal/FhirObjectGraphWalkerTests.cs` | MVP | Covered | Avoid noisy validation output for optional fields. |
| VAL-CARD-001 | Cardinality | Cardinality | Repeated field property | Repeated fields should be list instances | Caller manually assigns repeated property to null | `Cardinality/Error/{path}` | BaseFhir | `ValidateReportsNullRepeatedField` | `Tests/Validation/Rules/CardinalityRuleTests.cs` | MVP | Covered | Protects serializer/client callers from null list surprises. |
| VAL-CARD-002 | Cardinality | Cardinality | Repeated field item | Repeated fields should not contain null items | List contains a null item | `Cardinality/Error/{path}[index]` | BaseFhir | `ValidateReportsNullRepeatedItem` | `Tests/Validation/Rules/CardinalityRuleTests.cs` | MVP | Covered | Applies to all `IList<T>` resource/datatype fields. |
| VAL-CARD-003 | Cardinality | Cardinality | Required repeated field | Required list must have at least one item | Required list is empty | `Cardinality/Error/{path}` | BaseFhir | `ValidateReportsEmptyRequiredRepeatedField` | `Tests/Validation/Rules/CardinalityRuleTests.cs` | MVP | Covered | Used by explicit required-list rules only. |
| VAL-TRAV-004 | Traversal | Contract | Object graph cycle guard | Traversal should not recurse forever | Same object reference appears more than once | No infinite recursion | N/A |  | `Tests/Validation/Traversal/FhirObjectGraphWalkerTests.cs` | Next | Planned | Useful defensive behavior if references are reused in object graphs. |

## Primitive Format Rules

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-PRIM-001 | Primitive Format | PrimitiveFormat | `Resource.Id` | Resource id follows FHIR `id` format | `Id = "a/b"` | `PrimitiveFormat/Error/Patient.id` | BaseFhir | `ValidateReportsInvalidResourceId` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | `Resource.Id` is currently `string?`, so validate with FHIR id rules. |
| VAL-PRIM-002 | Primitive Format | PrimitiveFormat | `FhirString` | String must be non-empty and avoid invalid control characters | `new FhirString("")` or invalid control char | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsIndexedPathForNestedPrimitive` | `Tests/Validation/Traversal/FhirObjectGraphWalkerTests.cs` | MVP | Covered | Uses existing `FhirString.IsValid()`. |
| VAL-PRIM-003 | Primitive Format | PrimitiveFormat | `FhirMarkdown` | Markdown follows FHIR string-like constraints | Empty value or invalid control char | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirMarkdown` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirMarkdown.IsValid()`. |
| VAL-PRIM-004 | Primitive Format | PrimitiveFormat | `FhirCode` | Code has no leading/trailing whitespace and no repeated whitespace | `new FhirCode(" entered-in-error ")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirCode` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Does not validate terminology membership in MVP. |
| VAL-PRIM-005 | Primitive Format | PrimitiveFormat | `FhirUri` | URI has no whitespace and is relative or absolute | `new FhirUri("bad uri")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirUri` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirUri.IsValid()`. |
| VAL-PRIM-006 | Primitive Format | PrimitiveFormat | `FhirUrl` | URL has no whitespace and is absolute | `new FhirUrl("Patient/123")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirUrl` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirUrl.IsValid()`. |
| VAL-PRIM-007 | Primitive Format | PrimitiveFormat | `FhirCanonical` | Canonical is absolute or fragment, optionally versioned | `new FhirCanonical("relative/path")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirCanonical` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirCanonical.IsValid()`. |
| VAL-PRIM-008 | Primitive Format | PrimitiveFormat | `FhirId` | Id allows ASCII letters, digits, `-`, `.`, max 64 chars | `new FhirId("a/b")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirId` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Applies to primitive wrapper values; see `VAL-PRIM-001` for `Resource.Id`. |
| VAL-PRIM-009 | Primitive Format | PrimitiveFormat | `FhirDate` | Date supports `YYYY`, `YYYY-MM`, or `YYYY-MM-DD` | `new FhirDate("2026-99-99")` | `PrimitiveFormat/Error/Patient.birthDate` | BaseFhir | `ValidateReportsInvalidFhirDate` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirDate.IsValid()`. |
| VAL-PRIM-010 | Primitive Format | PrimitiveFormat | `FhirDateTime` | Time value requires timezone and valid date components | `new FhirDateTime("2026-05-29T10:30:00")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirDateTime` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirDateTime.IsValid()`. |
| VAL-PRIM-011 | Primitive Format | PrimitiveFormat | `FhirInstant` | Instant requires full date-time with timezone | `new FhirInstant("2026-05-29")` | `PrimitiveFormat/Error/Bundle.timestamp` | BaseFhir | `ValidateReportsInvalidFhirInstant` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirInstant.IsValid()`. |
| VAL-PRIM-012 | Primitive Format | PrimitiveFormat | `FhirDecimal` | Decimal literal follows FHIR decimal limits | `new FhirDecimal("01.20")` or excess digits | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirDecimal` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirDecimal.IsValid()`. |
| VAL-PRIM-013 | Primitive Format | PrimitiveFormat | `FhirInteger64` | Integer64 literal is parseable signed 64-bit integer | Invalid literal or overflow literal | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirInteger64` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirInteger64.IsValid()`. |
| VAL-PRIM-014 | Primitive Format | PrimitiveFormat | `FhirPositiveInt` | Positive int must be greater than zero | `new FhirPositiveInt(0)` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidPositiveInt` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Important for Claim sequence fields. |
| VAL-PRIM-015 | Primitive Format | PrimitiveFormat | `FhirUnsignedInt` | Unsigned int must be zero or greater | `new FhirUnsignedInt(-1)` | `PrimitiveFormat/Error/Bundle.total` | BaseFhir | `ValidateReportsInvalidUnsignedInt` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Important for Bundle search totals. |
| VAL-PRIM-016 | Primitive Format | PrimitiveFormat | `FhirBase64Binary` | Base64 value must be valid base64 without whitespace | `new FhirBase64Binary("not base64")` | `PrimitiveFormat/Error/{path}` | BaseFhir | `ValidateReportsInvalidFhirBase64Binary` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Uses existing `FhirBase64Binary.IsValid()`. |
| VAL-PRIM-017 | Primitive Format | PrimitiveFormat | `FhirBoolean` and `FhirInteger` | Wrapper values are always valid when representable by .NET type | Valid false or integer value | No issue | N/A | `ValidateDoesNotReportIssueForValidBooleanAndInteger` | `Tests/Validation/Rules/PrimitiveFormatRuleTests.cs` | MVP | Covered | Ensures traversal handles always-valid primitive wrappers without false positives. |

## Top-Level Required Field Rules

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-REQ-001 | Required Field | Required | `Bundle.Type` | `Bundle.type` is `1..1` | `Type` is null | `Required/Error/Bundle.type` | BaseFhir | `ValidateReportsMissingBundleType` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-002 | Required Field | Required | `Coverage.Status` | `Coverage.status` is `1..1` | `Status` is null | `Required/Error/Coverage.status` | BaseFhir | `ValidateReportsMissingCoverageTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-003 | Required Field | Required | `Coverage.Kind` | `Coverage.kind` is `1..1` | `Kind` is null | `Required/Error/Coverage.kind` | BaseFhir | `ValidateReportsMissingCoverageTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-004 | Required Field | Required | `Coverage.Beneficiary` | `Coverage.beneficiary` is `1..1` | `Beneficiary` is null | `Required/Error/Coverage.beneficiary` | BaseFhir | `ValidateReportsMissingCoverageTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-005 | Required Field | Required | `Encounter.Status` | `Encounter.status` is `1..1` | `Status` is null | `Required/Error/Encounter.status` | BaseFhir | `ValidateReportsMissingEncounterStatus` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-006 | Required Field | Required | `Claim.Status` | `Claim.status` is `1..1` | `Status` is null | `Required/Error/Claim.status` | BaseFhir | `ValidateReportsMissingClaimTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-007 | Required Field | Required | `Claim.Type` | `Claim.type` is `1..1` | `Type` is null | `Required/Error/Claim.type` | BaseFhir | `ValidateReportsMissingClaimTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-008 | Required Field | Required | `Claim.Use` | `Claim.use` is `1..1` | `Use` is null | `Required/Error/Claim.use` | BaseFhir | `ValidateReportsMissingClaimTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-009 | Required Field | Required | `Claim.Patient` | `Claim.patient` is `1..1` | `Patient` is null | `Required/Error/Claim.patient` | BaseFhir | `ValidateReportsMissingClaimTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-010 | Required Field | Required | `Claim.Created` | `Claim.created` is `1..1` | `Created` is null | `Required/Error/Claim.created` | BaseFhir | `ValidateReportsMissingClaimTopLevelFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 required field. |
| VAL-REQ-011 | Required Field | Contract | `Patient` | No base FHIR R5 Patient field in current SDK model is `1..1` | Optional fields are null | No issue | N/A | `ValidateDoesNotRequirePatientOptionalFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Still validate primitive fields when present. |
| VAL-REQ-012 | Required Field | Contract | `Organization` | No base FHIR R5 Organization field in current SDK model is `1..1` | Optional fields are null | No issue | N/A | `ValidateDoesNotRequireOrganizationOptionalFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Still validate primitive fields when present. |
| VAL-REQ-013 | Required Field | Contract | `Practitioner` | No base FHIR R5 Practitioner field in current SDK model is `1..1` | Optional fields are null | No issue | N/A | `ValidateDoesNotRequirePractitionerOptionalFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Still validate primitive fields when present. |

## Nested Required Field Rules

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-NREQ-001 | Required Field | Required | `Bundle.Link[].Relation` | `Bundle.link.relation` is `1..1` when link exists | Link item has null relation | `Required/Error/Bundle.link[0].relation` | BaseFhir | `ValidateReportsMissingBundleLinkFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 nested required field. |
| VAL-NREQ-002 | Required Field | Required | `Bundle.Link[].Url` | `Bundle.link.url` is `1..1` when link exists | Link item has null URL | `Required/Error/Bundle.link[0].url` | BaseFhir | `ValidateReportsMissingBundleLinkFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 nested required field. |
| VAL-NREQ-003 | Required Field | Required | `Coverage.PaymentBy[].Party` | `Coverage.paymentBy.party` is `1..1` when paymentBy exists | PaymentBy item has null party | `Required/Error/Coverage.paymentBy[0].party` | BaseFhir | `ValidateReportsMissingCoveragePaymentByParty` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 nested required field. |
| VAL-NREQ-004 | Required Field | Required | `Coverage.Class[].Type` | `Coverage.class.type` is `1..1` when class exists | Class item has null type | `Required/Error/Coverage.class[0].type` | BaseFhir | `ValidateReportsMissingCoverageClassFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 nested required field. |
| VAL-NREQ-005 | Required Field | Required | `Coverage.Class[].Value` | `Coverage.class.value` is `1..1` when class exists | Class item has null value | `Required/Error/Coverage.class[0].value` | BaseFhir | `ValidateReportsMissingCoverageClassFields` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 nested required field. |
| VAL-NREQ-006 | Required Field | Required | `Encounter.Location[].Location` | `Encounter.location.location` is `1..1` when location exists | Location item has null location reference | `Required/Error/Encounter.location[0].location` | BaseFhir | `ValidateReportsMissingEncounterLocation` | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | MVP | Covered | Base FHIR R5 nested required field. |
| VAL-NREQ-007 | Required Field | Required | `Claim.Payee.Type` | `Claim.payee.type` is `1..1` when payee exists | Payee exists with null type | `Required/Error/Claim.payee.type` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Nested Claim rule; useful after top-level Claim rules are covered. |
| VAL-NREQ-008 | Required Field | Required | `Claim.Event[].Type` | `Claim.event.type` is `1..1` when event exists | Event item has null type | `Required/Error/Claim.event[0].type` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Requires Claim nested rule registry coverage. |
| VAL-NREQ-009 | Required Field | Required | `Claim.CareTeam[].Sequence` | `Claim.careTeam.sequence` is `1..1` when careTeam exists | CareTeam item has null sequence | `Required/Error/Claim.careTeam[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Also covered by positiveInt primitive validation when present. |
| VAL-NREQ-010 | Required Field | Required | `Claim.CareTeam[].Provider` | `Claim.careTeam.provider` is `1..1` when careTeam exists | CareTeam item has null provider | `Required/Error/Claim.careTeam[0].provider` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-011 | Required Field | Required | `Claim.SupportingInfo[].Sequence` | `Claim.supportingInfo.sequence` is `1..1` when supportingInfo exists | SupportingInfo item has null sequence | `Required/Error/Claim.supportingInfo[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-012 | Required Field | Required | `Claim.SupportingInfo[].Category` | `Claim.supportingInfo.category` is `1..1` when supportingInfo exists | SupportingInfo item has null category | `Required/Error/Claim.supportingInfo[0].category` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-013 | Required Field | Required | `Claim.Diagnosis[].Sequence` | `Claim.diagnosis.sequence` is `1..1` when diagnosis exists | Diagnosis item has null sequence | `Required/Error/Claim.diagnosis[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-014 | Required Field | Required | `Claim.Procedure[].Sequence` | `Claim.procedure.sequence` is `1..1` when procedure exists | Procedure item has null sequence | `Required/Error/Claim.procedure[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-015 | Required Field | Required | `Claim.Insurance[].Sequence` | `Claim.insurance.sequence` is `1..1` when insurance exists | Insurance item has null sequence | `Required/Error/Claim.insurance[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Claim.insurance itself is `0..*` in base R5. |
| VAL-NREQ-016 | Required Field | Required | `Claim.Insurance[].Focal` | `Claim.insurance.focal` is `1..1` when insurance exists | Insurance item has null focal | `Required/Error/Claim.insurance[0].focal` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Claim.insurance itself is `0..*` in base R5. |
| VAL-NREQ-017 | Required Field | Required | `Claim.Insurance[].Coverage` | `Claim.insurance.coverage` is `1..1` when insurance exists | Insurance item has null coverage | `Required/Error/Claim.insurance[0].coverage` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Claim.insurance itself is `0..*` in base R5. |
| VAL-NREQ-018 | Required Field | Required | `Claim.Accident.Date` | `Claim.accident.date` is `1..1` when accident exists | Accident exists with null date | `Required/Error/Claim.accident.date` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-019 | Required Field | Required | `Claim.Item[].Sequence` | `Claim.item.sequence` is `1..1` when item exists | Item has null sequence | `Required/Error/Claim.item[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Claim.item itself is `0..*` in base R5. |
| VAL-NREQ-020 | Required Field | Cardinality | `Claim.Item[].BodySite[].Site` | `Claim.item.bodySite.site` is `1..*` when bodySite exists | BodySite site list is empty or null | `Cardinality/Error/Claim.item[0].bodySite[0].site` | BaseFhir | `ValidateReportsEmptyRequiredRepeatedField` | `Tests/Validation/Rules/CardinalityRuleTests.cs` | MVP | Covered | Required-list rule on nested backbone element; emitted as cardinality because the missing value is an empty `1..*` list. |
| VAL-NREQ-021 | Required Field | Required | `Claim.Item[].Detail[].Sequence` | `Claim.item.detail.sequence` is `1..1` when detail exists | Detail item has null sequence | `Required/Error/Claim.item[0].detail[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-022 | Required Field | Required | `Claim.Item[].Detail[].SubDetail[].Sequence` | `Claim.item.detail.subDetail.sequence` is `1..1` when subDetail exists | SubDetail item has null sequence | `Required/Error/Claim.item[0].detail[0].subDetail[0].sequence` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Base FHIR R5 nested required field. |
| VAL-NREQ-023 | Required Field | Required | `Bundle.Entry[].Request.Method` | `Bundle.entry.request.method` is `1..1` when request exists | Request exists with null method | `Required/Error/Bundle.entry[0].request.method` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Transaction/batch-focused; not needed before REST client validation. |
| VAL-NREQ-024 | Required Field | Required | `Bundle.Entry[].Request.Url` | `Bundle.entry.request.url` is `1..1` when request exists | Request exists with null URL | `Required/Error/Bundle.entry[0].request.url` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Transaction/batch-focused; not needed before REST client validation. |
| VAL-NREQ-025 | Required Field | Required | `Bundle.Entry[].Response.Status` | `Bundle.entry.response.status` is `1..1` when response exists | Response exists with null status | `Required/Error/Bundle.entry[0].response.status` | BaseFhir |  | `Tests/Validation/Rules/RequiredFieldRuleTests.cs` | Next | Planned | Transaction/batch-response-focused. |

## Choice Element Rules

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-CHO-001 | Choice Element | ChoiceElement | `Patient.deceased[x]` | At most one choice value | `DeceasedBoolean` and `DeceasedDateTime` are both set | `ChoiceElement/Error/Patient.deceased[x]` | BaseFhir | `ValidateReportsPatientDeceasedChoiceConflict` | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | MVP | Covered | Explicit choice group in current Patient model. |
| VAL-CHO-002 | Choice Element | ChoiceElement | `Patient.multipleBirth[x]` | At most one choice value | `MultipleBirthBoolean` and `MultipleBirthInteger` are both set | `ChoiceElement/Error/Patient.multipleBirth[x]` | BaseFhir | `ValidateReportsPatientMultipleBirthChoiceConflict` | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | MVP | Covered | Explicit choice group in current Patient model. |
| VAL-CHO-003 | Choice Element | ChoiceElement | `Practitioner.deceased[x]` | At most one choice value | `DeceasedBoolean` and `DeceasedDateTime` are both set | `ChoiceElement/Error/Practitioner.deceased[x]` | BaseFhir | `ValidateReportsPractitionerDeceasedChoiceConflict` | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | MVP | Covered | Explicit choice group in current Practitioner model. |
| VAL-CHO-004 | Choice Element | ChoiceElement | `Claim.event.when[x]` | Exactly one choice value when event exists | Event has neither or both `WhenDateTime` and `WhenPeriod` | `ChoiceElement/Error/Claim.event[0].when[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Combines required choice and one-of validation. |
| VAL-CHO-005 | Choice Element | ChoiceElement | `Claim.supportingInfo.timing[x]` | At most one choice value | Both `TimingDate` and `TimingPeriod` are set | `ChoiceElement/Error/Claim.supportingInfo[0].timing[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Optional choice group. |
| VAL-CHO-006 | Choice Element | ChoiceElement | `Claim.supportingInfo.value[x]` | At most one choice value | More than one `Value*` property is set | `ChoiceElement/Error/Claim.supportingInfo[0].value[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Optional choice group with several possible types. |
| VAL-CHO-007 | Choice Element | ChoiceElement | `Claim.diagnosis.diagnosis[x]` | Exactly one choice value when diagnosis exists | Diagnosis has neither or both codeable concept and reference | `ChoiceElement/Error/Claim.diagnosis[0].diagnosis[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Required choice group. |
| VAL-CHO-008 | Choice Element | ChoiceElement | `Claim.procedure.procedure[x]` | Exactly one choice value when procedure exists | Procedure has neither or both codeable concept and reference | `ChoiceElement/Error/Claim.procedure[0].procedure[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Required choice group. |
| VAL-CHO-009 | Choice Element | ChoiceElement | `Claim.accident.location[x]` | At most one choice value | Both `LocationAddress` and `LocationReference` are set | `ChoiceElement/Error/Claim.accident.location[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Optional choice group. |
| VAL-CHO-010 | Choice Element | ChoiceElement | `Claim.item.serviced[x]` | At most one choice value | Both `ServicedDate` and `ServicedPeriod` are set | `ChoiceElement/Error/Claim.item[0].serviced[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Optional choice group. |
| VAL-CHO-011 | Choice Element | ChoiceElement | `Claim.item.location[x]` | At most one choice value | More than one location choice is set | `ChoiceElement/Error/Claim.item[0].location[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Optional choice group. |
| VAL-CHO-012 | Choice Element | ChoiceElement | `Coverage.costToBeneficiary.value[x]` | Exactly one choice value when costToBeneficiary exists | Cost entry has neither or both value choices | `ChoiceElement/Error/Coverage.costToBeneficiary[0].value[x]` | BaseFhir |  | `Tests/Validation/Rules/ChoiceElementRuleTests.cs` | Next | Planned | Required choice group if cost entry exists. |

## Client Integration Rules

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-CLI-001 | Client Integration | Contract | `FhirClientOptions.ValidateBeforeSend` | Validation disabled by default | Option is false or omitted | No validation call; request can be sent | N/A |  | `Tests/Client/FhirClientValidationTests.cs` | MVP | Planned | Client should remain backward-compatible. |
| VAL-CLI-002 | Client Integration | Contract | `CreateAsync` | Validate before sending when enabled | Invalid resource and `ValidateBeforeSend=true` | Throws `FhirValidationException`; no HTTP request | N/A |  | `Tests/Client/FhirClientValidationTests.cs` | MVP | Planned | Exception should include full `ValidationResult`. |
| VAL-CLI-003 | Client Integration | Contract | `UpdateAsync` | Validate before sending when enabled | Invalid resource and `ValidateBeforeSend=true` | Throws `FhirValidationException`; no HTTP request | N/A |  | `Tests/Client/FhirClientValidationTests.cs` | MVP | Planned | Existing update id guard remains separate. |
| VAL-CLI-004 | Client Integration | Contract | `ReadAsync` | Read does not validate resource body | `ValidateBeforeSend=true` | No resource validation is attempted | N/A |  | `Tests/Client/FhirClientValidationTests.cs` | MVP | Planned | Read only sends resource type and id. |
| VAL-CLI-005 | Client Integration | Contract | `SearchAsync` | Search does not validate resource body | `ValidateBeforeSend=true` | No resource validation is attempted | N/A |  | `Tests/Client/FhirClientValidationTests.cs` | MVP | Planned | Search sends query parameters, not a resource body. |

## TW Core Patient Profile Rules

Initial TW Core Patient support is a manual L1 profile structural validation POC. TW Core v1.0.0 is
based on FHIR R4.0.1 while the current SDK models are FHIR R5-oriented, so these rules should not be
treated as full TW Core conformance.

Package/profile settings:

| Setting | Value |
|---|---|
| Source | ImplementationGuide |
| PackageId | `tw.gov.mohw.twcore#1.0.0` |
| ProfileUrl | `https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore` |
| Target resource | `Patient` |
| Validation level | L1 Profile Structural Validation |
| Terminology | Deferred |
| FHIRPath invariants | Deferred |
| Full slicing | Deferred |

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| TWCORE-PAT-001 | Profile Contract | Contract | `TwCorePackage` / `TwCoreProfiles.Patient` | `TwCorePackage` supports the TW Core Patient canonical URL | `TwCoreProfiles.Patient` cannot be resolved by the package | N/A | N/A |  | `Tests/ImplementationGuides/TwCore/TwCorePackageTests.cs` | Next | Planned | PackageId: `tw.gov.mohw.twcore#1.0.0`. ProfileUrl: `https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore`. Verifies profile metadata and lookup only. |
| TWCORE-PAT-002 | Profile Cardinality | Cardinality | `Patient.identifier` | TW Core Patient requires `Patient.identifier` `1..*` | `Identifier` is empty | `Cardinality/Error/Patient.identifier`; `Source=ImplementationGuide`; `PackageId=tw.gov.mohw.twcore#1.0.0`; `ProfileUrl=Patient-twcore`; `RuleId=TWCORE-PAT-002` | ImplementationGuide |  | `Tests/ImplementationGuides/TwCore/Validation/TwCorePatientValidationTests.cs` | Next | Planned | L1 structural rule. This must not be added to base `ResourceRuleRegistry` because base Patient can remain valid without identifier. |
| TWCORE-PAT-003 | Profile Required Field | Required | `Patient.identifier[].system` | TW Core Patient requires each identifier item to include `system` | Identifier item exists with null `System` | `Required/Error/Patient.identifier[0].system`; `Source=ImplementationGuide`; `PackageId=tw.gov.mohw.twcore#1.0.0`; `ProfileUrl=Patient-twcore`; `RuleId=TWCORE-PAT-003` | ImplementationGuide |  | `Tests/ImplementationGuides/TwCore/Validation/TwCorePatientValidationTests.cs` | Next | Planned | Applies only when validating against `TwCoreProfiles.Patient`. |
| TWCORE-PAT-004 | Profile Required Field | Required | `Patient.identifier[].value` | TW Core Patient requires each identifier item to include `value` | Identifier item exists with null `Value` | `Required/Error/Patient.identifier[0].value`; `Source=ImplementationGuide`; `PackageId=tw.gov.mohw.twcore#1.0.0`; `ProfileUrl=Patient-twcore`; `RuleId=TWCORE-PAT-004` | ImplementationGuide |  | `Tests/ImplementationGuides/TwCore/Validation/TwCorePatientValidationTests.cs` | Next | Planned | Applies only when validating against `TwCoreProfiles.Patient`. |

## Deferred Rules and Explicit Non-Goals

| ID | Area | Rule Type | Target | Rule / Cardinality | Invalid Condition | Expected Issue | Source | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| VAL-DEF-001 | Terminology | Deferred | Required terminology bindings | Validate code membership in required value sets | Code value is not in required value set | Deferred | BaseFhir |  | TBD | Future | Deferred | Terminology validation is out of MVP scope. |
| VAL-DEF-002 | FHIRPath | Deferred | FHIRPath invariants | Execute official FHIRPath invariants | Invariant expression evaluates false | Deferred | BaseFhir |  | TBD | Future | Deferred | FHIRPath is out of MVP scope. |
| VAL-DEF-003 | StructureDefinition | Deferred | StructureDefinition-driven validation | Load official definitions and validate all cardinalities/invariants | Resource violates generated rule | Deferred | BaseFhir |  | TBD | Future | Deferred | MVP uses explicit rules, not StructureDefinition parsing. |
| VAL-DEF-004 | Profile Validation | Deferred | Custom profiles | Validate profile-specific required fields and slices | Resource violates profile | Deferred | ImplementationGuide |  | TBD | Future | Deferred | Profile validation is out of MVP scope. |
| VAL-DEF-005 | Remote Validation | Deferred | `$validate` operation | Call external FHIR server validation endpoint | Server reports issue | Deferred | N/A |  | TBD | Future | Deferred | Network/server-side validation is not part of local MVP validation. |
| VAL-DEF-006 | Reference Resolution | Deferred | `Reference` targets | Verify referenced resources exist or match expected type | Reference cannot be resolved | Deferred | BaseFhir |  | TBD | Future | Deferred | Cross-resource resolution requires repository/server context. |
| VAL-DEF-007 | Business Rules | Deferred | Claim exchange workflow | Require fields that are business-required but not base R5-required | `Claim.Item` or `Claim.Insurance` is empty | Deferred | BusinessRule |  | TBD | Future | Deferred | Base R5 has `Claim.item` and `Claim.insurance` as `0..*`; make these profile/business rules later. |
| VAL-DEF-008 | Bundle Invariants | Deferred | Bundle FHIRPath invariants | Enforce rules such as `bdl-1` and `bdl-18` | Bundle invariant is false | Deferred | BaseFhir |  | TBD | Future | Deferred | Can be implemented later as explicit simple rules or full FHIRPath support. |
| VAL-DEF-009 | OperationOutcome Mapping | Deferred | Structured validation output to OperationOutcome | Convert `ValidationResult` to FHIR `OperationOutcome` | Caller requests OperationOutcome output | Deferred | N/A |  | TBD | Future | Deferred | Useful future API but not needed for local MVP result model. |

## Source References

Base resource rules should be checked against the official FHIR R5 pages before implementation:

- FHIR R5 Bundle: https://hl7.org/fhir/R5/bundle-definitions.html
- FHIR R5 Coverage: https://hl7.org/fhir/R5/coverage-definitions.html
- FHIR R5 Encounter: https://hl7.org/fhir/R5/encounter-definitions.html
- FHIR R5 Claim: https://hl7.org/fhir/R5/claim.html

IG/profile rules should be checked against the concrete IG version before implementation:

- TW Core IG v1.0.0 home: https://twcore.mohw.gov.tw/ig/twcore/
- TW Core Patient profile: https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition-Patient-twcore.html
- TW Core downloads/package page: https://twcore.mohw.gov.tw/ig/twcore/downloads.html

The implementation plan should stay aligned with:

- `docs/fhir_sdk_validation_design.md`
- `docs/fhir_sdk_mvp_prd.md`
- `docs/architecture.md`
