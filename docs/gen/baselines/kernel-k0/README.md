# Runtime kernel K0 migration baseline

K0 fixes the post-D Tool/CodeGen 1.1.0 baseline before any assembly extraction.
The SDK source revision is `1a28f01d8a4c3aeea46c63da875d01594aeee086`;
the machine-readable pin is `eng/kernel-migration-baseline.json`. The ADR was accepted
on 2026-10-07; authorization for K1 comes from that separate owner decision, not from
these fixtures. Historical baseline bytes and evidence remain unchanged.

## K0 delivery status (2026-10-06)

K0 was merged into `main` through PR #40 from
`chore/runtime-kernel-k0-baseline`. The local Git history confirms merge commit
`ead6fd7d5f78e876f6d1c6fa9aca359a97bd50a5`.
The user confirmed branch CI passed before the merge and confirmed the merged
`main` CI passed on 2026-10-06. This records user-confirmed CI evidence; no Actions
run URL or downloaded CI artifact was supplied for independent verification.
K0 is recorded as delivered based on the merge and these CI confirmations.

The development review and its P1/P2 corrections are documented below. Subsequent
CI fixes cover build language, expected-failure exit codes, and archive line endings.
This records the review work performed in this development session; it does not
assert a GitHub reviewer approval or Architecture/Runtime ADR acceptance.

