# Runtime kernel migration consumers

K3 follows the approved rebuild-all policy: rebuild consumers and dependent DLLs against
the split SDK and Runtime, deploy them together, and provide no kernel type forwarders.

| Source | Purpose | Build/execution gate |
| --- | --- | --- |
| `BaselineConsumer/v1` | Frozen, content-pinned K0 consumer; historical SDK only | `eng/Test-KernelMigrationBaseline.ps1` |
| `Consumer` | Current source consumer using explicit SDK/Runtime DLL references | K0 current-consumer gate and K3 isolated execution |
| `RebuiltLibrary` | Dependent DLL exposing moved kernel types and using SDK parser/serializer/SPI | Built from fresh staging as a dependency of `RebuiltConsumer` |
| `RebuiltConsumer` | Application testing the rebuilt library, both PE references, ownership and reflection identity | K3 independent process, positive and missing-Runtime deployment tests |
| `Inventory` | Explicit implementation assembly API inventory, with no SDK project reference | K0 single historical DLL; K3 combined SDK and Runtime |
| Seven SDK test projects | Architecture, CodeGen, Client, Parser, Serializer, Validation and TW Core | K3 clean solution regression; includes dynamic/generated-source consumers |

From the repository root, with PowerShell 7.2+ and the pinned .NET SDK:

```powershell
pwsh -File eng/Test-KernelConsumerRebuild.ps1 -OutputDirectory artifacts/kernel-k3
```

Use a fresh output directory for each run. The runner restores and cleans the solution,
rebuilds and runs all seven test projects, then copies migration sources into fresh staging.
Discovery scans repository source projects, pruning artifacts, bin/obj and tool metadata
directories. Unknown projects outside `Tests` also fail until they have a rebuild plan.
Every required test assembly must have exactly one report with passing executed tests;
unexpected assemblies, duplicate/missing reports, failed outcomes and counter mismatches
are rejected. Only the existing Client integration smoke can skip when its service URL is
unset, with the expected skip reason. Each project's execution counts are recorded separately.
Explicit-reference fixtures build against the SDK's deployed implementation DLLs, not the
historical compiler-only reference. The library and application rebuild together through
their project reference. Both PE files must bind moved types to `MyFhirSdk.Runtime`.

The application checks all 14 kernel declarations, absence of corresponding SDK forwarders,
assembly-qualified names and reflection resolution, accessor casts, model/base assignment,
and parser/serializer round-trip. In the independent deployment process, the SDK, Runtime,
library and application must all load from that deployment directory. Removing only its
Runtime DLL must produce a dependency failure. The runner restores that DLL afterwards;
an expected negative test does not return a failure exit code to CI.

CI supplies its canonical compiler metadata asset while rebuilding the production SDK:

```powershell
pwsh -File eng/Test-KernelConsumerRebuild.ps1 `
  -OutputDirectory artifacts/kernel-k3 `
  -RuntimeReferenceAssetPath <downloaded-compiler-reference.dll>
```

That option preserves the existing K4 asset boundary; it does not deploy the reference DLL
as Runtime. No SDK/Runtime project dependency is added to CodeGen.

Outputs include `consumer-inventory.json`, `evidence.json`, TRX reports, isolated deployment
logs, and deterministic combined `public-api.txt`. They remain ignored artifacts. The
Windows/Ubuntu CI matrix uploads them as `kernel-k3-windows` and `kernel-k3-linux`.

The lightweight failure-mode harness runs in both CI environments:

```powershell
pwsh -File eng/Test-KernelConsumerRebuildHarness.ps1
```

Its synthetic repositories and TRX cases verify discovery exclusions, unknown projects,
per-project execution, report identities, permitted integration skipping and rejection of
invalid regression evidence. It does not require NuGet or rebuild the SDK.

K0 source pin, fixture content, hashes and approved inventories remain immutable. K3
verifies the fixture pin and unchanged baseline files; the separate K0 CI job reconstructs
and executes the historical SDK/consumer and compares its inventories. Do not execute
the old binary against split assemblies or rewrite K0 evidence to represent K3 results.
