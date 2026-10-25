# D3：移除 R5 defaults 與舊 Client

D2 已由使用者確認 push 並通過 CI，來源為 `c2e9098225718c79daf1aed4289d4f806ebab570`。D3 讓 SDK 只依賴 Runtime、ModelContracts、ModelSupport，版本模型由呼叫端明確選擇。這是 SDK 的 breaking change，使用端必須重建。

## API 與實作

- 刪除 Parser、Serializer、FhirValidator、FhirResourceTypeResolver 的公開無參數建構子。
- 刪除公開 R5ModelContext，以及內部 R5ModelMetadataProvider adapter、PrimitiveRegistry.Default 與隱含 R5 fallback。
- 刪除非泛型 FhirClient、IFhirClient、FhirResponseHandler、IFhirResponseHandler。保留 typed Client、共用 HTTP executor／response processor、非泛型與泛型 FhirHttpException 的診斷資訊。
- ProfileValidator 只接受 IContextualFhirValidator，移除 D1 的兩個 obsolete 過渡 overload 及其舊組合分支。版本、ownership、支援型別與 profile 註冊檢查皆走同一條 contextual 路徑。
- SDK 移除 R5 Models ProjectReference，沒有新增 type forwarders、相容組件或反射載入。

明列差異保存在 `baselines/d3/api-delta.json`；五個公開型別、四個無參數建構子、兩個 legacy ProfileValidator 建構子及退場型別的成員被移除，沒有新增公開 SDK API，TW Core API 不變。D3 runner 從 D2 證據計算允許的精確結果，再與實際 DLL API 比對；不接受任意 snapshot 更新。

```csharp
var context = new FhirModelContext(
    R5ModelProvider.Metadata,
    R5ModelProvider.Primitives,
    R5ModelProvider.Validation);
var parser = new FhirJsonParser(context);
var serializer = new FhirJsonSerializer(context);
var validator = new FhirValidator(context);
var client = new FhirClient<Bundle, OperationOutcome>(httpClient, context, options);
```

R5ModelProvider 由 R5 Models 組件提供。R4 改用 R4ModelProvider 及 R4 Bundle／OperationOutcome；TW Core 另引用獨立 IG 專案，使用 R4 contextual validator。

## 呼叫端與測試遷移

目前九個測試專案直接引用所需的 Models。共用的 `Tests/Shared/TestContexts.cs` 明確組合 R5 provider，僅用於測試；產品中沒有 default context。既有 parser／serializer／validator、生成模型、typed HTTP、profile 與 primitive 行為測試保留。

原 Client 假物件測試改走 typed Client 的內部注入接點及共用 response processor。SDK 的 friend assemblies 明列 Architecture.Tests、Client.Tests；production CodeGen 不獲得 internal 存取。保留的內部 parser／serializer／validator 接點需要明確 primitives／rules，供實驗與動態編譯測試使用，不會補用 R5。

V6 smoke、V7 R4／R5／Dual samples、目前 KernelMigration Consumer、RebuiltLibrary 與 RebuiltConsumer 同步遷移。歷史 BaselineConsumer、Runtime／Models／generator 產品來源及既有 baseline 證據不變。K0／K3 的目前 consumer 工具改用實際 Models 輸出位置與明確 Contracts／Support 引用，SDK graph allowlist 改成三個中立專案。

## 驗收入口

```powershell
dotnet restore MyFhirSdk.sln -p:NuGetAudit=false
python -m unittest discover -s Tests/SdkDecoupling -p "test_*.py" -v
python eng/Test-SdkDecouplingD3.py --output artifacts/sdk-decoupling/d3/local
```

需要 pinned .NET SDK 與 PowerShell 7.2+；可用 `MYFHIRSDK_TEST_PWSH` 指定 pwsh。輸出目錄必須尚未存在。`--record-api` 僅供首次建立精確允許的 API 證據，已有檔案會拒絕覆寫。

D3 gate 驗證歷史 D0／D1／D2 證據未改、穩定來源未變、Runtime compiler reference hash、SDK／IG evaluated graph、實際 DLL API、fresh K3 harness／完整回歸、67 個必要案例與四種乾淨部署。來源 fingerprint 在開始及結束必須相同。完整結果在新輸出的 summary.json、TRX、project-graph.json 與 deployment/evidence.json，不混用舊 TRX。

`Test-SdkDecouplingDeployment.py` 在 repository 外建立獨立 temporary projects，只用指定 DLL，四種情境均使用同一份 SDK binary。檢查 publish DLL 集合、deps.json、實際 assembly references／載入路徑、各版本 round trip／validation／受控 typed HTTP；R4 + TW Core 另驗證正負案例與 package 注入替換。逐一移除每個必要 DLL，要求非零退出及缺件診斷；Windows 子程序關閉錯誤對話框並設 timeout。暫存工作目錄於結束時清除，log、hash inventories 與診斷保存於 artifacts。

CI 新增目前 D3 Windows／Ubuntu matrix，D2 gate 固定 checkout 已接受的 D2 revision。D0／D1 與 V7 歷史入口仍使用各自的固定來源。D4 的正式 delivery exporter／模板與更完整交付入口遷移仍待後續實作；D3 的部署 probe 不代表正式交付包已更新。

未設定 endpoint 的外部 server smoke 仍依既有規則略過，受控 HTTP 與獨立部署不能代替實際 server 驗收。本次不執行外部 FHIR 寫入、不發布 NuGet、不提交或 push。
