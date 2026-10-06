# Public primitive accessor contract evidence

This evidence accompanies the user-authorized public accessor and CodeGen reference
migration on 2026-10-06. The assembly remains `MyFhirSdk, Version=1.0.0.0`, with a
null public key token and target framework `net9.0`. No Runtime assembly extraction
or public package release is included.

| Input | Current value |
| --- | --- |
| Runtime contract version | `runtime-kernel-accessor-v1` |
| Compiler reference SHA-256 | `9bedf2420e4290afdc0df144d04b77cb8e243bb2380d2ea181a952d5689afa01` |
| Descriptor SHA-256 | `643dc74c30ab0265c9f3c9c9ec30518c40050a2fbeae5d0c478a63d1c146392e` |
| Primitive policy SHA-256 | `47fb82123bc97fff108230876d33447208f8e6c2e6b0643bdf3a0a0dce05b253` |

Tool/CodeGen and primitive policy versions remain `1.1.0`; this is an unreleased
repository migration, distinguished by the exact Runtime contract version and
hashes. The primitive policy's runtime contract selection changes, not its primitive
decisions. Descriptor schema remains 1, manifests remain schema 2, and the generator
mapping still has 13 symbols. Public accessor membership does not add a mapping role.

Both committed generation manifests were regenerated with the current tool. Their
changes are limited to contract version, descriptor/reference hashes and, for the
model manifest, the primitive policy hash. All 831 model sources and 21 primitive
sources remain byte-identical. The primitive baseline test compares all historical
source hashes and full manifests with only those explicitly approved provenance
differences, and compares directory, materialized-package, archive and repeated runs.

Only the new primitive manifest and artifact-hash evidence are stored here. CLI
help, artifact inventory and fixture contract continue to use the unchanged
`primitive-tgz-input` evidence. Its 1.0.0/1.1.0 files and all `kernel-k0` evidence
remain historical and are not overwritten.

Reproduce with the pinned SDK and PowerShell 7:

```powershell
dotnet build eng/MyFhirSdk.CodeGen.Build.proj -c Release
dotnet test MyFhirSdk.sln -c Release
pwsh -NoProfile -File eng/Export-PrimitiveTgzInputBaseline.ps1 -OutputDirectory artifacts/accessor-rebuilt
```

K0 reconstruction still uses its original pin, consumer and tool. Its current-source
regression uses the current SDK reference rather than injecting the historical
reference into the new descriptor. The complete K0 gate therefore checks both
historical reconstruction and current regression without mixing their contracts.

## Local validation on 2026-10-06

Windows validation passed with the pinned .NET SDK 9.0.317:

- Solution regression: 823 passed, 1 external FHIR service test skipped, 0 failed.
- Full K0 reconstruction, old/current consumers, inventory comparison, current
  primitive equivalence and historical tool smoke passed. Evidence is in
  `artifacts/accessor-k0-verified/evidence.json`.
- Current-package clean install/uninstall/reinstall and generation smoke passed:
  831 model sources, 22 primitive artifacts, and byte-identical tgz/directory output.
  Evidence is in `artifacts/accessor-current-smoke-verified/smoke-summary.json`.
- All 852 generated C# files were compared byte-for-byte against committed sources.

These are local results, not a claim that branch CI or Ubuntu validation has run.
