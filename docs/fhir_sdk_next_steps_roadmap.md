# MyFhirSdk Next Steps Roadmap

Last updated: 2026-06-10

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

Status: Planned

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

Status: Planned

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

Status: Planned

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

- Validation inventory clearly labels rule source.
- Base validation and IG validation are not mixed together.

---

### Phase 3 - Complete Base Validation Rules

Status: Planned

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

Status: Planned

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

Status: Planned

Goal:

Add optional IG-aware validation without changing base resource classes.

Possible design:

- Base validator always runs base FHIR rules.
- Profile validator adds IG-specific rules.
- Profile constants define canonical URLs.
- `meta.profile` can be used to indicate profile conformance.

Done when:

- At least one IG/profile validation path exists.
- Base validation still works independently.

---

### Phase 6 - Complete Client Inventory

Status: Planned

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

Status: Planned

Goal:

Verify the SDK against a real or test FHIR server flow.

Candidate flow:

1. Create Patient
2. Read Patient
3. Search Patient
4. Update Patient
5. Parse Bundle result

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