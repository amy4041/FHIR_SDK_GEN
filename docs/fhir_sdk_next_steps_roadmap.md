# MyFhirSdk Next Steps Roadmap

Last updated: 2026-06-23

## Purpose

This document tracks the next engineering steps for MyFhirSdk after the initial FHIR R5 MVP foundation.

The goal is to move the SDK from a working base implementation toward a testable, maintainable, and eventually IG-aware SDK.

## Current Status

The SDK currently supports:

- FHIR R5 base resource models.
- JSON serialization and parsing.
- REST client read/create/update/search.
- Basic validation.
- Console-based test runners.

Existing inventories:

- `docs/fhir_sdk_validation_rule_inventory.md`
- `docs/fhir_sdk_client_test_inventory.md`

## Guiding Decisions

- Keep base FHIR R5 rules separate from IG/profile rules.
- Do not put IG-specific requirements directly into base resource classes.
- Add `.sln` before broader engineering cleanup.
- Move to xUnit before adding large amounts of new tests.
- Use JSON end-to-end fixtures to verify real resource behavior.

## Roadmap

### Phase 0 - Add Solution File

Status: Finished

Goal:

Create a standard .NET solution file so the SDK and test projects can be managed together.

Tasks:

- Add `MyFhirSdk.sln`.
- Include the SDK project.
- Include current test projects.

Done when:

- `dotnet build MyFhirSdk.sln` succeeds.

---

### Phase 1 - Add Base FHIR JSON Fixtures

Status: Finished

Goal:

Add JSON end-to-end fixture coverage for MVP resources beyond Patient.

Priority order:

1. Bundle search result
2. Claim
3. Coverage
4. Encounter
5. Organization
6. Practitioner

Tasks:

- Add serializer fixture JSON files.
- Add parser assertions.
- Ensure serializer and parser runners pass.

Done when:

- Each MVP resource has at least one meaningful serialize + parse fixture.
- Existing JSON fixture tests still pass.

---

### Phase 2 - Define IG Boundary

Status: Finished


Goal:

Decide how IG/profile support will fit into the SDK before adding many more validation rules.

Decisions to make:

- Which IG or local profile should be supported first?
- Which rules are FHIR R5 base rules?
- Which rules are IG/profile rules?
- Which rules are local business rules?

Possible rule sources:

- FHIR R5 Base
- IG/Profile
- Local Business Rule

Done when:

- IG layer boundary is documented in `docs/fhir_sdk_ig_layer_design.md`.
- Validation inventory clearly labels rule source.
- Initial TW Core Patient profile rules are listed in `docs/fhir_sdk_validation_rule_inventory.md`.
- Initial TW Core Patient POC implementation order is documented in Phase 5.
- Base validation and IG validation are not mixed together.

---

### Phase 3 - Complete Base Validation Rules

Status: Finished

Goal:

Implement remaining planned validation rules that are truly FHIR R5 base rules.

Focus areas:

- Claim nested required fields.
- Claim choice elements that are base FHIR rules.
- Coverage base choice/required rules.
- Bundle base nested rules if relevant to MVP.

Done when:

- Base validation Planned rules are either Covered or explicitly Deferred.
- Tests pass.

---

### Phase 4 - Migrate Tests to xUnit

Status: Finished

Goal:

Replace console test runners with standard xUnit tests.

Tasks:

- Add xUnit test packages.
- Convert serializer/parser/client/validation tests.
- Use `dotnet test` as the standard command.
- Keep JSON fixtures as test data.

Done when:

- `dotnet test MyFhirSdk.sln` runs all tests.
- Console test runner `Program.cs` files are no longer needed.

---

### Phase 5 - Add IG/Profile Validation

Status: In Progress

Goal:

Add optional IG-aware validation without changing base resource classes.

Possible design:

- Base validator always runs base FHIR rules.
- Profile validator adds IG-specific rules.
- Profile constants define canonical URLs.
- `meta.profile` can be used to indicate profile conformance.

Initial TW Core Patient POC implementation order:

1. Add validation issue metadata: (Finished)
   - `ValidationRuleSource`
   - `ValidationIssue.Source`
   - `ValidationIssue.PackageId`
   - `ValidationIssue.ProfileUrl`
   - `ValidationIssue.RuleId`
2. Add the generic profile framework: (Finished)
   - `Validation/Profiles/IImplementationGuidePackage.cs`
   - `Validation/Profiles/IProfileValidationRule.cs`
   - `Validation/Profiles/ProfileValidationContext.cs`
   - `Validation/Profiles/ProfileValidationOptions.cs`
   - `Validation/Profiles/ProfileValidator.cs`
3. Add `ImplementationGuides/TwCore/TwCoreProfiles.cs`. (Finished)
4. Add `ImplementationGuides/TwCore/TwCorePackage.cs`. (Finished)
5. Add `ImplementationGuides/TwCore/Validation/TwCorePatientRules.cs` with only the initial identifier rules: (Finished)
   - `TWCORE-PAT-002`: `Patient.identifier` must contain at least one item.
   - `TWCORE-PAT-003`: each `Patient.identifier[*].system` must be present.
   - `TWCORE-PAT-004`: each `Patient.identifier[*].value` must be present.
6. Add TW Core Patient tests: (Finished)
   - `Tests/ImplementationGuides/TwCore/TwCorePackageTests.cs`
   - `Tests/ImplementationGuides/TwCore/Validation/TwCorePatientValidationTests.cs`
7. Reassess the next TW Core Patient scope:
   - name slices
   - identifier slices
   - `meta.profile` declared-profile validation

Done when:

- At least one IG/profile validation path exists.
- Base validation still works independently.

---

### Phase 6 - Complete Client Inventory

Status: Finished

Goal:

Cover remaining MVP/Next client scenarios from the client inventory.

Focus areas:

- Raw search overload.
- Default no-auth client behavior.
- Repeated search parameters.
- Direct Bundle response handler test.
- Token/date/string search value preservation.

Done when:

- Client inventory MVP/Next Planned items are Covered or intentionally Deferred.

---

### Phase 7 - Integration Smoke Test

Status: Finished

Goal:

Verify the SDK against a real or test FHIR server flow.

Implemented flow:

1. Create Patient
2. Read Patient
3. Search Patient
4. Update Patient
5. Parse Bundle result

Implementation:

- `Tests/Client/Integration/FhirClientIntegrationSmokeTests.cs`
- `Tests/Client/Integration/IntegrationFactAttribute.cs`

Run behavior:

- Skipped by default when `MYFHIRSDK_INTEGRATION_BASE_URL` is not set.
- Can be run manually against a configured FHIR R5 test server.
- Supports optional bearer authentication with `MYFHIRSDK_INTEGRATION_BEARER_TOKEN`.
- Uses a UUID-based Patient identifier marker to avoid mixing smoke test data with existing server data.

Example manual run:

```powershell
$env:MYFHIRSDK_INTEGRATION_BASE_URL="https://hapi.fhir.org/baseR5"
dotnet test MyFhirSdk.sln --filter Category=Integration
```

Done when:

- A repeatable integration smoke test exists.
- It can be skipped or configured when no server is available.

---

### Phase 8 - SDK Usability and Release Readiness

Status: Planned

Goal:

Make the SDK easier for other developers to use.

Tasks:

- Improve README.
- Add usage examples.
- Add CI.
- Add package metadata.
- Prepare NuGet packaging.
- Consider samples folder.

Done when:

- New developer can understand basic usage from README.
- `dotnet build`, `dotnet test`, and `dotnet pack` are documented.
