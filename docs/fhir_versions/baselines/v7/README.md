# V7：跨平台交付與 handoff

V6 commit `99017eff8c608aa1e34292ddaf10eab60912bbed` 已由使用者確認 CI 通過。
V7 延續同一 branch，新增交付驗收，不改變 Runtime／Models／SDK／Generator 的公開 API、版本或生成內容。

## 驗收層級

- `G7-platform`：單一 OS 的完整 G6 回歸、canonical tool 安裝／生成、fresh integration、部署及 rollback。
- `G7`：Windows 與 Ubuntu 兩份通過的 artifacts 經 comparator 核對後，才接受跨平台交付。
- 本機 Windows 成功不能代表 Ubuntu 或跨平台通過。實際本機結果以本目錄 `summary.json` 為準；新增 CI 尚待提交。
- 外部 R4／R5 server acceptance 沿用當次 G6 smoke 結果。未設定 endpoint 時保持 Pending，不以受控 HTTP 或交付驗收替代。
- 公開 NuGet 發布、R4 IG／TW Core、完整 terminology／FHIRPath validation、跨版本轉換仍不在範圍內。

## 新增工作

`eng/Test-FhirV7Delivery.py` 接收一份明確的 canonical `.nupkg`。CI 兩個 OS 都下載既有 `compiler-reference-asset` job 產出的同一份 package，而不是各自重打包作為 G7 輸入。

1. 重新執行 G6，核對當前來源 fingerprint、九個 regression assemblies、TRX 計數及允許的 skips；V0–V6 的歷史 evidence 不改寫。
2. 在 checkout 外、空 NuGet cache，僅由本機 feed 安裝 canonical tool，核對 package 身分與 compiler reference。
3. 依固定的 R4 `4.0.1`／R5 `5.0.0` input locks，對兩版各生成 models／primitives 兩次；比對完整檔案集合及 bytes，並與提交模型核對。
4. 在另一份乾淨 source tree，以當次生成結果編譯，從新 dependency cache 還原套件，執行 81 項 V5 與 92 項 V6 tests。
5. 重新 publish R4、R5、Dual 三個 consumer，在獨立部署目錄執行 round trip、validation 與 typed HTTP。Dual 在同一程序使用兩個 context；受控 handler 不連外。
6. 檢查實際載入的 SDK／Runtime／Contracts／Support／Models 路徑都在部署目錄；逐一移除必要 DLL，確認 process 非零退出且指出缺少的 assembly。三個 consumer 共 16 個負面案例。
7. 用 `git archive` 在隔離目錄還原完整 V6 pin，重新建置並跑九個測試 assemblies，記錄 current／restored 各自 counts、來源與 hashes；不 reset 目前 branch、不改 main。

`eng/Compare-FhirV7Delivery.py` 核對 canonical package、兩份來源身分、tool／SDK／contract／policy／input identities、同一 FHIR 版本的完整生成 hashes、實際 TRX、部署輸出與缺少依賴清單、rollback 證據。
不要求不同平台的 implementation DLL bytes 相同，也不比較 R4 與 R5 模型 bytes 相等。
來源另記錄 UTF-8 文字 CRLF→LF 的 portable fingerprint；binary 保持原始 bytes，各平台 raw fingerprint 仍保留。

## 操作方式

使用 pinned .NET SDK `9.0.317`、Python 3.13 與 PowerShell 7.2+；Git history 須含 V2／V5／V6 pins。

```powershell
dotnet restore MyFhirSdk.sln
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
python eng/Test-FhirV7Delivery.py --tool-package artifacts/packages/MyFhirSdk.CodeGen.Tool.1.3.1.nupkg --output artifacts/fhir-versions/v7/run
```

依賴測試套件的 fresh restore 需要 NuGet 網路。canonical tool 本身只從指定本機 feed 安裝，不使用遠端 feed 補齊工具內容。
輸出路徑必須是 `artifacts/` 下不存在的新目錄。完整原始 logs、TRX、生成樹、可部署的三個 consumer 留在該目錄。

下載兩個 CI artifacts 後執行：

```powershell
python eng/Compare-FhirV7Delivery.py --windows artifacts/v7-windows --linux artifacts/v7-linux --output artifacts/v7-comparison/summary.json
```

