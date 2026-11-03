# V3 — R5 Models 分離與重編遷移

日期：2026-10-16。V2 commit `fa8dbb38c4809b028151167887d5d72968d100a8` 已由使用者確認 CI 通過。
V3 繼續在 `feature/fhir-r4-v0` 開發，尚未提交／push，新增 CI 待執行；main 保持穩定。

## 實體 ownership

| Assembly | 責任與相依 |
| --- | --- |
| MyFhirSdk.Runtime | 原有 14 個 kernel types，沒有 SDK／Models 相依 |
| MyFhirSdk.ModelContracts | 原有公開中立 SPI，只引用 Runtime |
| MyFhirSdk.ModelSupport | 共用 primitive codecs／validators、required／choice rule implementations，只引用 Contracts |
| MyFhirSdk.R5.Models | 850 個 R5 FHIR declarations（含 SimpleQuantity）、R5 provider 與 generated composition，只引用 Contracts／ModelSupport |
| MyFhirSdk | context、parser／serializer／validator、lookup adapters、R5 default composition、Client／IG |

R5 Models 沒有 SDK／engines reference、type forwarders 或 production friend access。
`Generated/R5` 保留原位置，由 Models project 單獨編譯；root SDK 排除 Models／ModelSupport／generated sources。
生成的 registry partial 與 assembly-local composition bindings 同在 Models，不跨 assembly 延伸 partial。
Bindings 只把既有生成格式轉成 V2 的中立 provider definitions；不包含 SDK lookup／engines。

SDK 保留原有 namespace、constructor、R5 JSON／validation 行為與 `FhirSdkException` defining assembly。
ModelSupport 的 primitive conversion failure 由 SDK adapter 轉回既有 SDK 例外與訊息。
公開 `R5ModelProvider` 可在不載入 SDK 的程式中提供 factory、codec、validator 與規則。

## 生成、API 與 CodeGen 邊界

- 與 V2 commit 比較，852 個 generated C# 檔案 bytes 完全相同；R5 模型 namespace、members、base／interface graph
  由原有 snapshots 繼續驗證。SimpleQuantity source 亦維持原有 compiler auxiliary asset。
- Aggregate SDK／Runtime／Models API 僅新增 `R5ModelProvider` 的四個唯讀 properties；ModelSupport 公開面另有 snapshot。
  ModelContracts snapshot 不變。定義 assembly 的改變由 PE／AQN／實際 factory 型別測試固定。
- R5 ownership policy 更新實際 assembly layout；Tool／CodeGen patch version 升為 1.2.1，descriptor compatibility
  同步 policy hash。兩份 generated manifests 更新版本與 descriptor／policy provenance，沒有 schema 或 CLI option 變更。
- Runtime public API、compiler reference bytes 與 `runtime-kernel-v2` 不變。Descriptor 檔案 hash 因 compatibility metadata 改變。
- TPA 可能包含 test host 的 SDK／Models／Support；CodeGen 排除 MyFhirSdk family implementation assemblies，
  不讓其補足缺少的 generated declarations。負面測試驗證缺少 SimpleQuantity auxiliary source 時仍失敗。
- R4 V2 fixture 改從共用 ModelSupport source 匯出 test-only primitive copies，記錄 provenance；V0／V1／V2 的歷史 evidence 不改寫。

## 驗證與證據

本機結果：完整 solution 976 通過、0 失敗、1 項指定外部 Client smoke 跳過；4 項 V3 專屬案例、
31 項 V2 案例、13 項 Python 完整性測試通過。K3 的 10 個 gates 與 23 項 failure-mode harness checks 通過。
G3 與 K3 的 SDK／Runtime／Contracts／Models／Support deployment hashes 已逐一比對一致。

[G3 summary](summary.json)、[V3 cases](v3-test-cases.json)、[consumer rebuild](consumer-rebuild-summary.json)
與 [file hashes](evidence-files.json) 記錄本機結果。G3 執行完整 G2／solution regression，驗證 V2 source／API 保留、
四項 V3 ownership／dependency／inventory／support API cases，以及實際測試的七個 implementation DLL hashes。
K3 執行 consumer inventory、clean regression、相依 DLL 重編、隔離部署、四種 supporting DLL 缺檔失敗、API inventory 與歷史 pins 保護。
Provider-only consumer 直接使用 R5 provider，確認 factory／codec／validation rules 成功且 SDK 未載入。

```powershell
dotnet restore MyFhirSdk.sln
python eng/Test-FhirV3Migration.py --output artifacts/fhir-versions/v3/run
pwsh -NoProfile -File eng/Test-KernelConsumerRebuildHarness.ps1 -OutputDirectory artifacts/fhir-versions/v3/harness
pwsh -NoProfile -File eng/Test-KernelConsumerRebuild.ps1 -OutputDirectory artifacts/fhir-versions/v3/consumers
dotnet run --project Tests/FhirVersions/V3/ProviderConsumer/ProviderConsumer.csproj -c Release
```

需要完整 git history（包含 V2 pin）、SDK 9.0.317、Python 3.10+、PowerShell 7.2+。
CI 的 G3 Windows／Ubuntu job 使用 `fetch-depth: 0`。唯一允許的 skip 仍是 endpoint 未設定的指定外部 Client smoke。
原始本機 logs／TRX／DLLs 位於 ignored `artifacts/fhir-versions/v3/verified-release/` 與 `consumers/`。
G3 與 K3 統一使用 `DOTNET_CLI_UI_LANGUAGE=en-US`，避免 MSBuild 生成註解的語系差異改變 PDB checksum／PE bytes。
開發中失敗的 run 仍保留於 ignored artifacts，包含待同步的 1.2.1 hash／CLI baselines；
正式 G3 證據來自更新基線後的新完整 run。1.2.1 的五份 tool baselines 存於本目錄 `tool-1.2.1/`，未覆寫 K4 歷史檔案。

## Consumer 遷移與 rollback

ProjectReference 指向 SDK 的應用程式會取得新的 transitive dependencies，但所有 current consumers 與相依 DLL
仍須全面重編。直接 DLL reference 除 SDK／Runtime 外需新增 R5 Models，例如：

```xml
<Reference Include="MyFhirSdk.R5.Models">
  <HintPath>$(ModelsAssemblyPath)</HintPath>
</Reference>
```

部署需一起提供 `MyFhirSdk.dll`、`MyFhirSdk.R5.Models.dll`、`MyFhirSdk.ModelSupport.dll`、
`MyFhirSdk.ModelContracts.dll`、`MyFhirSdk.Runtime.dll` 及 consumers 的 deps/runtimeconfig。
Assembly-name-based 設定、reflection lookup、保存的 CLR AQN 需從 `MyFhirSdk` 改為 `MyFhirSdk.R5.Models`；
例如 `MyFhirSdk.Resources.Patient` 的 namespace／FHIR JSON 名稱維持不變，defining assembly 改變。
不提供 binary drop-in 相容性或 type forwarders。

Rollback 以完整 V2 source commit `fa8dbb38c4809b028151167887d5d72968d100a8` 為單位，在獨立 checkout／worktree
還原 SDK／Models ownership、consumer source、tool manifest／policy／descriptor／generated manifests，清理舊產物後
restore、重編全部 consumers 並執行該 revision 的 G2／K3。不要混用 V2 SDK 與 V3 Models DLL。
既有 K0–K7 baseline pins／fixture 保留原樣；本次沒有執行使用者分支或 main 的回復操作。

V4 才加入正式多版本 generator configuration／policies，V5 才交付完整 R4 Models；本階段未宣稱 production R4 或公開 NuGet release。
