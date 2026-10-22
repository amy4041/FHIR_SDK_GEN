# D0：固定來源與引用盤點

日期：2026-10-21；review 修正：2026-10-22。D0 實作保留產品 API；D1–D5 尚未實作。本紀錄的來源數量取自實際 Git archive 與工作目錄掃描。

## 來源與 baseline 證據

固定 `f52c64a2b5c4b7db81a254a1286dda626f8c91e7`，即 README 的候選 baseline。開發起點 `eae2b5913885da3d0ebd676a97f67a113ed36721` 僅新增本次規劃文件。GitHub REST API 已確認 [CI 39553734204](https://github.com/amy4041/FHIR_SDK_GEN/actions/runs/39553734204) completed／success；21 個 jobs 全數 success，包含 Windows／Ubuntu V7、跨平台比較、K0、tool/compiler reference 與一般 regression。完整 job 連結保存於 [ci.json](baselines/d0/ci.json)。這是該 revision 的結果，沒有借用 `c7680b7` 的 CI。

[inventory.json](baselines/d0/inventory.json) 保存固定 revision 的 2,375 個檔案路徑、大小與 SHA-256；以停用 checkout 換行轉換的 Git archive 讀取，不把目前工作目錄冒充 baseline。包含 1,533 個 Generated 檔案（1,529 個 C#、四份 generation manifests）、17 份特別標記的 API／tool／Runtime 契約檔案。`files` 同時包含全部政策、lock、inputs 與歷史證據的 hashes。

六份 Approved API 快照另複製至 [snapshots](baselines/d0/snapshots)：SDK／Runtime aggregate、R4 Models、R5 Models、ModelContracts、ModelSupport、Client。Aggregate 的 namespace 篩選不包含全部 SDK 型別。Review 後新增 [完整 SDK API](baselines/d0/full-sdk-api.txt) 與其 [TW Core 公開面子集](baselines/d0/twcore-api.txt)，含完整型別、成員與 defining assembly；不再依賴部分 namespace 快照作為 IG 基準。

新增快照由固定 `f52c64a` 的隔離來源重建，使用同 revision 的 `Tests/KernelMigration/Inventory` 工具擷取，沒有採用目前 bin 或修改歷史 snapshot。[full-api.json](baselines/d0/full-api.json) 保存來源 revision、inventory 工具來源 hashes 及兩份新快照 hashes。每次 D0 驗收都重新從固定來源 build／擷取 API 並逐 bytes 比對新快照；`--record-api-baseline` 僅供一次性建立，已有檔案時拒絕覆寫。歷史 baselines 及原始 D0 inventory 未被改寫。

Tool 固定 `1.3.1`，SDK `9.0.317`，TFM `net9.0`。R4／R5 `runtime-contract.json` 為 schema 2、`runtime-kernel-v2`；compiler reference 為 `MyFhirSdk.Runtime/net9.0`、assembly version `1.0.0.0`、token `null`。R5 descriptor 記錄的 reference SHA-256 為 `8b990107f921287068ffc8326ce69b28a422dace2fcbf5a657ef50a7adde8e71`。契約來源與 hashes 均在 inventory；CodeGen 使用已驗證的 Runtime compiler metadata image，不得改拿 SDK implementation DLL。D0 未重新製作 compiler asset，實際工具與兩平台驗收證據來自上述固定 revision CI。

## 引用清單與遷移分類

`inventory.json.references` 為固定 revision 的逐檔、逐行、逐種類清單；目前來源輸出至 `artifacts/sdk-decoupling/d0/references.json`，每次重新掃描。每筆是 lexical candidate，不是語意呼叫次數；同一行可有多種類，alias、反射及 dynamic compilation 需配合人工檢查。Generated 模型不列入待遷移呼叫端。

| 類別 | 目前筆數 | 處理方式 |
| --- | ---: | --- |
| product | 64 | D1–D3 修改產品組合、內部 fallback 與具體 IG ownership |
| current-test-or-fixture | 402 | 仍參與目前編譯／執行；遷移功能測試，不能整批視為凍結來源 |
| current-example | 4 | repository 的 R4／R5／Dual 範例與 README |
| current-harness | 15 | source compiler、project inventories、歷史 gate 的角色需逐一遷移 |
| documentation | 173 | 遷移目前操作文件；歷史紀錄保留來源身分 |
| frozen-consumer | 3 | `Tests/KernelMigration/BaselineConsumer/v1` 不改；配對其歷史 SDK |
| historical-evidence | 8 | 舊 baselines 不改，不拿來接受新 API drift |

核心影響面：

| 入口／檔案 | 現況與後續工作 |
| --- | --- |
| `Serialization/Json/FhirJsonParser.cs`、`FhirJsonSerializer.cs` | public 無參數 constructor 使用 R5 context；internal metadata-only overload 使用預設 registry。D3 移除／改為明確 provider |
| `Validation/FhirValidator.cs` | public 無參數 constructor 與 internal rule-provider/walker overload 使用預設 registry；D1 增 contextual 介面，D3 清除 fallback |
| `Client/Requests/FhirResourceTypeResolver.cs` | 無參數 constructor 使用 R5 context |
| `ModelMetadata/R5/R5ModelContext.cs` | SDK 內靜態 R5 組合入口；production engines、V5/V6/V7、V1–V3 architecture、動態測試來源均有引用 |
| `ModelMetadata/R5/R5ModelMetadataProvider.cs` | metadata／rules adapter 委派回 R5 context；V1 experiment 與 primitive/runtime architecture tests 仍使用 |
| `Primitives/Runtime/PrimitiveRegistry.cs` | Default 直接由 R5ModelProvider 組合；測試含 reflection 取得 Default 的案例，不能只搜尋直接呼叫 |
| `Client/FhirClient.cs`、`Client/Abstractions/IFhirClient.cs` | 非泛型 Client 搜尋固定回傳 R5 Bundle；目前 Client tests 及 kernel migration consumers 需重建 |
| `Client/Responses/FhirResponseHandler.cs`、`Client/Abstractions/IFhirResponseHandler.cs` | 固定 R5 Bundle handler；保留通用 response processor、executor、sender、request/URI builder |
| `ImplementationGuides/TwCore/**` | SDK 預設 glob 編入具體 IG；D2 排除並移入獨立專案 |
| `Tests/Architecture/KernelIntegrationSeamTests.cs` | Roslyn 拼裝 SDK／Models；D2 更新來源分類，保留 ownership 測試 |
| `Tests/CodeGen/Runtime/RealSdkSourceCompiler.cs` | `ReadSources`／`CreateSdk` 把 ImplementationGuides 來源一起編入 SDK；D2 必須同步移除，不能只改 csproj |
| `Tests/KernelMigration/Consumer`、`RebuiltLibrary`、`RebuiltConsumer` | 目前實作的 rebuild fixtures，D3 遷移；`Inventory` 是通用 assembly inventory 工具 |
| `Tests/FhirVersions/V6/Smoke`、`V7/Program.cs` | 目前版本 smoke／部署 consumers，D3 改成 provider 明確組合 |
| `eng/Test-KernelConsumerRebuild.ps1` 等 K/V runners | 專案分類、allowlist、aggregate API preservation 與歷史來源重建須分開處理 |

## 待移除／變更 API

D3 刪除四個無參數 constructor：`FhirJsonParser()`、`FhirJsonSerializer()`、`FhirValidator()`、`FhirResourceTypeResolver()`；刪除 `R5ModelContext`、非泛型 `FhirClient`／`IFhirClient`、`FhirResponseHandler`／`IFhirResponseHandler` 的完整公開面。精確舊簽名由凍結 API snapshots 及上述 pinned source 定義。

內部退場項為 `R5ModelMetadataProvider`、`PrimitiveRegistry.Default`、三個 engines 的隱含 registry overload 及 SDK → R5 Models reference。`ProfileValidator(IFhirValidator, ...)` 兩個 overload 須在 D1 引入 contextual 組合後、D3 結束前退場；`IImplementationGuidePackage` 增版本／resource 支援宣告屬 D1 breaking delta。TW Core 型別 defining assembly 由 SDK 改成 IG DLL，屬 D2 binary breaking change。

保留明確 context constructor、typed Client／介面、共用 HTTP 執行核心及 `FhirHttpException`；泛型 exception 仍使用後者保存 HTTP 資訊。`MyFhirSdk.Primitives` namespace 有中立執行元件與 Models 型別，不能整批刪除。D0 沒有更動以上 API。

## TW Core 身分與三條 POC

| 項目 | 固定 baseline 現況 | 目標 |
| --- | --- | --- |
| PackageId | `tw.gov.mohw.twcore#1.0.0` | 保留手寫 POC 身分；不宣稱載入官方 archive |
| Name | `TW Core` | POC 文件明示範圍 |
| Profile | `https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore` | 保留 canonical |
| FHIR base | 字串 `R4.0.1`，實際 `MyFhirSdk.Resources.Patient`（R5） | `4.0.1`，`MyFhirSdk.R4.Resources.Patient` |
| `TWCORE-PAT-002` | identifier 至少一個非 null item；Cardinality | D2 保留 |
| `TWCORE-PAT-003` | 每個非 null identifier 的 system 存在；Required | D2 保留 |
| `TWCORE-PAT-004` | 每個非 null identifier 的 value 存在；Required | D2 保留 |

system／value 的 presence 為 primitive 非 null 且 `HasValue || Extension.Count > 0`；不是單純非空白字串檢查。system／value 規則略過 null identifier，缺整個 identifier 由 002 判斷。issue 保留 Source／PackageId／ProfileUrl／RuleId。D2 要補正向、三種缺漏、extension-only 與 R5 拒絕測試。本次不導入官方 IG、FHIRPath、terminology 或額外規則。

## Repository 外使用端

`D:\projects\SDK_DEMO` 只讀結果另存 [external-inventory.json](baselines/d0/external-inventory.json)。僅保存應用程式／專案與生成 manifests 的相對路徑、大小、hash、命中種類及行號，不保存原始碼、endpoint 或憑證，沒有啟動程式或 server 操作。

`SdkDemo/Program.cs:39` 使用 `R5ModelContext.Default`；R4 已使用明確 provider 組合，Client 已使用泛型入口。R5 namespace alias 對版本 Models 的引用屬應用程式正常依賴。`SdkDemo.csproj` 使用 DLL 參考，日後需依實際情境重新 export、替換整包、重建；不是只替換 SDK DLL。範例見 [D0_Migration_Examples.md](D0_Migration_Examples.md)。不改動該專案。

## D 系列驗收與 CI 分工

```powershell
python -m unittest discover -s Tests/SdkDecoupling -p "test_*.py" -v
python eng/Test-SdkDecoupling.py --output artifacts/sdk-decoupling/d0-run-01
python eng/Test-SdkDecoupling.py --require-decoupled --output artifacts/sdk-decoupling/d0-target-01
```

前兩個是 D0 gate；第三個是目標差異探針，目前應失敗並記錄 14 個結構差異。需要固定 .NET SDK 與 Python，無需 pwsh。輸出目錄必須全新且嚴格位於 repository artifacts 下；重跑時更換目錄名稱。每次執行先寫 Pending，例外寫 Failed 並非零退出；既有目錄直接拒絕，避免混用上次證據。

IG 與 Models 依賴檢查使用 `dotnet msbuild -getItem:Compile,ProjectReference` 的實際評估結果，另保存 `sdk-msbuild-items.json`。註解、未生效的 Condition 或同名無關 property 不會被當成排除設定，Remove 後再次 Include 的來源仍會被偵測。此探針不等於完整 D3 assembly／部署驗收；`summary.json` 保持 `scope=source-pin-and-inventory-only`、`fullyDecoupled=false`。TRX 不混入來源檢查結果；baseline 已建立後拒絕 `--freeze` 覆寫。

`protectedDrift` 對已知 UTF-8 契約檔正規化 CRLF→LF，比較固定來源與 checkout 的邏輯文字；內容修改、遺失仍失敗。Generated 檔案維持原始 bytes 精確比對，新增、刪除、內容或換行變更均會失敗。原始來源 inventory 仍保存未正規化 hashes。

| 階段 | CI 角色 |
| --- | --- |
| D0 | 新增獨立 Windows／Ubuntu D0 job，安裝固定 SDK，驗證 source pin、六份舊 snapshot、完整 SDK／TW Core 的 pinned rebuild、生成／契約不變與盤點失敗案例；保留全部既有 K/V jobs |
| D1–D2 | 新 framework／IG tests 與 API delta；D0 的舊 SDK snapshot 保留，新 gate 須顯式區分可變 SDK／IG API 與不變 Models／Runtime／tool，不能直接修改舊 pin 接受 drift |
| D3 | 同一可建置變更遷移目前測試／consumers，新增實際 assembly graph、無版本依賴與 clean publish 驗收；在 breaking API 生效時同步切換會做歷史 API preservation 的目前 CI 入口 |
| D4 | 完成 D 系列 canonical generation、fresh tool install、typed HTTP、R4/R5/Dual/optional IG 部署、缺 DLL 負例、來源 rollback、兩平台比較與 artifacts；舊 K/V runner 以原始來源隔離重跑 |
| D5 | 文件與 repository 外重建 smoke；外部 endpoint 未設定的 skip 與實際 server 成功分開 |

舊 V3→V7 串接 aggregate SDK API 比較，不能在 D3 更新快照後繼續假裝適用。D0 runner 也只適用本階段，不在未來偷偷放寬 `protectedDrift`；D1 實作須提供獨立、經 review 的 breaking delta 驗收策略。

## 驗證紀錄

首次實作的 D0 source gate、七項 unit tests 與既有 37 項 FHIR versions Python integrity tests 已通過。2026-10-22 review 修正後，新增契約換行／內容 drift、Generated bytes、舊目錄拒絕、例外 Failed、實際 MSBuild 評估、API snapshot／provenance drift 及 TW Core 公開面覆蓋案例；目前共 14 項 D0 tests。修正後的完整驗收另存新目錄，見下列 review 結果。當前來源 inventory 保存原始 bytes hashes；summary 的來源 fingerprint 只對 UTF-8 文字正規化 LF，保留跨平台可比性。

Windows `.NET SDK 9.0.317`、Python `3.13.13` 的完整 solution Release regression 已執行：九個 test assemblies、1,171 項，1,165 通過、5 失敗、1 略過。五個失敗皆為 `CodeGenToolPackageTests.PackageInventory_CompilerSourceEncoding_MatchesRuntimeValidation` 的不同 encoding cases，原因是本機沒有 `pwsh` executable；沒有改成 skip 或刪除測試。既有 Client external smoke 未設定 endpoint，保持略過。TRX 位於 `artifacts/sdk-decoupling/d0/regression/`；此本機完整回歸結果為 Failed，不能冒充全綠。

遠端新 D0 jobs 須提交後才能驗證，既有 baseline CI 不代表本次新增程式已在 Ubuntu 執行。D0 來源／盤點驗收通過與本機完整回歸的環境限制分開記錄；後續具備 PowerShell 7 的環境需重跑 packaging cases。

## 2026-10-22 review 修正結果

四項 P2 已修正並補測：契約換行誤判、重跑留下成功摘要、csproj 字串造成的 IG 誤判，以及 TW Core 公開 API 基準缺漏。產品 C#、Models、Runtime、生成來源與六份原始 API snapshots 均未修改；外部 SDK_DEMO 沒有寫入或執行。

- 14 項 D0 tests 通過，MSBuild 案例包含註解、無關 property、未生效 Condition、有效 Remove、Remove 後重新 Include，以及 Models ProjectReference。
- 既有 37 項 FHIR versions Python integrity tests 通過；`git diff --check` 通過。
- 固定來源隔離重建與 API 比對通過，完整 SDK 快照涵蓋 49 個公開型別，TW Core 子集涵蓋 2 個公開型別，保留原本由 SDK 定義的 assembly 身分。最終 D0 證據位於 `artifacts/sdk-decoupling/d0-review-final/`。
- `artifacts/sdk-decoupling/d0-review-target/summary.json` 為嚴格目標探針的預期 Failed（退出碼 1），14 項目標差異仍存在，沒有將尚未解耦標記為成功。

CI D0 job 已加入固定 SDK 安裝，會執行相同的 pinned rebuild／API 比對；只上傳頂層 evidence、快照與 logs，避免打包暫存重建來源及 bin／obj。本次未修改產品程式，因此未重跑完整 .NET solution regression；前次五項缺少 pwsh 的環境失敗仍保留於上節，不視為已解決。新兩平台 CI 結果待提交後確認。
