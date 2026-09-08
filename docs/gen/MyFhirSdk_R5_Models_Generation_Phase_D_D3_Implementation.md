# MyFhirSdk CodeGen Phase D D3 實作紀錄

Version 1.0

- 狀態：Completed
- 前置基線：D0-D2 completed、Runtime contract schema 1
- Runtime contract：`phase-a-v1+c4-primitives-v1`
- Target framework：`net9.0`
- Runtime reference SHA-256：`7c945def6e2414e7944367d0df0ac33aa6386e4cdbedce0d05922ae5023176c9`
- Descriptor SHA-256：`b8362333a3a26514eead62b2ca5e2abd130bc37b36c8290748f01497593aa333`

## 1. 完成範圍

D3 建立 `RuntimeReferenceService`，將 Roslyn 的 host framework references 與 Runtime contract
reference 分離解析，並在建立 `RoslynCompilationValidator` 前完成 deterministic preflight。

package-owned compiler-only baseline 固定為：

```text
Assets/RuntimeReferences/net9.0/MyFhirSdk.dll
```

Git 不保存此 DLL。`MyFhirSdk.csproj` 的 `GetCompilerReferenceAsset` target 先建置 SDK，再明確
回傳 SDK reference assembly；`eng/MyFhirSdk.CodeGen.Build.proj` 將該 `TargetOutputs` 以
`RuntimeReferenceAssetPath` 傳入 CodeGen project，複製至相同 output layout。CodeGen 未加入
SDK ProjectReference，也不自行搜尋 repository、`bin`、`obj` 或任意已載入 assembly。
`.gitignore` 另明確拒絕此路徑下的 DLL。

compiler-only asset 使用 SDK 產生的 reference assembly，不包含 implementation method body 與
platform-specific debug payload。SDK build 另關閉 commit-dependent informational version，並以
固定 `PathMap` 排除 checkout root。普通 CodeGen build 不要求 asset；packaging 則必須明確提供
`RuntimeReferenceAssetPath`。

Repository root 的 `global.json` 固定 compiler asset build 使用的 .NET SDK；TFM 升級時由 D7
明確更新 SDK pin、project target framework、Runtime descriptor 與 reference hash。這避免同一個
TFM 因機器預設 SDK 不同而產生不同 compiler asset bytes。

## 2. RuntimeReferenceSet

Immutable `RuntimeReferenceSet` 現在包含：

- target framework；
- 依 name、version、public key token、TFM 組成的 logical identity 做 ordinal 排序；
- 分離的 trusted platform references 與 Runtime contract references；
- Runtime logical assembly identity（name、version、public key token、TFM）；
- descriptor contract SHA-256；
- Runtime reference exact-byte SHA-256。

TPA 來自 host 的 `TRUSTED_PLATFORM_ASSEMBLIES`。與 explicit Runtime reference 同 simple name 的
TPA entry 會被排除，由經過驗證的 package asset 明確勝出；它不會成為 fallback。

## 3. Preflight validation

Resolver 將 Runtime asset 只讀取一次，以同一份 immutable bytes 解析 PE metadata 與計算 hash，
再由 Roslyn `MetadataReference.CreateFromImage` 編譯該份已驗證 image。asset 不會載入 default
load context，也不會列舉 types。
Roslyn emit 前會驗證：

- asset 存在且可讀；
- CLR assembly name、version 與 public key token；
- `TargetFrameworkAttribute`；
- exact-byte SHA-256；
- Runtime/TPA category 內沒有重複 assembly identity；
- 所有 reference input 最終依 logical assembly identity 排序。

Diagnostics 僅顯示 logical asset/assembly identity，不包含 installation、temporary 或 repository
absolute path。

| Code | Meaning |
| --- | --- |
| `FSG0110` | Runtime/TPA reference missing |
| `FSG0111` | reference unreadable or corrupt |
| `FSG0112` | assembly identity mismatch |
| `FSG0113` | Runtime reference SHA-256 mismatch |
| `FSG0114` | duplicate assembly identity |
| `FSG0115` | target framework mismatch |

## 4. Production composition

`Program` 先處理 help 與無效 CLI syntax；只有有效的 generation command 才載入 D1 Runtime
contract，並以 executable/tool root 解析固定 package asset layout。
只有 contract 與 reference set 均成功時，才建立 `RoslynCompilationValidator` 及 generation
pipelines。所有 Runtime reference diagnostics 都映射為 input/preflight exit code `2`。

兩個 Roslyn validators 都接受同一個 explicit `RuntimeReferenceSet`，不再各自讀取 TPA。Test
project 以 `GetCompilerReferenceAsset` 明確取得 Release SDK reference assembly，建立 package-like
staging layout，再使用相同 resolver；production CodeGen 不會取得 test SDK ProjectReference。

## 5. 驗收結果

- valid package-owned reference 可完成 Roslyn validation 與 full model batch；
- missing、corrupt、wrong identity、wrong hash、wrong TFM、duplicate identity 均有負向測試；
- shuffled TPA input 產生相同 ordered reference set 與 compilation result；
- Runtime asset 驗證後即使實體檔案被替換，Roslyn 仍只使用原先驗證的 immutable image；
- diagnostics 不含 machine-specific absolute paths；
- committed model output 與 manifest hash 不變；
- CodeGen standalone build、solution Release build、完整 tests 與 `git diff --check` 通過。

Windows/Linux 實機使用相同 package asset 的 smoke matrix 由 D7 執行；D3 已固定 logical ordering、
path-independent diagnostics 與可重現 Runtime compiler asset inputs。

## 6. D4 交接

D4 應保留本服務的驗證與排序語意，並以 `ToolAssetResolver` 集中組合 package defaults 與
`--runtime-contract`、`--runtime-reference` overrides。override precedence 不得引入
repository/current-directory/environment fallback；output safety 與 repository safety context 依 D0
決策分離。