這個命令必須使用真實 Windows／Linux 產物；不能複製同一平台的 summary 改名充當另一平台。
Evidence integrity tests：`python -m unittest discover -s Tests/FhirVersions -v`。

## Consumer migration 與部署

- 既有 R5 程式保留無參數 Parser／Serializer／Validator、舊 Client 及 resolver 的 R5 defaults。
- R4 程式明確引用 R4 Models，建立 `FhirModelContext`，使用 typed Client；同程序並用時每個 instance 固定 context，不能混用 CLR Models。
- 重新編譯 application 及相依 libraries；型別搬到 Models／Runtime 後，不提供舊 assembly 的 type forwarders。不要只替換舊 application 的 SDK DLL。
- 以 `dotnet publish --self-contained false` 的完整輸出部署，包含 application `.dll`／`.deps.json`／`.runtimeconfig.json` 與相依 implementations；目標機須具備 .NET 9 runtime。
- 共同必要元件為 `MyFhirSdk.dll`、`MyFhirSdk.Runtime.dll`、`MyFhirSdk.ModelContracts.dll`、`MyFhirSdk.ModelSupport.dll`；另含實際使用的 Models。
- SDK 目前仍參考 R5 Models，因此 R4 application 的 publish 輸出也可能包含 R5 Models，應保留完整輸出。本次沒有承諾 R4-only SDK physical package。
- `Client/Responses/FhirResponseHandler` 是舊 R5 相容層。新 Client 捕捉 `FhirHttpException<TOutcome>` 或共用 `FhirClientException`，不能只捕捉舊非泛型 HTTP exception。
- 呼叫端持有並釋放 HttpClient；在首次使用 HttpClient 前建構 Client。每次操作的 request／response 由 executor 釋放。

## Rollback 操作

本次自動演練目標為已驗收的 V6，屬於完整 source rollback：以獨立 checkout／worktree 還原 pin、還原套件、重新建置／測試／publish，再替換完整部署。不可將新舊階段的 DLL 任意混搭。
V6 原始碼仍包含 R4 支援；若需退回「加入 R4 前的 main」，V0 固定的 main restore point 為 `ca0c27f81adb729657da8ddab61c71c70e113984`，它是另一個 rollback 目標，不把 V6 演練誤稱為已驗證此 main。
既有 K0／K7 歷史 rollback 機制保留。本次 G6 prerequisite 也執行 current／historical assembly inventory 的 rollback failure-mode harness。
還原來源不可接受不相符的舊 TRX；來源 fingerprint、輸入與 tool hashes、SDK／configuration、assemblies、counts／skips 必須一起核對。

## Handoff register

| 項目 | 目前交付／後續條件 |
| --- | --- |
| 共用 Runtime／SPI | Runtime 14 個公開 kernel 宣告，Contracts 為中立 provider DTO；版本身分須一致 |
| Models／Support | R4／R5 Models 各自擁有模型與 provider，共用 codecs／rules 在 ModelSupport |
| 生成契約 | Tool `1.3.1`、`runtime-kernel-v2`、descriptor schema 2、manifest schema 3；依版本固定 policies／auxiliary assets |
| 跨平台交付 | 本機只記錄實際 OS；Windows／Ubuntu 新 CI 加 comparator 通過後才接受 G7 |
| 外部 Client | 明確版本 endpoints 與 smoke 結果另驗收；目前未執行者保持 Pending |
| 驗證能力 | 以各版本 provider inventory 為準；未實作的 terminology／FHIRPath 等不宣稱支援 |
| 套件發布 | 未上傳公開或私人 feed；license／signing／provenance／promotion 與發布政策另案 |
| R4 IG／TW Core | 另指定 package、FHIR base 與 rules 驗收，不沿用 R5 rules 宣稱 R4 支援 |
| 保留的 main | 同一 feature branch 開發，未 merge／重寫 main |

## 本機證據

Windows `G7-platform` **Passed**，原始證據位於 ignored `artifacts/fhir-versions/v7/verified`：

