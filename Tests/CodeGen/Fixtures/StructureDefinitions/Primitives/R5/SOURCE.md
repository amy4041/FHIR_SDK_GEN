# FHIR R5 primitive StructureDefinition fixtures

This directory contains a pinned test snapshot of the official FHIR R5
primitive `StructureDefinition` resources used by the Phase B primitive
inventory and policy-coverage tests.

## Source

- Specification: FHIR R5
- FHIR version: `5.0.0`
- Package ID: `hl7.fhir.r5.core`
- Package version: `5.0.0`
- Official specification: <https://hl7.org/fhir/R5/>
- Official downloads: <https://hl7.org/fhir/R5/downloads.html>

The FHIR R5 downloads page identifies `hl7.fhir.r5.core` as the package that
contains the resources needed for conformance testing and code generation.

## Selection

The fixture set contains every package resource satisfying both conditions:

```text
resourceType == StructureDefinition
kind == primitive-type
```

The snapshot contains 21 JSON files. It includes the primitives currently
supported by MyFhirSdk Runtime and the official primitives that Phase B must
classify explicitly as supported or unsupported.

## Repository policy

- Treat these files as immutable third-party test inputs.
- Do not edit them to accommodate the current loader or generator.
- Tests must not download or replace them at runtime.
- When updating the FHIR/package version, replace the complete selected set,
  update this file, regenerate `SHA256SUMS.txt`, and review the inventory diff.
- Verify licensing and attribution requirements against the official FHIR
  distribution when redistributing these fixtures.

`SHA256SUMS.txt` records the exact bytes of the repository snapshot.
