# D1：版本明確的 IG framework

日期：2026-10-22。D0 已提交為 `447e7f141c5edf1e803b3d6c85c3bfca9778c5de`；本階段建立中立 IG 組合契約與驗收，不代表 D2 的 TW Core R4 移植或 D3 的 SDK 解耦已完成。

## 契約與組合

`IContextualFhirValidator : IFhirValidator` 新增唯讀 `FhirModelContext Context`。一般 `IFhirValidator.Validate(Resource)` 不變，仍可獨立使用。`FhirValidator` 實作新介面；公開的 context constructor 暴露傳入的同一個 immutable context。無參數 constructor 暫時仍使用既有 R5 預設，D3 移除。

internal rule-only constructors 沒有完整 context；讀取其 Context 或拿來做新的 IG 組合會明確拋出 InvalidOperationException，不會捏造 R5 context。這些測試／內部組合亦列為 D3 後續盤點項。

新組合方式：

```csharp
var context = new FhirModelContext(
    R4ModelProvider.Metadata,
    R4ModelProvider.Primitives,
    R4ModelProvider.Validation);
var profileValidator = new ProfileValidator(
    new FhirValidator(context),
    myR4Package);
```

`IImplementationGuidePackage` 新增必要方法 `SupportsResource(string profileUrl, Type resourceType)`。第三方實作須更新並重建；不以 GetRules 的空集合推測 resource 是否受支援。支援但沒有額外規則的 pair 可以回傳空集合，仍執行 base validation。

建立 contextual ProfileValidator 時檢查：

- FHIR base version 必須是正規三段數字，且與 context.Identity.FhirVersion 完全相等；`R4.0.1`、`4.0`、前導零、preview 後綴及空白不會被近似接受。IG package 自己的版本號不參與此比較。
- 相同 profile URL 不得跨 package 或在同一 package 重複宣告，使用 Ordinal canonical 比對。
- package 須提供 ID、非空白且不含 `|version` 的 profile keys，並與 SupportsProfile 宣告一致。
- options 與必要依賴必須有效；profile registrations 會保存為該 validator 自己的查詢表，不依 first-registration-wins 選 package。

## 驗證行為

1. 在呼叫任何 base validator 前，使用捕捉到的 context 檢查整個模型圖 ownership。跨版本或混合巢狀模型拋出 FhirSdkException，不能作為一般 IG success。
2. 執行 base validator 一次並保存原始 issues。一般 base issues 不阻止 IG 規則繼續收集。
3. 選取明確 profile 或 meta.profile，去除重複 URL。已知 profile 的 CLR resource type 必須由 SupportsResource 明確支援；否則加入 `PROFILE-RESOURCE-UNSUPPORTED` error，即使未知 profile 策略是 Ignore。
4. 未知 profile 沿用 options：explicit 預設 Error、declared 預設 Ignore，也可選 Warning。`url|version` 明確產生 `PROFILE-VERSION-UNSUPPORTED` error，不會偷偷去掉版本或因 Ignore 而吞掉。
5. 執行適用 rules，將結果合併；framework 設定 Source=ImplementationGuide，以及實際 PackageId／ProfileUrl／RuleId。base issues 保持原資料。null rules、null issues、空 RuleId 屬規則配置錯誤，明確拋出例外。

沒有遠端下載、官方 IG archive、FHIRPath、terminology 或 DI container 相依。

## 小型測試用 IG

`Tests/Validation/Profiles/ContextualProfileValidatorTests.cs` 內含 synthetic package 與 rules：

- profile 使用 `https://example.test/StructureDefinition/d1-patient`，package 為 `synthetic.d1#9.2.3`，分別宣告 `4.0.1`／`5.0.0` 與各版 Patient CLR type。
- 使用可控制的空規則或合成 issue，驗證 base＋IG 合併、同一 context 注入不同 package 的替換效果與 provenance。
- 驗證兩版正向／負向、跨版本及巢狀 ownership、已知 profile 不支援 resource、未知策略、重複註冊、版本格式、重複 meta.profile 與錯誤配置。

測試不引用 TwCorePackage，也不宣稱這些規則是官方 IG。Architecture tests 另外檢查 IFhirValidator 保持原契約、Context 無 setter、舊 overload 的 Obsolete 標記，以及 internal rule-only validator 不得假裝有 context。

## API delta 與過渡移除清單

