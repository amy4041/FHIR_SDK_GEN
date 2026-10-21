# 實作計畫：D0–D5

本文件是後續工作順序，並非已完成紀錄。每階段須有可建置的 commit、review 與對應檢查；不得以尚未補完的 API snapshots 或停用測試讓 CI 表面通過。

## D0：固定基準與盤點呼叫端

- 確認 README 記錄的候選 baseline，保存 source inventory、API snapshots、生成 hashes、tool／Runtime compiler reference 契約及完整 CI 連結。
- 盤點無參數 engines、resolver、R5ModelContext、內部 metadata／registry fallback、非泛型 Client／handler、TW Core 與動態 Roslyn source compiler 的引用。
- 區分目前程式、歷史 frozen consumer、文件範例與 repository 外使用端。使用者的 `D:\projects\SDK_DEMO` 只讀盤點，另外提供遷移範例；不直接改寫或自動連到該專案的 server。
- 確認目標 TW Core 是 Design.md 的 R4 三規則 POC，記錄原有 package／profile／RuleId 身分，不混入完整官方 IG 導入。
- 建立 D 系列驗收入口／CI 過渡方案。保留現有產品 API，先讓新的驗收可測目前與目標的差異。

交付：D0 來源 pin、引用清單、待移除 API 清單、CI 分工及 baseline 證據。規劃時的檔案與數量不能代替實際 inventory。

## D1：先建立版本明確的 IG framework

- 新增 contextual validator 介面與唯讀 context，FhirValidator 實作；保留 IFhirValidator 的一般驗證用途。
- 增加 package resource 支援宣告、FHIR base version 正規格式、重複 profile／版本／型別檢查。
- 建立新 ProfileValidator 組合路徑，測試基礎與 IG issues 合併、注入替換、自訂驗證器及未知 profile 策略。
- 以小型測試用 IG 驗證 R4／R5，避免 framework 的測試依賴 TW Core 才能完成。
- 可短暫保留已標明待刪的舊 overload，讓 D1 可建置；不可讓新路徑偷偷委派回 R5 defaults，D3 完成前必須移除過渡 overload。

交付：IG 中立契約與測試、目前 API 變更清單、後續刪除清單。

## D2：TW Core 獨立專案及 R4 POC

- 新增 `ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj`，引用 SDK、R4 Models；加入 solution。
- SDK 增加對 ImplementationGuides 來源的排除；型別只能由新 IG DLL 擁有。
- 三條 Patient POC 規則改用 R4 types，version 改為 `4.0.1`，更新 package 適用型別宣告，使用 D1 framework。
- `Tests/ImplementationGuides/TwCore/` 引用新 IG 與所需 R4 Models；驗證無 identifier、缺 system、缺 value、primitive extension presence、正向資料及 R5 型別拒絕。
- 更新動態 SDK source compilation：`Tests/Architecture/KernelIntegrationSeamTests.cs`、`Tests/CodeGen/Runtime/RealSdkSourceCompiler.cs` 等不能繼續把具體 IG sources 拼進 SDK compilation。
- 更新 K3 project inventory／分類／dependency allowlist 及相應 harness，不用略過新專案來避開驗收。

交付：獨立 IG DLL、正確 R4 型別 ownership、POC 範圍文件與通過的 framework／IG tests。SDK 在本階段仍可能因舊 API 引用 R5，不宣稱已解耦。

## D3：移除 R5 defaults 與舊 Client

- 先把目前仍用舊 API 的測試、consumers、fixtures helpers 改成明確 context／typed Client；保留測試涵蓋的功能。
- 刪除 Design.md 的退場 API、內部 R5 fallback，移除 SDK → R5 Models ProjectReference。
- 保留共用 HTTP 執行核心與 exception 資訊；更新 public API snapshots 並 review 每一項刪除／新增。
- 在同一可建置變更中遷移 V6／V7 sample programs、目前 kernel migration consumers 及其 dependent library。凍結的歷史 source 不改。
- 檢查 SDK project graph、實際組件 references 與乾淨 publish；不只搜尋 `using MyFhirSdk.Resources` 就當成完成。

交付：SDK 無 R4／R5 Models 及具體 IG 依賴；R4-only、R5-only、Dual 回歸與部署證據。

## D4：目前 CI 與交付遷移

- D 系列 runner 保存新 SDK／IG snapshots、TRX、source fingerprint、DLL inventories 與明確的允許 skips。
- 舊 V3–V7 runner 會串接並比較歷史 SDK public API；在新 API 下無法原封不動繼續通過。明確替換目前 CI 的驗收入口，保留歷史 runner／baselines；若需重跑舊 gate，對其對應 revision 做隔離 checkout，不修改來源比較讓它接受未知 drift。
- 新驗收繼承仍適用的要求：生成一致、真實 Runtime reference、fresh tool install、各版本 runtime integration、typed HTTP、獨立部署與完整來源 rollback。舊 API preservation 改為本次明列的 breaking API delta；Models／Runtime 的不變要求仍保留。
- K0–K7、V0–V7 內與當前來源有關的 test helpers／project allowlists 要分類遷移；歷史重建仍使用歷史來源與其契約，不能混入新 snapshots 或舊 TRX。
- 更新 `eng/Export-FhirDelivery.py`、`eng/Test-FhirDelivery.py` 與外部模板：以使用情境組合 SDK + Models，不再一律要求六 DLL；IG 額外交付。
- R4／R5／Dual 三種 bundle 與 optional IG 的版本、必要檔案及 hashes 在 manifest 明列。SDK 在同一套 binaries 下服務不同 Models，不靠打包偷偷換實作。

交付：Windows／Ubuntu 的新 gates、交付 artifacts、外部執行與來源身分證據。

## D5：文件、實際 smoke 與結案

- 更新 repository README、外部 README、API 遷移範例、assembly dependency 圖與交付方式。
- 說明不再支援無參數 constructor／舊 Client，以及 TW Core POC 已移至獨立 DLL 且只適用 R4。
- 使用最新乾淨來源重新 export／publish；在 repository 外重新驗證 R4／R5／Dual 與 IG replacement。
- 外部 server smoke 須明確 endpoint、版本、操作及 synthetic data。CI 未設定 endpoint 可依既有規則略過，但實際 server 驗收不得因此標為 Passed。
- 記錄結果與剩餘範圍；不回寫舊 V7 baseline，不自動合併 main、不發布 NuGet。

交付：D 系列結案、可重現的交付包及使用端遷移文件。
