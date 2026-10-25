# 驗收與使用端遷移

## 1. 架構完成條件

- SDK csproj 與傳遞 project graph 不含 R4／R5 Models、TW Core 專案；SDK implementation 的 assembly references 也不含它們。
- 不藉由反射、全域預設、靜態初始化、NuGet 傳遞依賴或 deployment fallback 載入版本 Models。
- Runtime、ModelContracts、ModelSupport 與 Models 保持既有依賴方向；不將 R5-specific code 移入共用組件以掩飾依賴。
- TW Core 型別由其獨立 DLL 定義，SDK 不再編譯這些來源。主專案的 default globs、動態 source compiler 及 test fixtures 都須檢查。
- SDK breaking API delta 明列並核准於 review；Runtime compiler contract、Models API 及生成來源預設保持不變，有額外差異必須具體說明。

## 2. 功能與 IG 驗收矩陣

| 情境 | 預期 |
| --- | --- |
| R4／R5 明確 context 的 Parse／Serialize／Validate | 既有正向、負向與 lexical 規則維持；版本差異仍正確 |
| 跨版本模型或混合 provider | 及早拒絕，不使用另一版 registry 補救 |
| typed Client 兩版 CRUD／search、錯誤回應、取消與釋放 | 既有受控 HTTP 功能涵蓋維持；各版 Bundle／Outcome 型別正確 |
| 相同 context 注入兩個不同實作的 IG package，分別建立 validator | 可替換，不必修改 SDK 或重建 Models |
| 自訂 contextual base validator | 可注入，基礎與 IG issues 都保留 |
| IG FHIR base version 與 context 不合 | 建立驗證器時拒絕 |
| 同一 validator 註冊重複 profile URL | 拒絕，不使用 first-registration-wins |
| 已知 profile 搭配不支援的 resource CLR type | 明確 error，不能當成空規則成功 |
| 已宣告支援但沒有額外規則的 profile | 可只執行 base validation；與不支援有不同宣告 |
| 未知 explicit／declared profile | 遵守 options；預設 explicit error、declared ignore 的行為清楚記錄 |
| 基礎 validation 有一般 issues | 收集適用 IG rules，最後合併；ownership／配置錯誤不作為一般 validation success |
| TW Core Patient R4 POC | 三條既有本機規則、positive／negative／extension presence cases 通過 |
| R5 Patient 配 R4 TW Core package | 拒絕，不能宣稱已支援 R5 TW Core |

各項結果須記錄來源、執行環境、實際測試集合與 counts；先前 smoke 通過不代替此次新 API 的結果。

## 3. 乾淨部署

| Consumer | 必要自家組件 | 不應需要 |
| --- | --- | --- |
| R4-only | SDK、Runtime、Contracts、Support、R4 Models | R5 Models、TW Core |
| R5-only | SDK、Runtime、Contracts、Support、R5 Models | R4 Models、TW Core |
| Dual | SDK、Runtime、Contracts、Support、兩版 Models | TW Core |
| R4 + TW Core POC | R4-only 的組件 + TW Core DLL | R5 Models |

從 repository 外、沒有共用 bin／obj 的目錄 publish／執行；檢查 deps.json、必要檔案與實際載入 assemblies。先不放入未使用的 Models DLL，再確認程式可執行；不要只在六 DLL 齊備時測試後推論「不依賴 R5」。

Windows／Ubuntu 各自驗證上述情境。必要 DLL 移除測試應以非零退出與診斷確認，處理 Windows 預期失敗 process 的無互動終止與 timeout；不得因錯誤對話框停住而誤記為成功。

## 4. 使用端遷移示意

以下 context／typed Client 語法為 D3 的使用方式；R5ModelContext 與無參數 engine 建構子已移除：

```csharp
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

var context = new FhirModelContext(
    R5ModelProvider.Metadata,
    R5ModelProvider.Primitives,
    R5ModelProvider.Validation);

var parser = new FhirJsonParser(context);
var serializer = new FhirJsonSerializer(context);
var validator = new FhirValidator(context);
```

R4 使用 R4ModelProvider，搭配 R4 Models。舊 Client 呼叫改為：

```csharp
var client = new MyFhirSdk.Client.FhirClient<
    MyFhirSdk.Resources.Bundle,
    MyFhirSdk.Resources.OperationOutcome>(httpClient, context, options);
```

R4 換成 `MyFhirSdk.R4.Resources` 對應型別。D1 已實作 contextual IG framework，D2 已建立獨立 R4 TW Core POC 專案；以下組合須額外引用 `MyFhirSdk.ImplementationGuides.TwCore` 與 R4 Models：

```csharp
var profileValidator = new ProfileValidator(
    new FhirValidator(r4Context),
    TwCorePackage.Default);
var result = profileValidator.Validate(r4Patient, TwCoreProfiles.Patient);
```

第三方 package 實作新版 IImplementationGuidePackage，提供真實的 base version 與 resource 支援宣告；自訂 base validator 實作 contextual 介面。constructor injection 保留，不綁定特定 DI container，不自動安裝 IG。

## 5. Snapshot、CI 與 rollback

舊 `ApprovedPublicApi.txt` 與 V3–V7 API-preservation gates 描述舊 SDK。應保留原始證據，新的 snapshot 反映已確認的 breaking delta；不能直接放寬歷史比較，或刪掉負向案例以求綠燈。

新 D gates 要驗證 Models／Runtime／generator 未發生非預期 drift，並補上 SDK 無版本依賴、IG 可替換及 R4-only 部署。舊 runner 若保留為歷史回歸，只能配對它固定的 sources／snapshots／artifacts，明確區分目前 CI 的驗收角色。

D0 pin 與每階段結果保存為新證據，不覆寫 `docs/fhir_versions/baselines/` 或 `docs/gen/baselines/`。Rollback 以獨立 checkout 還原已驗收 revision，重新 restore／build／test／publish，整包替換部署；不混用前後 DLL，不 reset 或改寫 main。

本次完成條件是 D0–D5 的實際結果，而非文件全部建立、舊 V7 曾通過或單一平台能編譯。