新增公開面：IContextualFhirValidator／Context、FhirValidator.Context、package.SupportsResource、兩個 contextual ProfileValidator constructors，以及現有 TwCorePackage 的 SupportsResource 實作。SDK 的一般 API snapshot 更新並另保存於 D1 evidence；D0 snapshots 不變。

[D1 完整 SDK API](baselines/d1/full-sdk-api.txt)、[aggregate API](baselines/d1/ApprovedPublicApi.txt) 與 [精確 delta](baselines/d1/api-delta.json) 已記錄。完整 SDK inventory 增加 11 筆、沒有刪除公開成員；aggregate 中 FhirValidator 的 interface 描述更新，並增加新介面、Context 屬性、SupportsResource 方法與 constructors。Obsolete attributes 由專門的 reflection test 驗證。

兩個 `ProfileValidator(IFhirValidator, ...)` overload 暫時保留並標記 Obsolete，明示 D3 移除。它們保留舊的無版本組合路徑，不能當成新路徑的 adapter。以 FhirValidator 直接呼叫 constructor 時，C# 會選新的 contextual overload；自訂驗證器應實作新介面後注入。

目前 TW Core 仍是宣告 `R4.0.1`、使用 R5 Patient 的歷史 POC。D1 只增加 resource support 方法；其既有測試明確轉成 IFhirValidator 以走已標記的過渡 overload，保持三條規則原測試涵蓋。新路徑會拒絕此不正確版本宣告。D2 必須完成獨立 DLL、真正 R4 types、base version `4.0.1`，並將這兩處過渡測試改成 R4 contextual 組合。

D3 仍須移除無參數 engines／resolver、R5ModelContext、非泛型 Client／handler、registry／metadata fallback、SDK→R5 ProjectReference，及兩個過渡 ProfileValidator overload。沒有新增 compatibility assembly 或 type forwarders。

## 驗收與 CI 過渡

目前來源使用新的 D1 runner：

```powershell
dotnet restore MyFhirSdk.sln -p:NuGetAudit=false
python -m unittest discover -s Tests/SdkDecoupling -p "test_*.py" -v
python eng/Test-SdkDecouplingD1.py --output artifacts/sdk-decoupling/d1/run-01
```

需固定 SDK、Python 與 PowerShell 7（部分既有 packaging tests 使用 pwsh）。輸出必須是 artifacts 下的新目錄；先寫 Pending，任何失敗保留 Failed 與 logs。`--record-api` 僅供一次性保存本階段經檢視的完整 SDK snapshot、aggregate snapshot 與精確增刪清單，已存在時拒絕覆寫；正常 CI 只能比對已記錄的差異，不會自動接受新 drift。

D1 gate 保存完整 source inventory／fingerprint、API delta、實際 Runtime compiler reference hash、全 solution regression 的 TRX、九個 test assemblies 的 counts 與 skip reasons。它檢查 D0 evidence 與已接受 revision 完全對應，並保護 Runtime、Contracts、Support、Models、Generated、CodeGen 及 inventory tool sources；僅允許 D1 已明列的 SDK API snapshot 變更。完整回歸只允許既有、未設定 endpoint 的一項 Client external smoke skip。

共用 kernel 的 `core/` 來源與兩版 core package fixtures 也列入保護；文字僅正規化 checkout 換行，binary inputs 精確比對。Generated 仍維持 D0 的 bytes 精確要求，實際 Runtime compiler reference 必須符合既有 `8b990107f921287068ffc8326ce69b28a422dace2fcbf5a657ef50a7adde8e71`。

Review 修正後，所有位置的 `.props`／`.targets`（包含 `Directory.Build.props` 與新增的 `Directory.Build.targets`）也與固定來源比對，新增、刪除或內容異動都會阻擋驗收。測試驗收另有固定的 37 個 D1 案例清單，逐一核對 TRX 的完整名稱、theory 參數、所屬 assembly 與 Passed 結果；缺少任一 R4／R5 案例、重複、略過或失敗都不能通過。清單獨立於當前測試探索結果；逐案證據保存在 `d1-required-tests.json`，摘要含 `requiredD1TestCounts`。過長的非法 profile 測試 URL 已縮短，避免 TRX 顯示名稱截斷而無法穩定識別案例。

API 改變後，舊 D0 gate 與 V3→V7 API-preservation runners 無法代表目前來源。CI 將這些 jobs 明確命名為 Historical，checkout 固定 D0 revision `447e7f1`；原 runner／baselines 不改，不放寬歷史比較。D1 job、V1／V2 regression、K3 consumers、compiler reference、fresh tool／generated SDK integration 等仍使用目前來源。D4 再完成新的當前版本完整 delivery／跨平台 gates。

## 執行紀錄

### K0 CI 建置 runtime 固定

D1 的 [CI run 39730860885](https://github.com/amy4041/FHIR_SDK_GEN/actions/runs/39730860885) 中，Windows／Ubuntu D1 jobs 均已通過，唯一失敗在 K0 歷史 SDK implementation hash。成功與失敗的 runner image 分別為 `20261007.264.1`、`20261018.275.1`；固定 SDK 並未固定實際執行 MSBuild／compiler 的 runtime patch。

本機以相同歷史來源、SDK `9.0.317`、`en-US` 及關閉 compiler server 重建，runtime `9.0.20` 得到原始 `d47a702c5b52277d81f96867ad026b447fc46cfc9e7fcf08d16fd47de1615afc`；改用官方 runtime `9.0.21` 即精確重現 CI 的 `65a5c217e1427f42b55f90f4cba3c53e565ee7a5f62ce8334cc0ffb7253f856f`。原有語系／Git metadata 隔離無法涵蓋 runtime patch 差異。

K0 job 現改用 `runner.temp` 下獨立的 `DOTNET_INSTALL_DIR`，安裝 SDK `9.0.317` 與 `9.0.318`；後者僅提供原始 build runtime `9.0.20`，實際選用 SDK 仍由 `global.json` 固定為 `9.0.317`。建置前由 `KernelBuildRuntime.proj` 在 MSBuild process 中讀取 `System.Environment.Version`，核對並保存 `buildRuntimeComparison`；不符立即失敗。歷史建置關閉 shared compilation，避免使用其他 runtime 啟動的 compiler server。沒有更改原始 DLL hash、SDK source pin 或歷史 snapshots。

本機驗證輸出使用 `artifacts/kernel-k0-runtime-fixed-verified-network/` 與 `artifacts/kernel-k0-runtime-harness/`；前者為完整 K0 gate，後者含 runtime mismatch 的 failed evidence 測試。新 CI 尚須推送後驗證。

本機環境：Windows、SDK `9.0.317`、Python `3.13.13`。NuGet 還原已完成；官方 PowerShell `7.6.7` zip 經 release asset 的 SHA-256 核對後，放在 `artifacts/tooling/pwsh-d1/expanded/`，沒有系統安裝。透過測試既有的 `MYFHIRSDK_TEST_PWSH` 環境變數指定執行檔。

- Review 修正後 D 系列 Python failure-mode tests：23 項通過，包含逐一移除 37 個必要案例、全部遺失、重複／略過／失敗／錯誤 assembly、參數變動及共用建置檔異動的負向驗證；既有 FHIR versions integrity tests：37 項通過。
- 新 framework 測試：34 項；Validation assembly 共 108 項通過。新增 Architecture 介面／過渡契約測試 3 項，該 assembly 共 246 項通過。
- 第一輪完整 gate 位於 `artifacts/sdk-decoupling/d1/acceptance-api/`：1,208 項、1,206 通過、1 失敗、1 略過。唯一失敗是既有 `DocumentedR4CommandsProduceCompleteModelsAndPrimitivesInOneTree` 回傳 UnsafeOutputPath（exit 5）；單獨重跑通過，TRX 位於 `codegen-diagnostic/`。沒有刪除、略過或放寬該測試；此輪仍保留 Failed。
- 最終完整 gate 使用新目錄 `artifacts/sdk-decoupling/d1/acceptance-final/`。實際通過／失敗與九個 assemblies 的 counts 以該目錄 `summary.json`、`regression.json`、TRX 為準，不混用第一輪或單項重跑報告。只允許未設定 endpoint 的既有 Client smoke skip。

本階段的新 SDK／IG API delta 已逐項檢視並補上對應測試；code review 的兩項 P2 已修正，新 Windows／Ubuntu D1 CI 尚待確認。Review 修正後的完整驗收獨立保存在 `artifacts/sdk-decoupling/d1/review-fixed/`，結果以該目錄的 `summary.json` 及逐案證據為準。沒有修改 D0／K／V 歷史證據、沒有改寫外部 SDK_DEMO、沒有連線 FHIR server，也沒有 commit／push。本階段不宣稱 SDK 已不依賴 R5 Models，亦不宣稱 TW Core 已支援 R4。
