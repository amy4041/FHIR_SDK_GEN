# D5：文件、交付重驗與實際 smoke 入口

日期：2026-10-27。D4 已由使用者確認 CI 完成，最新來源為 `6477df9`。D5 開發涵蓋目前文件、乾淨來源交付驗收及獨立的只讀 server smoke；使用者明確表示沒有測試環境，實際 server 驗收先保留 Pending，D 系列尚未全數結案。

## 開發項目

- Repository README 改為中立 SDK、明確 context、typed Client、獨立 R4 TW Core POC 的目前用法；移除失效的建構子與舊依賴敘述。
- 新增 [交付與遷移指南](Delivery_and_Migration.md)，包含 assembly 依賴圖、四種組合、API 退場對照、provider／IG 注入、export／publish、升級與 rollback。
- `Test-SdkDecouplingD5.py` 預設全新 export／pack，再使用現有正式 verifier 在 repository 外驗證四組部署、缺件診斷、IG 替換、空 cache tool install 與兩版重複生成。可接收同來源 bundle，拒絕不同 revision／fingerprint／dirty 狀態，執行後再次確認來源不變。
- 正式 gate 要求乾淨、已提交來源。`--allow-working-tree` 只供開發 preview，離線成功也不把乾淨來源標為 Passed。報告分列 offline、clean source、external server、series closeout。
- CI 在 D4 platform gate 後，用同輪 bundle 執行 D5 離線驗收，另存 artifact，不改寫 D4 的 hash inventory。D4 Windows／Ubuntu compare 與所有歷史 pins 維持原責任。
- 外部模板為每個 release 記錄 actual server 的 NotRun／Passed；只讀路徑增加 metadata 版本核對與 Patient ID 核對，停用 redirect，錯誤不印 remote body／憑證。
- `Test-FhirDeliveryServer.py` 需要明確 release、環境設定與 synthetic Patient 確認，外部 publish 後才向子程序提供該版憑證；驗證實際載入 DLL 與 manifest。它不被一般 CI 自動呼叫。

## 驗收紀錄

本機驗證使用 portable .NET SDK 9.0.317。D 系列 Python 工具測試 57 個通過，版本工具測試 37 個通過。README 的 R5 JSON／typed Client 與 R4 TW Core C# 範例已在 repository 外對交付 DLL 實際編譯。

`artifacts/sdk-decoupling/d5/dev-preview-network/` 保存全新來源 build／pack 與離線證據：R4、R5、Dual、R4TwCore 均通過 publish／run 與逐一移除必要 DLL；TW Core 正負案例與注入替換通過。空 cache 全新安裝工具，R4 的 677 個／R5 的 852 個 C# 來源各生成兩次，逐檔符合 committed hashes。這份開發 preview 的 workingTreeDirty 為 true，clean-source acceptance 保持 Pending。

本機 HTTP stub 另驗證六種情境：R4／R5 正向、metadata 版本不符、Patient 404、Patient ID 不符、redirect。版本不符與 redirect 都只到 metadata 即停止，其他負向結果為 Failed；兩版正向經實際外部 consumer 與 typed Client 執行，檢查 bearer header 與報告不含測試 URL／ID／token。結果保存於 `artifacts/sdk-decoupling/d5/loopback-tests.json`，不計為實際 server 驗收。

未加 preview 旗標的 dirty checkout 已確認會以 exit code 2 拒絕，且不建立驗收輸出。正式 clean-source 與 Windows／Ubuntu 結果須由提交後的新 CI 確認。更新紀錄後的完整來源會再執行一輪 `final-preview`；兩輪 artifacts 各自保留其原始來源 fingerprint，不回寫較早 manifest。

| 項目 | 狀態 |
| --- | --- |
| D0–D4 | 使用者已確認先前 CI 通過；原始基準保留 |
| D5 本機離線 preview | Passed：四組部署、缺件診斷、IG 替換、兩版生成 |
| D5 工具測試／README 編譯／本機 stub | Passed：review 修正後 60 個 D 工具測試；先前 37 個版本工具測試、兩組文件範例與六種 stub 情境保留原紀錄 |
| D5 clean-source Windows／Ubuntu CI | Pending：提交後執行 |
| R4 實際 server | Pending：使用者尚無測試環境 |
| R5 實際 server | Pending：使用者尚無測試環境 |
| D 系列結案 | Pending：須核對新 CI 與兩版實際 server 證據 |

## 剩餘範圍

沒有外部環境時不以 CI skip、受控 handler 或本機 stub 代替實際 server Passed。保留 D4 API／Models／Runtime／生成契約，D5 沒有新增產品 SDK API；歷史 V7、D0–D4 baselines 不回寫。不更動 SDK_DEMO、不合併 main、不發布 NuGet。

## Code review 修正

- P2：離線與 server 入口從同一份 manifest bytes 解析 JSON／計算 hash，驗收結束前再次檢查原始 hash，避免只驗證記憶體中的舊物件而漏掉磁碟 manifest 變更。Server 的 Passed flags 移到最後 integrity 檢查之後才寫入。
- P3：server 入口保存固定的 failureStage／errorCode 與非零子程序退出碼，可區分建置、server execution、metadata 版本、Patient 缺失或 ID、報告驗證及 integrity 失敗。Timeout 也保留階段；不記錄 exception message、原始 build／server output、Patient 或憑證。
- 回歸測試實際呼叫兩個 CLI 的 main，以模擬子程序在驗證中改 revision 或追加空白；要求 Failed／非零退出，並保留未變更的正向案例。另驗證 publish 失敗、執行失敗、timeout、metadata／Patient 診斷、錯誤 JSON 與不洩漏敏感字串。D 工具測試共 60 個通過。
- 修正後另外重新 export／外部驗證至 `artifacts/sdk-decoupling/d5/review-fix-preview/`，保留獨立來源 fingerprint；新範例的只讀診斷再以本機 stub 驗證，仍不計為實際 server Passed。