`local-validation.json` remains the historical local-run evidence. Its
`branchCiValidated: false` describes that run's evidence scope, not the current
delivery status. The pinned pre-extraction SDK revision remains
`1a28f01d8a4c3aeea46c63da875d01594aeee086`; the K0 merge commit does not replace it.
The ADR was subsequently accepted on 2026-10-07. See the
[acceptance record](../../MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#9-acceptance-gate-核准紀錄)
for the owner authorization to begin K1; implementation gates remain outstanding.

## Reproduce

After the public accessor contract migration, current-source regression builds the
current SDK compiler reference. The historical K0 reference remains reserved for
the frozen consumer and baseline tool. This separates current contract validation
from reconstruction of the immutable K0 baseline.

From the repository root with the pinned .NET SDK 9.0.317, PowerShell 7.2 or newer,
Git, tar, and NuGet access:

```powershell
pwsh -NoProfile -File eng/Test-KernelMigrationHarnessExitCode.ps1
pwsh -NoProfile -File eng/Test-KernelMigrationBaseline.ps1 -RunRegressionAndSmoke
```

Use `-OutputDirectory artifacts/kernel-k0-second` for a second independent run.
The output directory must be fresh and below `artifacts/`. The script exports
the pinned Git tree into isolated staging, builds Release SDK/reference assemblies,
packs the tool, checks the descriptor/reference hashes and records the package
hash from that pack. Git discovery and SourceLink are disabled for the exported
baseline build; repository commit and informational-version source revision are
explicitly pinned, with no branch. This prevents the enclosing checkout's HEAD,
branch and machine paths from entering the package or PDB. The packed nuspec is
checked before accepting its inventory. NuGet zip hashes are run evidence, not
assumed stable across packs.

Source export uses invocation-local `core.autocrlf=false` and `core.eol=lf`.
`git archive` applies working-tree conversions, so the runner's Git configuration
can otherwise change source bytes and hence Portable PDB checksums and DLL hashes.
Committed attributes and binary bytes are preserved; caller Git settings are not
modified. This retains the existing pinned implementation hash.

K0 dotnet build commands explicitly use `DOTNET_CLI_UI_LANGUAGE=en-US` through
`Invoke-KernelDotNet`, restoring the caller's environment even on failure. This is
part of the canonical build contract: MSBuild's generated AssemblyInfo comment is
localized, and its Portable PDB document checksum affects the implementation DLL's
deterministic hash. A fixed SDK version and source revision alone are insufficient.

Without `-RunRegressionAndSmoke`, evidence reports `status: partial` and marks the
three omitted gates `skipped`. A full successful run reports `passed` only after
the final committed-inventory comparison. Any exception reports `failed`, the
failing gate and its message; later gates remain pending. CI's always-uploaded
evidence must be read using this overall status, not individual success fields.
SDK identity and implementation-hash failures are reported separately, with expected
and actual values plus the canonical build language persisted in
`sdkBaselineComparison` before the gate throws.

## Evidence and inventory

`public-api.txt` inventories every exported SDK type, including defining assemblies,
base types, interfaces, nested types, generic arguments/constraints, constructors,
methods, fields, properties, events and signature type identities, including protected
members of exported types. It also records nullable read/write states recursively
through arrays/generic arguments, parameter default values, and constant/enum values.
Values are culture-independent and strings are escaped to preserve line structure.
`IDENTITY` rows map compact assembly aliases to complete
assembly identities; ambiguous aliases fail rather than losing version information. Output uses ordinal
sorting and LF with no machine paths or timestamps. Existing approved API snapshots
are copied as evidence, never replaced by this inventory.

`source-inventory.txt` records hashes for the 831 model sources, 21 primitive/registry
sources, both manifests and both approved API snapshots. The pinned Git revision
preserves the full source content. The script also saves the manifests and snapshots.
Counts are checked against actual data: 13 descriptor symbols and 842 R5 surface types.
The latter uses the existing architecture snapshot definition, not all SDK exports.

`evidence.json` distinguishes the actual implementation DLL hash from the compiler-only
reference hash. It records assembly full identity, SDK/TFM, policy/FHIR/package hashes,
consumer DLL hash and fixture provenance. `fixtureRevision` is a fixed content
revision (SHA-256), not the caller's HEAD. Prior CI success is recorded as user-confirmed,
without inventing a run URL. New CI artifacts provide the K0 execution evidence.

## Consumer lifecycle

The old fixture is frozen in `Tests/KernelMigration/BaselineConsumer/v1`, introduced
by K0 rather than assumed to exist in the historical SDK commit. The baseline JSON
pins both file hashes and their content revision. That revision is SHA-256 over
UTF-8 lines `sha256  filename\n`, in ordinal filename order (Consumer.csproj, then
Program.cs). The files have enforced LF line endings. Both content hashes and the
revision are verified before staging; editing the frozen fixture fails the gate.
This avoids inventing a Git commit that cannot yet contain the uncommitted K0 files.
After commit, the frozen source remains rebuildable directly from that checkout.

The old fixture compiles against the isolated old SDK DLL and exercises primitive
value read/write, model-to-base assignment and public parser/serializer round-trip.
Its build fixes Git metadata and maps source paths for repeatability. The separate
`Tests/KernelMigration/Consumer` is the current-source fixture and may evolve without
redefining the old consumer. Each has separate staging/output/obj directories. The
old consumer DLL hash is checked after both runs. Any intentional replacement of
the frozen fixture requires a reviewed new fixture version and baseline pin.

K0 runs the old binary against the original SDK. It does not claim split compatibility.
K3 must reuse the old consumer output, replace only SDK dependencies with the split
assemblies and run without rebuilding the consumer. DLLs belong only in ignored local
artifacts and CI artifacts, never Git.

## Ownership review input

| Surface or seam | K0 owner and extraction disposition |
| --- | --- |
| 13 descriptor symbols | Currently MyFhirSdk; proposed Runtime ownership requires ADR acceptance. |
| Extension, Meta, Narrative | Proposed Runtime bootstrap types to avoid a reverse Models dependency. |
| PrimitiveType and internal IPrimitiveValueAccessor | Currently SDK; K1 must review a minimal integration contract before moving the base. |
| PrimitiveRegistry and generated partial composition | SDK owner; no codec or validator visibility expansion in K0. |
| R5 metadata, factories and default parser/serializer/validator composition | SDK owner; stay together in this extraction. |
| Generated R5 wrappers, types/resources and SimpleQuantity | SDK owner; no declaration moves. |
| Client and TW Core IG | SDK owner; no changes. |
| FhirSdkException | Keep in SDK: parser consumers throw it, while the proposed kernel sources do not require it. Not an initial descriptor symbol. |
| Other unlisted public/internal SDK types | Keep in SDK by default; inventory supports later explicit review. |

## Acceptance boundary

The CI job rebuilds the pin, verifies deterministic inventory, runs both consumers,
existing API/runtime regression, primitive input equivalence and installed-tool smoke.
Existing Windows/Ubuntu tool jobs continue to verify cross-platform behavior.
K0 evidence does not mark the ADR Accepted or validate type forwarding. No production
project, public declaration, descriptor version or generated source is changed.

## Local verification

`local-validation.json` records the latest completed full run's identities, input
hashes, canonical package hash and test counts. A live-server Client test requires
`MYFHIRSDK_INTEGRATION_BASE_URL`; skipped tests are counted explicitly. This is local
evidence, not a claim that the new branch CI has run.

Twelve inventory regression cases verify detectable changes in nullable contracts,
defaults, constants/enums, assembly identity, nested types and constraints, including
culture-independent escaping. Harness tests create two independent Git repositories
with different HEADs/branches, source paths and caller UI languages (`zh-TW`, `en-US`).
They reproduce the uncorrected locale-sensitive DLL hashes, then prove canonical
DLL bytes and package metadata are identical while reference bytes are unchanged.
They also verify environment restoration after success/failure and the expected/
actual hash details in failed evidence. Uncorrected metadata is rejected.
Archive regression cases exercise both autocrlf settings with `core.eol=crlf`,
verify canonical LF exports for ordinary and attributed text, preserve binary
bytes, and check that caller Git settings remain unchanged.
Additional cases cover frozen fixture
tampering, live-fixture independence, final-comparison failures, early failures and
the distinction between partial and complete verification.

The CI entry point `Test-KernelMigrationHarnessExitCode.ps1` runs the actual harness
with the Actions PowerShell prefix/suffix, including `exit $LASTEXITCODE`. The
`missing.csproj` error is intentional: it verifies environment restoration after a
failed native command. The harness explicitly exits zero only after all assertions
and summary writes succeed, so that expected failure cannot leak into the CI step's
status. The entry point also verifies that an unhandled harness precondition failure
still returns nonzero. Its results are saved as `shell-exit-summary.json` alongside
the harness summary; it does not alter the SDK baseline or claim a new full K0 run.

## Review corrections to the baseline

P1 corrected the package nuspec's repository commit from the enclosing checkout to
the pinned SDK revision. Only the nuspec hash changes in the normalized package
inventory (CodeGen DLL/PDB are already represented as platform build outputs).
The old SDK SourceLink also included the enclosing machine path and HEAD. Disabling
this accidental SourceLink changes the deterministic implementation DLL hash from
`6d4c068a9bd92be275b5de3c450fb3b30d69cecdbd8bc4866930a87a59e50e9e`
to `2bf89074d5cbaaa22261c80ef62d63821ccf97e9f304b162a7b7684fc8d4ed7f`.
The source revision, assembly identity and compiler reference hash stay unchanged.

That intermediate hash was generated under `zh-TW`. The first branch CI exposed
another missing build input: its English MSBuild emits a different generated comment.
Rebuilding the same pinned SDK locally under both languages reproduced the mismatch.
The canonical build now fixes `en-US`, with implementation SHA-256
`d47a702c5b52277d81f96867ad026b447fc46cfc9e7fcf08d16fd47de1615afc`.
This correction changes the implementation/PDB and old-consumer binary hashes,
without changing assembly identity, compiler reference, API or generated-source
inventories. The strict implementation hash check remains enabled.

P2 expands `public-api.txt` with previously omitted contract details. Existing
ApprovedPublicApi/ApprovedR5ModelApi snapshots and the source inventory are unchanged.
The frozen fixture's source is identical to the original K0 consumer; only its
provenance/build isolation is now fixed. Evidence includes explicit per-gate and
overall states, including failures after successful regression tests.

Branch CI and post-merge main CI are now user-confirmed as passed; see the delivery
status above for the evidence source and merge revision. No K1 work is included.
