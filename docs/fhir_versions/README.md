# MyFhirSdk FHIR 多版本開發

Version 0.1 — 2026-10-13

本目錄規劃在現有 repository 中新增 FHIR R4 支援，並保留 R5。目標是同一個應用程式可以
同時使用兩個版本，各自生成、解析、序列化與驗證資料。R4／R5 資料互轉另案處理。

## 閱讀順序

| 文件 | 用途 |
| --- | --- |
| [R4 support ADR](MyFhirSdk_FHIR_R4_Support_ADR.md) | 範圍、版本邊界、依賴方向、相容性與待定設計 |
| [Implementation Guide](MyFhirSdk_FHIR_R4_Support_Implementation_Guide.md) | V0–V7 工作包、前置條件、交付內容與開發順序 |
| [Acceptance Criteria](MyFhirSdk_FHIR_R4_Support_Acceptance_Criteria.md) | 各階段的驗收證據、正向／負面案例與完成定義 |

V0 已建立 baseline runner、R4 input lock 與 [設計決策](V0_Decisions.md)。
執行結果及 G0 狀態見 [V0 baseline](baselines/v0/README.md)；尚未新增 R4 production 支援。
namespace／Models assembly 方向由 V0 固定；[V1 selected closure 實驗](baselines/v1/README.md)已提交，使用者確認 push CI 通過。
[V2 context／SPI](baselines/v2/README.md)已提交並由使用者確認 CI 通過；
[V3 R5 Models 分離](baselines/v3/README.md)將模型與 provider 編譯至獨立 assembly，共用實作由 ModelSupport 擁有。
V3、[V4 雙版本 generator](baselines/v4/README.md)已提交，使用者確認 CI 通過。
[V5 完整 R4 Models 與 runtime integration](baselines/v5/README.md)已提交，使用者確認 push CI 通過。
[V6 R4／R5 Client integration](baselines/v6/README.md)已提交於 `99017ef`，使用者確認 CI 通過；外部伺服器驗收依實際 smoke 結果標示。
[V7 跨平台交付與 handoff](baselines/v7/README.md)已提交，包含 canonical package 生成、乾淨部署、缺少依賴與 V6 source rollback 驗證；首次 CI 的兩個平台驗證通過，跨平台比較修正後待完整 CI 重跑。
V7 首次 CI 的跨平台 rollback 比較遇到 Git CRLF／LF 匯出差異，原因與修正、完整 CI 重跑方式見 [V7 CI rollback fix](V7_CI_Rollback_Fix.md)。

## 目前基準與目標

文件建立時 checkout 為 `main`，HEAD 為 `dbb6964`（PR #46，K7 merge）。此 revision 是
規劃時的參考位置；V0 須再固定完整 source revision、輸入 hashes 與實際執行證據。
使用者已確認 K7 分支 push CI 通過；不以此紀錄代替新工作包或合併後 main 的 CI 證據。

| 維度 | 目前已實作 | 本階段目標 |
| --- | --- | --- |
| FHIR | R5 `5.0.0`／R4 `4.0.1` Models、engines 與明確 context 的 typed Client | 外部 Client smoke 與 V7 正式交付分別驗收 |
| Repository | Runtime、SDK、Generator 同 repository | 保持同 repository，以專案與契約劃分責任 |
| Generator | Tool `1.3.1`，共用 pipeline，各版本固定 policies 與資產 | G4 雙版本生成；G5 實際編譯當次生成來源 |
| Models | `MyFhirSdk.R4.Models.dll`／`MyFhirSdk.R5.Models.dll` 各自擁有模型與 provider | 維持單一 compile owner，無 SDK 反向依賴 |
| Engines | Parser／Serializer／Validator 在 SDK，預設 R5 | 使用明確版本 context；獨立 engines assembly 視實作需要 |
| Runtime | 最小 kernel，含 bootstrap types | 優先共用，先驗證 bootstrap 與跨版本邊界 |
| 工具鏈 | SDK `9.0.317`／`net9.0` | 此工作不順帶升級 TFM |

目前精確契約見 [Kernel handoff](../gen/MyFhirSdk_Runtime_Kernel_Extraction_Handoff.md)、
[CodeGen README](../../CodeGen/README.md) 與
[Compatibility matrix](../../CodeGen/Compatibility/GenerationCompatibilityMatrix.cs)。

## 建議先做什麼

先完成 V0 baseline／設計決策，再以 V1 小範圍實驗驗證共用 Runtime 與雙版本 context。
接著完成 provider／composition 契約及 R5 Models 分離，才展開完整 R4 生成。
Models 分離與 engines 解除 R5 固定依賴需配合進行，不能只搬動檔案。

Client 的版本相依 API 必須在 R4 Client 交付前處理；完整 Client／IG package 拆分、
通用 constraint Profile generator、CLI input 退場、完整 terminology／FHIRPath validation
及公開 NuGet release 都不是基礎 R4 models／JSON 支援的前置條件。

## Repository 維護原則

- 共用 Generator、適用的 engines 流程及測試 harness；版本差異放在明確的設定／policy／provider。
- 分開 R4、R5 的輸入、生成來源、snapshot、測試與證據，避免彼此覆寫。
- 延續目前 generated source 的提交方式；R4 採用前盤點檔案數與容量。若改為 build-time generation，另訂遷移，不與本工作默默混用。
- 官方 package fixture 延續既有 R5 fixture／lock 模式，評估是否提交或以固定 hash 取得；不能只記 URL 或依賴浮動 cache。
- DLL、NuGet build outputs、TRX、下載快取及暫存 tree 放在忽略目錄或 CI artifacts；可攜證據摘要才提交。
- CI 按版本拆 job 並共用快取；Runtime、Generator、contracts／engines 的共同變更要驗證兩個版本。

## 規格來源

- [FHIR R4 官方下載與 core package](https://hl7.org/fhir/R4/downloads.html)：R4 `4.0.1`、`hl7.fhir.r4.core`。
- [FHIR R4 datatype 定義](https://hl7.org/fhir/R4/datatypes-definitions.html)、[FHIR R5 datatype 定義](https://hl7.org/fhir/R5/datatypes-definitions.html)：用於確認版本差異。

官方 definitions 是生成輸入；SDK 宣稱的支援能力仍須以實際 policy、executor 與驗收證據為準。