- 當前完整 solution：1171 項，1170 通過、0 失敗、1 項既有外部 Client smoke 因未設定 endpoint 略過。
- Python integrity：22 項通過，包括 9 項新增 V7 failure-mode checks。
- G6 prerequisite 與 G7 canonical fresh integration 各自執行 81 項 V5 SDK／92 項 V6 HTTP cases，全部通過。
- canonical package 安裝使用空快取與本機 feed；R4／R5 各自兩次生成一致，完整 677／852 個 C# 與 manifests 符合提交模型。
- R4、R5、Dual 三個獨立部署成功；16 個 missing-dependency cases 通過，正常執行的 implementations 全部來自各自部署目錄。
- V6 whole-source rollback：九個 assemblies，1171 項，1170 通過、0 失敗、1 項同樣的外部 smoke 略過；還原輸入不變。
- 原始 Windows artifact 已通過 comparator 的單平台完整性檢查。Linux／跨平台比較、新 CI 與外部 server acceptance 仍 Pending，見 `acceptance-status.json`。
- 既有 K3 consumer rebuild 守門檢查全數通過，包含新增三個 consumer 的 project inventory、完整 solution 回歸、舊 consumer／相依 library 重建與部署。補充證據位於 `artifacts/fhir-versions/v7/consumer-inventory-accepted`，可攜摘要為 `consumer-rebuild-summary.json`／`consumer-inventory.json`。

先前 `artifacts/fhir-versions/v7/development` 在 G6 fresh restore 遇到沙箱 NuGet 連線限制而中止，不作為最終通過證據。授權連線後完整重跑的 `verified` 才是本次驗收。
補充 K3 本機執行曾遇到 clean 清除輸入 reference，以及 Windows 錯誤回報讓預期的 unhandled dependency exception 停留。最終以 `artifacts` 中固定 reference 與 process-local `SetErrorMode(3)` 無互動設定重跑通過；這是本機啟動設定，沒有改動 SDK 或既有 consumer 邏輯。完整本機啟動腳本保存在 ignored `artifacts/fhir-versions/v7/Invoke-K3.ps1`。
本目錄保留 platform summary、必要生成／部署 inventories、cases、rollback 摘要與 evidence file hashes；JSON 正規化為 LF，以便 Git 保存。
`summary.json` 中 prerequisite／inventory 的原始檔案 hashes 對應完整 raw artifacts；本目錄可攜檔案的 hashes 以 `evidence-files.json` 為準。跨平台 comparator 使用完整 artifacts，不使用這個可攜子集替代 TRX、package 或部署實體。
不提交 logs、DLL、`.nupkg` 或完整 publish 目錄；CI artifacts 與本機 ignored `artifacts/` 保存這些原始證據。

## Review 修正驗證

兩項 comparator 問題已修正：

- 外部驗收由 R4／R5 原始 smoke 的狀態推導，檢查 release／FHIR version、passed operations，並要求與 G6 embedded records、G6 summary、平台 summary 一致。Skipped 只能得到 Pending；summary 不得自行提升為 Passed。
- 部署 inventory 必須實際包含共同必要 DLL、對應 Models，以及 application `.dll`／`.deps.json`／`.runtimeconfig.json`。移除必要檔案後，即使重新計算 inventory、hash 與檔案數也必須拒絕。

Python integrity 共 31 項通過；新增測試直接執行完整 `validate_leg`，未 mock 掉驗收器。涵蓋原始 smoke 缺失／不一致、偽造 Passed、部分與全部版本通過，以及 R4／R5／Dual 共 25 個必要部署檔案的逐一移除。合成 Windows／Linux fixture 僅驗證 comparator 邏輯，不代表實際 Ubuntu 交付通過。

原始 Windows `verified` artifact 經修正後的驗收器重驗通過；兩項 review 重現案例在 artifact 副本中已被拒絕，恢復檔案後仍通過。結果見 `review-fixes.json`。
本次僅變更 comparator 與其 Python 測試，未重跑完整生成／部署／rollback。原始 platform summary 與其來源 fingerprint 保留；補充驗證另外記錄當前 source fingerprint、驗收器 hash、被驗證 artifact 的原始 summary hash 及測試結果，避免把原始完整執行誤標為修正後來源的重新執行。
跨平台 G7、新 CI 與外部伺服器驗收維持 Pending。
