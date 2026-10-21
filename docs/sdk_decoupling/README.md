# SDK 版本解耦與 IG 專案化

日期：2026-10-21。狀態：實作規劃，尚未執行以下程式變更。

目標：移除共用 SDK 對 R5 Models 的直接及間接依賴，刪除 R5 預設組合與舊版 Client，將 TW Core 移至獨立專案，同時保留 IG 的注入與替換能力。

本目錄命名為 `sdk_decoupling`，表示解除 SDK 與版本模型、具體 IG 的耦合；仍維持同一 repository，不拆 Git 儲存庫。工作包使用 D0–D5，與既有 Runtime extraction K0–K7、FHIR 多版本 V0–V7 分開。

## 文件

| 文件 | 用途 |
| --- | --- |
| [設計決策](Design.md) | 目標依賴、API 移除清單、IG 契約與 TW Core 範圍 |
| [實作計畫](Implementation_Plan.md) | D0–D5 的工作順序、檔案影響與階段交付 |
| [驗收與遷移](Acceptance_and_Migration.md) | 架構、功能、部署與 CI 驗收，以及使用端遷移方式 |

## 目前來源

規劃時 HEAD：`f52c64a2b5c4b7db81a254a1286dda626f8c91e7`，訊息為 `feat(delivery): finalize V7 handoff with external R4/R5 examples`。這是 D0 的候選 baseline；D0 需再確認該 revision 的 CI／交付驗證結果，不將先前 `c7680b7` 的 CI 當成此 revision 的證據。

先前成果見 [V7 結案](../fhir_versions/V7_Closeout.md)、[V7 交付指南](../fhir_versions/V7_Delivery_Guide.md)。使用者先前已完成 R4／R5 generator 與實際 server 的手動 smoke；新 API 遷移後仍需另做對應版本的驗收。

## 本次範圍

- 共用 SDK 只依賴 Runtime、ModelContracts、ModelSupport，不引用任何版本 Models 或具體 IG。
- Parser／Serializer／Validator／resolver／typed Client 都使用明確的 immutable context。
- 移除舊 API，不新增 R5 compatibility assembly，不提供假裝中立的 R5 fallback。
- IG 驗證框架保留於 SDK；TW Core 的具體規則編譯到獨立組件。
- 保留三條 Patient POC 規則，規劃移植到 R4 模型並驗證；不宣稱已支援完整官方 TW Core。
- 更新目前測試、外部範例、交付與 CI；既有歷史 baseline 不改寫。

本次不升級 TFM、不改變 FHIR core inputs、不新增 R4／R5 轉換、完整 terminology／FHIRPath 或官方 IG 自動生成；公開／私人 NuGet 發布另案。原則上保持 Runtime／Models／generator 的既有公開契約與生成 bytes，SDK／IG API 的 breaking changes 則明列並驗收。

## 執行方式

先完成 D0 盤點與驗收入口，再實作 D1–D5；每個工作包完成後做 code review、修正與對應 gates。可延續目前 branch，以獨立 commits 推進；本規劃不要求合併 main，不修改既有 main 還原點。

SDK 拆除版本預設與 TW Core 拆專案會互相影響。不得在中間階段宣稱已完成版本解耦；只有 D3 的組件／部署檢查通過後，才能作此結論。
