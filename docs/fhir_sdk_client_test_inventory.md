# FHIR SDK Client Test Inventory

This document tracks planned and covered tests for the FHIR SDK Client layer.

## Columns

| Column | Description |
|---|---|
| ID | Stable test case identifier, for example `CLIENT-REST-001`. |
| Area | Client layer area under test, such as `REST Operations`, `Request Building`, `Response Handling`, `Search Query`, or `Authentication`. |
| Operation | FHIR REST operation involved, such as `Read`, `Create`, `Update`, `Search`, or future operations like `Delete`. |
| Scenario | Short description of the behavior being tested. |
| FHIR Rule | Relevant FHIR REST or search rule, such as `GET [base]/[type]/[id]`. |
| SDK API | Public SDK API or internal collaborator being exercised, such as `ReadAsync<Patient>("123")` or `BuildReadRequest<Patient>("123")`. |
| Input | Important test inputs, including resource type, id, query string, request body, headers, or fake response setup. |
| Expected Request | Expected HTTP method, URL, headers, and request body when the test involves outgoing requests. |
| Fake Response | Fake HTTP response used by the test, if applicable. |
| Expected Result | Expected SDK result, parsed resource, `null`, or exception. |
| Covered By | Test method name that covers the scenario. |
| Test File | Test file where the scenario is or should be implemented. |
| Priority | Test priority, such as `MVP`, `Next`, or `Future`. |
| Status | Current state, such as `Planned`, `Covered`, `Missing`, or `Blocked`. |
| Notes | Additional context, assumptions, or future follow-up details. |

## REST Operations

| ID | Area | Operation | Scenario | FHIR Rule | SDK API | Input | Expected Request | Fake Response | Expected Result | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |

## Request Building

| ID | Area | Operation | Scenario | FHIR Rule | SDK API | Input | Expected Request | Fake Response | Expected Result | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |

## Response Handling

| ID | Area | Operation | Scenario | FHIR Rule | SDK API | Input | Expected Request | Fake Response | Expected Result | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |

## Search Query

| ID | Area | Operation | Scenario | FHIR Rule | SDK API | Input | Expected Request | Fake Response | Expected Result | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |

## Authentication

| ID | Area | Operation | Scenario | FHIR Rule | SDK API | Input | Expected Request | Fake Response | Expected Result | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |

## Future Coverage

| ID | Area | Operation | Scenario | FHIR Rule | SDK API | Input | Expected Request | Fake Response | Expected Result | Covered By | Test File | Priority | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |
