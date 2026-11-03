# V6：R4／R5 Client integration

V5 已提交於 `9aee18656326a175b92d7b9fded1379188d64a4e`，使用者確認 push CI 通過。
V6 在同一 branch 加入明確版本的 typed Client、共用 HTTP executor／response processor、受控 HTTP tests 與外部 smoke runner；本機結果見 `summary.json`，新增 Windows／Ubuntu CI 待提交後執行。

本機 G6 **Passed**：完整 solution 1171 項，1170 通過、0 失敗、1 項既有外部 Client smoke 因未設定 endpoint 略過；13 項 Python integrity tests 通過。
Checkout 外重新編譯的 81 項 V5 integration cases 與 92 項 V6 HTTP cases 全部通過；rollback harness 的 16 項檢查通過。
R4／R5 新外部 smoke 各自記錄 `Skipped`，所以 `externalServerAcceptance=Pending`；不包含在上述 solution TRX 計數中。
並行案例透過同步點保證 R4／R5 HTTP 請求重疊，而不是只依賴可同步完成的 fake response。
原始 logs／TRX 在忽略目錄 `artifacts/fhir-versions/v6/review-fixed-verified`，本目錄保留可攜摘要、必要 cases、生成 hashes 與 source fingerprint。先前 `review-fixed` 的乾淨目錄 NuGet restore 因沙箱網路限制中止，不作為最終通過證據。

## API 與依賴

SDK 新增 `FhirClient<TBundle, TOperationOutcome>` 與 `IFhirClient<TBundle, TOperationOutcome>`。
Constructor 驗證兩個型別都是指定 context 中註冊的 Bundle／OperationOutcome，並從該 context 組裝 Parser／Serializer／Validator。
Context 是唯讀且不可變的；每個 instance 固定版本，不使用全域版本開關。

SDK 不新增對 R4 Models 的反向依賴。Consumer 明確引用 `MyFhirSdk.csproj` 與 `Models/R4/MyFhirSdk.R4.Models.csproj`：

```csharp
using MyFhirSdk.Client;
using MyFhirSdk.Client.Configuration;
using MyFhirSdk.Client.Search;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using R4Bundle = MyFhirSdk.R4.Resources.Bundle;
using R4Outcome = MyFhirSdk.R4.Resources.OperationOutcome;
using R4Patient = MyFhirSdk.R4.Resources.Patient;

var r4 = new FhirModelContext(
    R4ModelProvider.Metadata, R4ModelProvider.Primitives, R4ModelProvider.Validation);
using var http = new HttpClient();
var client = new FhirClient<R4Bundle, R4Outcome>(http, r4,
    new FhirClientOptions {
        BaseAddress = new Uri("https://server.example.org/fhir"),
        ValidateBeforeSend = true
    });
R4Patient? patient = await client.ReadAsync<R4Patient>("123");
R4Bundle results = await client.SearchAsync<R4Patient>(
    FhirSearchQuery.Create().Where("name", "Ada"));
R4Outcome? deleted = await client.DeleteAsync<R4Patient>("123");
```

R5 可用相同 API 搭配 `MyFhirSdk.Resources.Bundle`／`OperationOutcome` 與 `R5ModelContext.Default`。
Typed API 不接受獨立 parser／serializer／validator injection，避免組出不同版本的 engines。
JSON 通常沒有版本資訊，因此不承諾能從同名 Patient JSON 自動偵測錯誤 server 版本；外部 smoke 另驗證 CapabilityStatement。

## HTTP 行為與相容性

- Read／Create／Update／Search 使用註冊表解析確切 CLR resource type；不再用類別名稱推測。
- Search 支援 raw／structured query，回傳版本內的 typed Bundle；entries 使用同一 context 的 factories。
- Create／Update 選擇性驗證，使用相同版本的 lexical／required／choice 規則；即使停用驗證，foreign root／contained／Extension value 仍在 HTTP 前拒絕。
- Read 404 回傳 null；Delete 204／空成功 body 回傳 null，非空成功 body 須為該版本 OperationOutcome。Delete 404 仍是 HTTP 錯誤。
- 共用 instance URL builder 拒絕 ID `.`／`..`，避免 URI 正規化使 Read／Update／Delete 指向上層端點；不受 ValidateBeforeSend 設定影響，其他含點 ID 保留原有行為。
- 新 typed Client 的錯誤為 `FhirHttpException<TOperationOutcome>`，保留 HTTP status、method、URI、raw body 與可解析的 typed `OperationOutcome`；錯誤 body 無法解析時 outcome 為 null，仍保留 HTTP 錯誤。
- 成功 response 空白／malformed／錯誤 resource type 時拋出 `FhirInvalidResponseException`；取消不包成解析例外。
- 共用 executor 處理 auth、序列化／驗證、request／response disposal 與 cancellation。HttpClient 由呼叫端擁有；建構 Client 時依現有 options 設定 Timeout，應在首次使用該 HttpClient 前建構。

