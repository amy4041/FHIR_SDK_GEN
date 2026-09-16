# Primitive package-input P0 entry baseline

- Entry source: clean `main` commit `d530faf0a8a5aba3af5e9218c976dd54bf66a9d7`.
- Implementation branch: `feat/primitive-tgz-p0`.
- Tool / CodeGen: `1.0.0`; primitive policy: `1.1.0`; primitive manifest: schema `2`.
- FHIR: `hl7.fhir.r5.core#5.0.0`, FHIR R5 `5.0.0`.
- Scope: P0 test harness and text evidence. Decision remains Proposed; production
  primitive input, policy requirements, versions and generated output are unchanged.

The clean entry commit was built in Release and its directory primitive command
was executed before adding this harness. The rebuilt output is checked against
the committed generated artifacts, not inferred from counts in the guide.

## Evidence

| File | Contract |
| --- | --- |
| `cli-help-1.0.0.txt` | Actual `GeneratorCli --help` output; UTF-8 without BOM, LF |
| `directory-artifact-inventory-1.0.0.txt` | Complete generated file set, ordinal filename order |
| `directory-artifact-hashes-1.0.0.txt` | SHA-256 of every exact generated file, including the manifest |
| `directory-manifest-1.0.0.json` | Exact generated manifest: primitive decisions, artifact hashes, policy and Runtime/compiler-reference provenance |
| `package-primitive-fixture-contract.txt` | Immutable archive SHA-256, selection rule, count and exact selected entry hashes |

The tests assert 21 input primitive specializations, 20 supported wrappers,
one registry composition, one manifest and 21 manifest artifact entries
(the manifest does not list itself). `xhtml` remains the explicit unsupported
primitive. All 22 output files are compared byte-for-byte. Wrapper and registry
hashes freeze source declarations and registry ordering; the existing
`PrimitiveWrapperBaselineTests`, `PrimitiveRuntimeContractTests` and approved
public API tests remain the runtime/API gates.

## Package and directory equivalence

The existing `CodeGen/Policy/r5-package-lock.json` remains the authoritative
archive lock. The fixture contract here also pins its hash so changing the lock
cannot silently change this historical baseline. No new archive or DLL is added.

`PrimitiveTgzInputBaselineTests` verifies the archive hash, then reuses
`FileDefinitionPackageInput` and `DefinitionPackageLoader` to validate package
identity. Its test-only oracle selects primitive specializations, checks unique
type/canonical identity and compares their entire JSON documents with the flat
directory fixture set, including fields outside the CodeGen DTOs.

For the equivalence experiment only, it reads selected raw archive entries into
memory and writes their bytes to a private temporary fixture directory using
filenames already matched against the approved flat fixture set. It then runs
the existing directory generation pipeline on both inputs, compares every output
byte, checks a repeat run and compares the result with committed output. The
temporary package-derived files are created in reverse ordinal order.

This establishes equivalence of the input fixtures and the existing directory
pipeline. It does **not** establish production primitive `.tgz` support, a P2
selector, malformed-archive handling, or tar-order-independent diagnostics.
Those remain P1–P5 work. The archive's exact hash belongs in this test contract,
not in manifest schema v2.

## Reproduce

From the repository root, with the approved offline FHIR archive present:

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln -c Release --no-restore
dotnet test Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~PrimitiveTgzInputBaselineTests
pwsh ./eng/Export-PrimitiveTgzInputBaseline.ps1
```

The exporter runs the same test helper and writes only text/JSON evidence to
`artifacts/primitive-tgz-p0/rebuilt/`. Normal tests only compare against approved
snapshots. Export also verifies snapshots and fails on drift; it never accepts
differences automatically. Review any difference before explicitly copying a
newly approved snapshot. Baselines contain no machine paths or timestamps.
Generated file hashes use actual writer bytes without newline normalization;
only CLI help is normalized to LF. Git pins the evidence to LF.

On Linux, use the canonical Windows-built Runtime compiler reference through
`-p:RuntimeReferenceAssetPath=<canonical-reference>` as in the existing CI.
Do not regenerate reference hashes to accommodate a different local DLL.

## Exit gates

Run the implementation guide's restore/build/full-test/pack/toolchain commands
and `git diff --check`. Preserve Phase D's installed local-tool smoke, package
inventory, baseline-version validation and Windows/Linux drift gates in
`.github/workflows/ci.yml`. P0 uses tool `1.0.0`'s install/reinstall baseline;
the actual `1.0.0` to `1.1.0` upgrade belongs to P6.

P1 onward requires the decision's acceptance gates. P3–P4 must introduce the
new CLI capability and its `1.1.0` compatibility identities together; keep this
`1.0.0` evidence as the historical entry baseline when adding successor evidence.

## Local verification

Verified on Windows with .NET SDK `9.0.317`:

- Clean-entry Release build and all six existing primitive pipeline tests passed.
- New Release solution build: zero warnings and errors.
- Full solution tests: 657 passed, one existing external-server client integration
  smoke skipped; all 362 CodeGen tests passed, including the new P0 test.
- Baseline exporter reproduced all five evidence files and passed verification.
- Canonical `eng/MyFhirSdk.CodeGen.Build.proj /t:Pack` passed.
- Toolchain contract, package inventory and `1.0.0` baseline-version checks passed.
- Repository-independent local-tool install/uninstall/reinstall smoke passed:
  831 model sources, 832 model artifacts and 22 primitive artifacts.
- `git diff --check` passed; production, CLI and committed generated files have
  no changes.

PowerShell `7.5.3` was provisioned as a local tool under ignored
`artifacts/primitive-tgz-p0/tools/` because this workstation had no `pwsh` on PATH.
Local commands used that `pwsh.exe`; this is not a product dependency or a tracked
tool-manifest change. Smoke logs and package reports are under ignored
`artifacts/primitive-tgz-p0/`. Windows/Linux CI and cross-platform drift have not
been executed from this working session and remain required before merge.
