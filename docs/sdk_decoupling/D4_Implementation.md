# D4：目前 CI 與正式交付遷移

D3 已 commit 並由使用者確認 CI 通過，來源固定為 `d47374bf99e91a61848fccf7612e2d571ae4bf67`。D4 保持產品 SDK／IG 公開 API、Models、Runtime、Contracts、Support 與 generator 來源不變，將正式交付與目前驗收入口遷移至明確 context／typed Client。

## 交付組合

`Export-FhirDelivery.py` 支援 `--composition R4|R5|Dual|R4TwCore|all`，預設 all。common DLL 為 SDK、Runtime、ModelContracts、ModelSupport；R4／R5 各加自己的 Models，Dual 加兩版 Models，R4TwCore 加 R4 Models 與獨立 TW Core DLL。R5 不支援 TW Core POC。單版匯出只包含該版 inputs 與對應範例。

Exporter 將 inventoried sources 複製到 repository 外的全新目錄建置，不沿用目前 bin／obj。可用 `--tool-package` 指定 CI canonical nupkg；未指定時由全新來源 restore／pack。驗證套件 ID、版本、實際 Runtime compiler reference hash 與沒有 SDK／Models implementation 夾帶。

交付 manifest schema 升為 2，保存選定 compositions 的版本、必要 DLL、IG POC scope、每個組件的實際 assembly identity、來源 revision／fingerprint、工具 hash、FHIR inputs 與所有交付檔案 hashes。`contracts/` 額外保存組件 API、Runtime contract 與 committed generated hashes。schema 1 的歷史交付仍以固定 checkout 的舊 verifier 驗證。

外部模板的 SdkReferences.props 依 UseR4Models／UseR5Models／UseTwCore 選擇 references。範例包含 synthetic round trip、validation、controlled typed HTTP read；R4TwCore 增加三條手寫 Patient POC 的正負案例與注入替換。這不等同完整官方 TW Core 支援。

```powershell
python eng/Export-FhirDelivery.py --composition R4 --output artifacts/delivery/r4
python eng/Test-FhirDelivery.py --bundle artifacts/delivery/r4 --output artifacts/delivery/r4-evidence
```

兩個輸出目錄均須尚未存在，evidence 不可寫進 immutable bundle。Verifier 在 repository 外、空 NuGet cache、停用繼承 endpoint 的環境執行。每個案例只複製自身必要 DLL，檢查 deps.json、實際載入路徑、SDK references、實際版本與 delivered binary hashes；逐一移除必要 DLL，要求非零退出及缺件診斷。Windows 關閉預期失敗的錯誤對話框並設 timeout。

工具從 local-only feed 全新安裝，核對已安裝 Runtime reference；每版生成兩次並逐檔符合 committed hashes。保存可供 fresh integration 使用的 generated tree，而非只比較 source count。

## 目前 gate 與歷史 gates

新增 `Test-SdkDecouplingD4.py`，接受 Windows／Ubuntu 共用的 canonical tool package。

```powershell
dotnet restore MyFhirSdk.sln -p:NuGetAudit=false
python -m unittest discover -s Tests/SdkDecoupling -p "test_*.py" -v
python eng/Test-SdkDecouplingD4.py --tool-package artifacts/packages/MyFhirSdk.CodeGen.Tool.1.3.1.nupkg --output artifacts/sdk-decoupling/d4/local
```

流程包含目前 neutral SDK 完整 gate、正式 export／外部驗證、將全新安裝工具的生成結果放入另一份全新 source tree 後執行 V5／V6／TW Core 整合測試，以及整份固定 D3 source rollback 的完整 regression／四組部署。Fresh integration 必須通過與目前 full regression 完全相同的案例身分，不能只接受數量下限。

D4 重用 D3 neutral gate 的 API／project graph／K3／67 個必要案例與部署檢查。唯一新增的 stable-source 例外是本階段必須遷移的 `examples/external/SdkReferences.props`；產品 build props／targets 仍不可漂移，未修改歷史 runner。D4 snapshots 與 D3 API 完全相同，另存於 baselines/d4，歷史 D0–D3 證據維持固定來源。

| CI／工具類型 | D4 處理 |
| --- | --- |
| D4 platform／compare | 目前來源，Windows／Ubuntu 使用同一 canonical nupkg，再比對來源、生成與 rollback 證據 |
| D0–D3 | 各自固定 accepted revision；D3 新固定為上述 commit |
| V3–V7 API preservation／歷史交付 | 保留既有固定 checkout；不放寬舊 SDK API 比較 |
| V1／V2 | 繼續驗證目前來源中仍適用的 neutral context／SPI 契約 |
| K0／K7 | 保留原始固定 baseline 的重建與整份來源 rollback；目前 consumer 已於 D3 遷移 |
| K3 | 目前 dependency allowlists、消費端重建、完整 TRX、缺件診斷沿用 D3 已遷移版本 |
| K4–K6／compiler-reference／tool smoke | 保留目前 toolchain、reference isolation、安裝及生成檢查；產品契約不變 |

`Compare-SdkDecouplingD4.py` 要求兩個不同平台的完整 artifacts；重算 evidence inventories、交付檔案、TRX、必要案例、fresh integration、生成與 rollback 證據。缺少任一平台不能宣稱 cross-platform Passed。實際 server smoke 未由本 gate 執行，externalServerAcceptance 保持 Pending；D5 再做實際 server 與結案。

這次不修改 SDK_DEMO、不執行遠端 FHIR 寫入、不發布 NuGet、不提交或 push。

## Code review 修正

- 比對器先要求 execution report 的 path 為正確的 `<deployment>/<assembly>.dll`，並核對 assembly identity 與 manifest 完全相同，再於副本重定位至下載後的 artifact 路徑。原始報告不被修改，repository 載入路徑、錯誤檔名與錯誤版本皆拒絕。
- 工具套件可使用任意輸入檔名；驗證 nuspec ID／版本及 Runtime reference 後，交付 feed 一律存成 `MyFhirSdk.CodeGen.Tool.<version>.nupkg`。套件 bytes／SHA-256 保持不變，manifest 記錄正規化後的交付路徑。
- 外部 README 改為正確的中立 SDK 依賴說明，生成範例依 manifest.fhirInputs 選擇版本，不再要求單版交付缺少的另一版 archive。回歸測試以 PowerShell 實際解譯 README 指令，分別驗證 R4-only／R5-only 的生成參數。
