# D2：TW Core 獨立專案與 R4 Patient POC

D1 已由使用者確認 push 並通過 CI；D1 來源固定為 `464474b190e702231d4b26a7883f1edbebb13ac2`。D2 將既有手寫 TW Core Patient POC 移至獨立 DLL，改用 R4 Models，保留 D1 中立驗證介面。

## 專案與使用方式

新增 `ImplementationGuides/TwCore/MyFhirSdk.ImplementationGuides.TwCore.csproj`，直接引用 SDK、R4 Models，加入 solution。SDK 的 default globs 與 Compile items 同時排除 ImplementationGuides；SDK 不引用 IG。`TwCorePackage`、`TwCoreProfiles` 維持 namespace，定義組件改為 `MyFhirSdk.ImplementationGuides.TwCore.dll`，呼叫端須新增此專案或 DLL 引用並重建，沒有 type forwarders。

```csharp
var context = new FhirModelContext(
    R4ModelProvider.Metadata,
    R4ModelProvider.Primitives,
    R4ModelProvider.Validation);
var validator = new ProfileValidator(new FhirValidator(context), TwCorePackage.Default);
var result = validator.Validate(patient, TwCoreProfiles.Patient); // R4 Patient
```

模型及 primitives 來自 `MyFhirSdk.R4.Resources`、`MyFhirSdk.R4.Types`、`MyFhirSdk.R4.Primitives`；context／provider 分別來自 `MyFhirSdk.ModelMetadata`、`MyFhirSdk.ModelMetadata.R4`，IG namespace 為 `MyFhirSdk.ImplementationGuides.TwCore`。

IG 宣告 FHIR base version `4.0.1`，只支援已註冊 profile 與 R4 Patient 的組合。R5 context 在組合時因版本不符被拒絕，R5 Patient 在 R4 context 的 ownership 檢查被拒絕。D2 尚保留 SDK 的 R5 defaults／ProjectReference，因此 IG 經 SDK 仍可能有 R5 傳遞依賴；D3 才移除。

## 保留的 POC 行為

PackageId 保留 `tw.gov.mohw.twcore#1.0.0`，profile URL 保留 `https://twcore.mohw.gov.tw/ig/twcore/StructureDefinition/Patient-twcore`。這些是既有手寫 POC 身分，不代表已載入官方 package。

| RuleId | 規則 |
| --- | --- |
| TWCORE-PAT-002 | 至少一個非 null identifier |
| TWCORE-PAT-003 | 每個非 null identifier 的 system 存在 |
| TWCORE-PAT-004 | 每個非 null identifier 的 value 存在 |

Presence 保留 `HasValue || Extension.Count > 0`。僅有 extension 的 primitive 也視為存在，沒有 value／extension 的空 wrapper 不算存在。D2 不額外加入身分證格式、system allowlist 或其他官方 TW Core 限制。沒有導入 IG archive、StructureDefinition compiler、FHIRPath 或 terminology server。

## 測試與工具遷移

TW Core 測試改用 R4 context 與新 IG project reference；28 個案例涵蓋三條規則、正向資料、extension-only／empty primitive、R4 JSON round-trip、base／IG issue 合併與 provenance、重複 meta.profile、null identifier、R5 拒絕及實際 assembly ownership。

`KernelIntegrationSeamTests` 與 `RealSdkSourceCompiler` 的 SDK sources 清單移除 ImplementationGuides，完整回歸繼續驗證實際 SDK source compilation。K3 將新專案分類為 `production-r4-ig`，使用 evaluated ProjectReference／Reference／PackageReference 檢查直接依賴；harness 額外驗證缺少 R4、加入 R5、額外 DLL／package 均遭拒絕。

## D2 驗收與 API 證據

```powershell
dotnet restore MyFhirSdk.sln -p:NuGetAudit=false
python -m unittest discover -s Tests/SdkDecoupling -p "test_*.py" -v
python eng/Test-SdkDecouplingD2.py --output artifacts/sdk-decoupling/d2/run-01
```

