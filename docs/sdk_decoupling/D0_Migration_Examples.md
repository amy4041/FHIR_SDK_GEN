# D0 使用端遷移範例

以下明確 context／typed Client 已存在，可在產品 API 尚未刪除時先遷移。範例使用 synthetic Patient，只展示本機 parse／serialize／validate；沒有 server 操作。

```csharp
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

// 取代 R5ModelContext.Default 與三個無參數 engines。
var context = new FhirModelContext(
    R5ModelProvider.Metadata,
    R5ModelProvider.Primitives,
    R5ModelProvider.Validation);
var parser = new FhirJsonParser(context);
var serializer = new FhirJsonSerializer(context);
var validator = new FhirValidator(context);
var patient = parser.Parse<MyFhirSdk.Resources.Patient>(
    """{"resourceType":"Patient","id":"synthetic-d0"}""");
var json = serializer.Serialize(patient);
var issues = validator.Validate(patient);
```

R4 換成 `MyFhirSdk.ModelMetadata.R4.R4ModelProvider` 與 `MyFhirSdk.R4.Resources.Patient`，每組 engines 共用自己的 context。應用程式的版本 namespace 引用保留於應用程式，不搬到共用 SDK。

舊 Client 的組合改為明確 context 與版本型別；`httpClient`、`options` 由應用程式既有設定提供，這段建立 Client 不會送出請求：

```csharp
var client = new MyFhirSdk.Client.FhirClient<
    MyFhirSdk.Resources.Bundle,
    MyFhirSdk.Resources.OperationOutcome>(httpClient, context, options);
```

R4 換成 `MyFhirSdk.R4.Resources.Bundle`／`OperationOutcome`，傳入 R4 context。自訂 request pipeline 使用 `new FhirResourceTypeResolver(context)`；自訂 response pipeline 遷移到 typed Client 的中立 response processor，不能保留固定 R5 Bundle 的舊 handler。

`SDK_DEMO` 目前只需將 R5 分支的 `R5ModelContext.Default` 換成上述 provider 組合；既有 R4 context 與 typed Client 已是明確版本入口。D3/D4 完成後，從新 delivery bundle 更新所用版本 Models 與 SDK／Runtime／Contracts／Support，重建應用程式與相依 library。R4-only 不應攜帶 R5 Models；optional TW Core 再加獨立 IG DLL。這些乾淨部署要求由 D3–D5 驗證，D0 尚未成立。

TW Core 新專案與 contextual IG framework 尚未建立；預期語法見 [Acceptance_and_Migration.md](Acceptance_and_Migration.md)，勿將該目標範例當成 D0 可編譯 API。