舊 `FhirClient`、`IFhirClient` constructors／Search 的 R5 Bundle 回傳型別、replaceable collaborators 及原本非 generic `FhirHttpException` 保留。
舊入口的 resolver 預設 R5，foreign／未註冊型別會在送出前拒絕。原先以舊 constructor 手動注入 R4 parser 的用法，須遷移到明確的 typed Client。
新 API 要捕捉 `FhirHttpException<R4Outcome>` 或共用 `FhirClientException`；舊非 generic HTTP exception 不會直接捕捉新的 typed wrapper，可透過其 `HttpError` 取得原有 HTTP 資訊。
自訂 sender 若要保留 request 作觀察，須在送出期間擷取內容；Client 完成 response 處理後會 dispose request。

Client API snapshot 位於 `Tests/FhirVersions/V6/ApprovedClientApi.txt`。92 項受控 HTTP cases 包含 R4／R5 CRUD、Search、headers／auth、query encoding、OperationOutcome、錯誤 body、取消與 disposal、停用驗證時的 ownership、R4 lexical 差異、並行雙版本 client，以及開啟／關閉驗證時拒絕 dot-segment ID 的 24 項回歸案例。既有 Client 測試另加入 5 項 URL builder 邊界案例。
Tool 維持 `1.3.1`；Runtime、ModelContracts、ModelSupport、全部 677 個 R4／852 個 R5 generated C#、兩版本 manifests 與模型 API 保持 V5 基準。

## 重現 G6

需 pinned .NET SDK、Python 3.13、PowerShell 7.2+ 與含 V2／V5 pins 的 Git history：

```powershell
dotnet restore MyFhirSdk.sln
python eng/Test-FhirV6Client.py --output artifacts/fhir-versions/v6/run
```

G6 先執行 G5／G4／G3／G2 的完整 solution regression、Python integrity、生成與 API guards。
目前 solution 的測試 assembly 清單為 9 個；歷史 rollback 仍為 7 個。現行 G5 runner 與 K3／K7／CI 的清單同步，V0–V5 已提交 evidence 不改寫。
接著在另一個 checkout 外的乾淨目錄，以 installed tool 當次輸出編譯 SDK、R4／R5 Models、V6 tests 及 smoke consumer。
驗收核對實際 assembly、必要 cases、TRX counters、API／來源 fingerprint、載入 implementation hashes 與生成輸入不變。NuGet dependency restore 可使用環境快取；tool 安裝依 G4 的隔離 feed／cache。

## 外部 server smoke

每個版本明確設定獨立 endpoint，不能以 R5 endpoint 代替 R4：

| 版本 | Base URL | 選用 Bearer token |
| --- | --- | --- |
| R4 | `MYFHIRSDK_R4_INTEGRATION_BASE_URL` | `MYFHIRSDK_R4_INTEGRATION_BEARER_TOKEN` |
| R5 | `MYFHIRSDK_R5_INTEGRATION_BASE_URL` | `MYFHIRSDK_R5_INTEGRATION_BEARER_TOKEN` |

Smoke 先 GET metadata，確認 server 是固定的 `4.0.1`／`5.0.0`，記錄 endpoint、版本與 software identity，再對帶有隨機 marker 的測試 Patient 執行 create／read／update／search，最後 delete 清理。
G6 runner 自動執行 smoke consumer；沒有 endpoint 時輸出 `Skipped`，不假裝外部通過。可設定相同環境變數後重跑 G6。
CI 預設未配置外部服務；controlled HTTP tests 不代替外部 smoke。`externalServerAcceptance=Pending` 時，只代表 Client 實作與受控 HTTP 驗收通過，不能宣稱 R4 Client 外部整合驗收通過。
正式部署、缺少 DLL、跨平台 delivery／rollback handoff 仍屬 V7。