需要固定 SDK 與 PowerShell 7.2+；本機可透過 `MYFHIRSDK_TEST_PWSH` 指定 portable pwsh。驗收目錄必須是 artifacts 下的新目錄。D2 runner 保留 D0／D1 snapshots，繼承 Models、Runtime、生成來源、compiler reference 與所有 `.props`／`.targets` 的不變要求；SDK／IG evaluated graph 及 ownership 亦納入檢查。

新 API 證據位於 `baselines/d2/`：SDK 完整 API、獨立 IG API、aggregate snapshot、相對 D1 的 delta。只允許上述兩個型別完整移至新組件；SDK 其餘 API 與 aggregate 不變。即使使用一次性的 `--record-api`，也拒絕超出此精確搬移的 API 變更，不覆寫已存在的證據。

Runner 執行全新 K3 harness 與完整 K3 gate，保存專案分類、九個 test assemblies 的 TRX、獨立 consumer rebuild／deployment 與依賴缺漏測試。固定 65 個必要案例（D1 framework／architecture 37 個、TW Core 28 個），逐案檢查完整名稱、theory 參數、assembly 與 Passed；缺漏／重複／略過／失敗均阻擋驗收。完整回歸僅允許既有未設定 endpoint 的 Client smoke skip。

CI 新增目前來源的 D2 Windows／Ubuntu job；D1 job 明確標為 Historical，checkout 已通過 CI 的 D1 revision。既有 D0／V 歷史 jobs 維持各自固定來源，K3／工具整合等仍使用目前來源。D4 的完整交付遷移尚未執行。

## 本機證據

初次記錄 API 的 `artifacts/sdk-decoupling/d2/record-api/` 已保存 snapshots，但 K3 restore 被網路沙箱阻擋，該輪維持 Failed；`acceptance-final/` 也保留前次 restore 失敗的 assets 錯誤。重新完成 restore 後，完整驗收另使用 `artifacts/sdk-decoupling/d2/acceptance-verified/`，以其 `summary.json`、`d2-required-tests.json`、K3 evidence／TRX 為準。Review 修正後 Python failure-mode tests 共 31 項。未連線外部 FHIR server，未改寫 SDK_DEMO，未 commit／push；新 CI 待推送後確認。

## Review 修正

P1：兩個 legacy `IFhirValidator` constructors 仍保留至 D3，但現在所有已知 profile 都須通過 `SupportsResource` 檢查。即使未知 profile 策略為 Ignore，不適用的 CLR resource 仍回傳 `PROFILE-RESOURCE-UNSUPPORTED` error，保留 PackageId／ProfileUrl 與 IG 來源；不再以空規則集合回報驗證成功。新增八個 TW Core 案例，涵蓋兩個舊 constructors 的 explicit 單一／清單／declared 選取，以及支援的 R4 Patient 正負向驗證；八項都納入固定必要案例清單。

P2：SDK 與 IG 的 evaluated graph 都禁止未允許的 DLL／package 引用；SDK 目前允許的四個 ProjectReference 不變，Reference／PackageReference 允許清單為空。K3 的 SDK 分類同步套用此檢查，harness 增加 SDK → IG DLL／package 的拒絕案例。Python 測試亦以實際 MSBuild import 注入兩種引用，確認被拒絕，不只比對靜態 csproj 文字。

修正前的失敗測試 TRX 保存在 `artifacts/sdk-decoupling/d2/review-red/`，定向通過結果位於 `review-green/`。Review 修正後完整驗收使用 `artifacts/sdk-decoupling/d2/review-fixed-verified/`，以該目錄 summary、65 個必要案例及 K3 evidence／TRX 為準；先前 `acceptance-verified/` 保留原始驗收紀錄。公開 API 與 D2 snapshots 沒有變更。
