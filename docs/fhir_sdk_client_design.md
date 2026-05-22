# FHIR SDK Client 功能設計文件

## 1. Client Layer 的角色

FHIR SDK 的 Client Layer 負責和 FHIR Server 溝通。它不是單純包裝 `HttpClient`，而是 FHIR-aware REST client，負責把 SDK resource object 轉成 HTTP request，並把 HTTP response 轉回 SDK resource object。

核心流程：

```text
FHIR Resource Object
  -> Serializer
  -> FHIR JSON
  -> HTTP Request
  -> FHIR Server
  -> HTTP Response
  -> Parser
  -> FHIR Resource Object
```

Client Layer 是 orchestration layer。它應該整合 serialization、parser、HTTP request/response handling、authentication 和 search query building，但不應該把 FHIR model、primitive validation、business logic 或 database access 放進來。

---

## 2. 設計決策

MVP 決策：

- MVP REST 操作包含 `Read`、`Create`、`Update`、`Search`。
- MVP 不包含 `Delete`，放到 future enhancements。
- MVP simple authentication 只包含 `NoAuthProvider` 和 `BearerTokenAuthProvider`。
- `FhirSearchQuery` 只負責 query parameters，不負責 resource type。
- Search API 採用 `SearchAsync<Patient>(FhirSearchQuery query)` 風格，由 client 泛型決定 resource type。
- 專案初期可以維持單一 `.csproj`，但檔案與 namespace 先拆好，讓未來可以平順搬成 `MyFhirSdk.Client` project。

---

## 3. 建議檔案結構

目前可以先在主專案底下建立 `Client` 目錄。資料夾與 namespace 先照未來 project boundary 拆開。

```text
Client
|-- FhirClient.cs
|-- Abstractions
|   |-- IFhirClient.cs
|   |-- IFhirHttpSender.cs
|   |-- IFhirRequestBuilder.cs
|   `-- IFhirResponseHandler.cs
|-- Authentication
|   |-- IAuthProvider.cs
|   |-- NoAuthProvider.cs
|   `-- BearerTokenAuthProvider.cs
|-- Configuration
|   `-- FhirClientOptions.cs
|-- Exceptions
|   |-- FhirClientException.cs
|   |-- FhirHttpException.cs
|   `-- FhirInvalidResponseException.cs
|-- Http
|   |-- FhirHttpConstants.cs
|   |-- FhirHttpContent.cs
|   |-- FhirHttpHeaders.cs
|   `-- FhirHttpSender.cs
|-- Requests
|   |-- FhirRequestBuilder.cs
|   |-- FhirRequestUriBuilder.cs
|   `-- FhirResourceTypeResolver.cs
|-- Responses
|   `-- FhirResponseHandler.cs
`-- Search
    |-- FhirSearchQuery.cs
    |-- FhirSearchParameter.cs
    `-- FhirSearchQueryBuilder.cs
```

建議 namespace：

```text
MyFhirSdk.Client
MyFhirSdk.Client.Abstractions
MyFhirSdk.Client.Authentication
MyFhirSdk.Client.Configuration
MyFhirSdk.Client.Exceptions
MyFhirSdk.Client.Http
MyFhirSdk.Client.Requests
MyFhirSdk.Client.Responses
MyFhirSdk.Client.Search
```

---

## 4. FhirClient

`FhirClient` 是 SDK 使用者主要接觸的入口。

MVP 負責功能：

- Read resource
- Create resource
- Update resource
- Search resources
- 套用 simple authentication
- 串接 serializer、parser、request builder、HTTP sender、response handler

使用範例：

```csharp
Patient? patient = await client.ReadAsync<Patient>("123");

Patient created = await client.CreateAsync(patient);

Bundle result = await client.SearchAsync<Patient>(
    FhirSearchQuery.Create()
        .Where("name", "John"));
```

建議 API：

```csharp
public interface IFhirClient
{
    Task<TResource?> ReadAsync<TResource>(
        string id,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<TResource> CreateAsync<TResource>(
        TResource resource,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<TResource> UpdateAsync<TResource>(
        TResource resource,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<Bundle> SearchAsync<TResource>(
        string query,
        CancellationToken cancellationToken = default)
        where TResource : Resource;

    Task<Bundle> SearchAsync<TResource>(
        FhirSearchQuery query,
        CancellationToken cancellationToken = default)
        where TResource : Resource;
}
```

行為規則：

- `ReadAsync<Patient>("123")` 呼叫 `GET /Patient/123`。
- `CreateAsync(patient)` 呼叫 `POST /Patient`。
- `UpdateAsync(patient)` 呼叫 `PUT /Patient/{id}`。
- `SearchAsync<Patient>("name=John")` 呼叫 `GET /Patient?name=John`。
- `SearchAsync<Patient>(query)` 由 `Patient` 決定 path，由 `query` 決定 query string。
- `ReadAsync` 遇到 `404 Not Found` 回傳 `null`。
- 其他非成功 HTTP status code 丟出 client exception。
- 所有 async API 都要支援 `CancellationToken`。

---

## 5. Abstractions

Client Layer 的 interface 讓各功能可以替換、測試與注入。

建議包含：

```text
IFhirClient
IFhirHttpSender
IFhirRequestBuilder
IFhirResponseHandler
IAuthProvider
```

範例：

```csharp
public interface IFhirHttpSender
{
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
```

抽象層的目的：

- 方便 unit test。
- 可替換 HTTP 實作。
- 可搭配 dependency injection。
- 避免 `FhirClient` 直接依賴所有具體實作。

---

## 6. HTTP

HTTP 模組負責真正的 request 傳送、FHIR MIME type、content 包裝與 header 設定。

建議包含：

```text
FhirHttpSender
FhirHttpContent
FhirHttpHeaders
FhirHttpConstants
```

FHIR JSON MIME type：

```http
Content-Type: application/fhir+json
Accept: application/fhir+json
```

Create / Update request 建議加入：

```http
Prefer: return=representation
```

`FhirHttpSender` 範例：

```csharp
public sealed class FhirHttpSender : IFhirHttpSender
{
    private readonly HttpClient _httpClient;

    public FhirHttpSender(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        return _httpClient.SendAsync(request, cancellationToken);
    }
}
```

---

## 7. Requests

Requests 模組負責建立符合 FHIR REST API 規則的 `HttpRequestMessage`。

建議包含：

```text
FhirRequestBuilder
FhirRequestUriBuilder
FhirResourceTypeResolver
```

MVP REST 對應：

| 操作 | HTTP Method | URL |
|---|---|---|
| Read | GET | `/Patient/123` |
| Create | POST | `/Patient` |
| Update | PUT | `/Patient/123` |
| Search | GET | `/Patient?name=John` |

設計重點：

- Resource type 不應該用 `typeof(TResource).Name` 猜。
- Resource type 應該透過 `Resource.ResourceType` 或集中式 `FhirResourceTypeResolver` 取得。
- URI 組合應該集中在 `FhirRequestUriBuilder`。
- `UpdateAsync` 必須檢查 `resource.Id`，沒有 id 時丟出清楚的 exception。

`FhirRequestUriBuilder` 應處理：

- `BaseAddress` trailing slash。
- base path，例如 `https://server.example.org/fhir` 的 `/fhir` 不應被吃掉。
- resource id encoding。
- query string trim。
- query parameter encoding。
- relative URI 與 absolute base address 的組合。

Read request 範例：

```csharp
public HttpRequestMessage BuildReadRequest<TResource>(string id)
    where TResource : Resource
{
    var resourceType = _resourceTypeResolver.GetResourceType<TResource>();
    var uri = _uriBuilder.BuildResourceInstanceUri(resourceType, id);

    return new HttpRequestMessage(HttpMethod.Get, uri);
}
```

Create request 範例：

```csharp
public HttpRequestMessage BuildCreateRequest<TResource>(
    TResource resource,
    string json)
    where TResource : Resource
{
    var uri = _uriBuilder.BuildResourceTypeUri(resource.ResourceType);

    var request = new HttpRequestMessage(HttpMethod.Post, uri);
    request.Headers.Accept.Add(FhirHttpHeaders.FhirJson);
    request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
    request.Content = FhirHttpContent.CreateJson(json);

    return request;
}
```

---

## 8. Responses

Responses 模組負責處理 FHIR Server 回傳的 HTTP response。

建議包含：

```text
FhirResponseHandler
```

MVP 負責功能：

- 檢查 HTTP status code。
- 讀取 response body。
- 成功時呼叫 `IFhirParser`。
- `ReadAsync` 遇到 `404 Not Found` 回傳 `null`。
- 非成功 response 丟出 client exception。
- exception 盡量保存 status code、reason phrase、response body、HTTP method 與 request URI。

範例：

```csharp
public sealed class FhirResponseHandler : IFhirResponseHandler
{
    private readonly IFhirParser _parser;

    public FhirResponseHandler(IFhirParser parser)
    {
        _parser = parser;
    }

    public async Task<TResource> HandleRequiredResourceAsync<TResource>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
        where TResource : Resource
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw FhirClientException.FromResponse(response, json);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FhirInvalidResponseException("FHIR response body is empty.");
        }

        return _parser.Parse<TResource>(json);
    }
}
```

OperationOutcome structured mapping 不放在 MVP。第一版先保存 raw response body，等 `OperationOutcome` resource 納入 scope 後再擴充。

---

## 9. Search

Search 模組負責建立 FHIR search query parameters。

MVP 決策：

- `FhirSearchQuery` 只管 query parameters。
- Resource type 由 `SearchAsync<TResource>` 的泛型決定。
- MVP 支援 raw query string overload，方便快速呼叫。
- MVP 支援簡單 builder：`Where`、`Sort`、`Count`。
- `_include`、`_revinclude` 和 pagination helper 放到 future enhancements。
- Search response 回傳現有 `Bundle`。
- MVP 不新增 `SearchResult` 包裝型別。

建議包含：

```text
FhirSearchQuery
FhirSearchParameter
FhirSearchQueryBuilder
```

使用範例：

```csharp
var query = FhirSearchQuery
    .Create()
    .Where("name", "John")
    .Where("birthdate", "1990-01-01")
    .Count(20);

Bundle result = await client.SearchAsync<Patient>(query);
```

產生 URL：

```text
GET /Patient?name=John&birthdate=1990-01-01&_count=20
```

也支援 raw query string：

```csharp
Bundle result = await client.SearchAsync<Patient>("name=John");
```

MVP 需要支援的 query 範例：

```text
Patient?name=John
Patient?identifier=123
Claim?patient=Patient/123
Patient?_count=20
Patient?_sort=birthdate
```

---

## 10. Authentication

Authentication 模組負責在 request 送出前套用認證資訊。

MVP simple authentication 只包含：

```text
IAuthProvider
NoAuthProvider
BearerTokenAuthProvider
```

`ApiKeyAuthProvider`、SMART on FHIR、OAuth token refresh 都放到 future enhancements。

Interface 範例：

```csharp
public interface IAuthProvider
{
    Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
```

`NoAuthProvider` 範例：

```csharp
public sealed class NoAuthProvider : IAuthProvider
{
    public Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

`BearerTokenAuthProvider` 範例：

```csharp
public sealed class BearerTokenAuthProvider : IAuthProvider
{
    private readonly string _token;

    public BearerTokenAuthProvider(string token)
    {
        _token = token;
    }

    public Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);

        return Task.CompletedTask;
    }
}
```

---

## 11. Configuration

Configuration 模組集中管理 client 設定。

建議包含：

```text
FhirClientOptions
FhirClientFactory
```

Options 範例：

```csharp
public sealed class FhirClientOptions
{
    public required Uri BaseAddress { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public bool ValidateBeforeSend { get; init; } = false;
}
```

建議 constructor：

```csharp
public sealed class FhirClient : IFhirClient
{
    public FhirClient(
        HttpClient httpClient,
        IFhirSerializer serializer,
        IFhirParser parser,
        FhirClientOptions options,
        IAuthProvider? authProvider = null)
    {
    }
}
```

使用範例：

```csharp
var options = new FhirClientOptions
{
    BaseAddress = new Uri("https://server.example.org/fhir"),
    Timeout = TimeSpan.FromSeconds(30)
};

var client = new FhirClient(
    httpClient,
    serializer,
    parser,
    options,
    new BearerTokenAuthProvider(token));
```

---

## 12. Exceptions

Exceptions 模組定義 Client Layer 專用錯誤型別。

MVP 建議包含：

```text
FhirClientException
FhirHttpException
FhirInvalidResponseException
```

使用情境：

| Exception | 使用時機 |
|---|---|
| `FhirClientException` | Client 通用錯誤，建議繼承 `FhirSdkException` |
| `FhirHttpException` | HTTP status code 失敗 |
| `FhirInvalidResponseException` | Response body 為空或無法 parse |

`FhirHttpException` 建議保存：

- `HttpStatusCode StatusCode`
- `string? ReasonPhrase`
- `string? ResponseBody`
- `HttpMethod? Method`
- `Uri? RequestUri`

`FhirOperationOutcomeException` 先放到 future，等 `OperationOutcome` resource 納入 scope 後再做。

---

## 13. CreateAsync 完整流程範例

使用者程式碼：

```csharp
var patient = new Patient
{
    Name =
    [
        new HumanName
        {
            Family = new FhirString("Chen"),
            Given = [new FhirString("Amy")]
        }
    ]
};

Patient created = await client.CreateAsync(patient);
```

內部流程：

```text
FhirClient.CreateAsync(patient)
  -> Serializer.Serialize(patient)
  -> FhirRequestBuilder.BuildCreateRequest(patient, json)
  -> Authentication.ApplyAsync(request)
  -> FhirHttpSender.SendAsync(request)
  -> FhirResponseHandler.HandleRequiredResourceAsync<Patient>(response)
  -> Parser.Parse<Patient>(responseJson)
  -> return Patient
```

HTTP request：

```http
POST /Patient
Content-Type: application/fhir+json
Accept: application/fhir+json
Prefer: return=representation
```

Request body：

```json
{
  "resourceType": "Patient",
  "name": [
    {
      "family": "Chen",
      "given": ["Amy"]
    }
  ]
}
```

---

## 14. MVP 最少應該完成的功能

必做檔案：

```text
FhirClient.cs
Abstractions/IFhirClient.cs
Authentication/IAuthProvider.cs
Authentication/NoAuthProvider.cs
Authentication/BearerTokenAuthProvider.cs
Requests/FhirRequestBuilder.cs
Requests/FhirRequestUriBuilder.cs
Requests/FhirResourceTypeResolver.cs
Http/FhirHttpSender.cs
Responses/FhirResponseHandler.cs
Search/FhirSearchQuery.cs
Configuration/FhirClientOptions.cs
Exceptions/FhirClientException.cs
```

REST 操作：

```text
Read
Create
Update
Search
```

HTTP 規則：

```text
Content-Type: application/fhir+json
Accept: application/fhir+json
Prefer: return=representation for Create and Update
BaseAddress handling
Status code handling
Response parsing
```

Authentication：

```text
NoAuthProvider
BearerTokenAuthProvider
```

Search：

```text
Raw query string
FhirSearchQuery parameter builder
SearchAsync<TResource>(FhirSearchQuery query)
```

---

## 15. Client Layer 測試建議

Unit test 可以使用 mocked `HttpMessageHandler` 或 mock `IFhirHttpSender`。

測試重點：

```text
Read URL test
Create URL/body/header test
Update URL/body/header test
Search raw query URL test
Search FhirSearchQuery encoding test
BaseAddress trailing slash test
Bearer token header test
NoAuthProvider test
Read 404 returns null test
Non-success response preserves status/body test
Response parser test
Empty response body test
```

Integration test 可使用公開測試 FHIR Server，例如 HAPI FHIR Test Server。

測試重點：

```text
Create Patient
Read Patient
Search Patient
Update Patient
Bearer token request header
```

測試檔案結構:
Tests
`-- Client
    |-- MyFhirSdk.Client.Tests.csproj
    |-- GlobalUsings.cs
    |-- Program.cs
    |-- Fakes
    |   |-- FakeFhirParser.cs
    |   |-- FakeFhirSerializer.cs
    |   `-- FakeFhirHttpSender.cs
    |-- Authentication
    |   |-- BearerTokenAuthProviderTests.cs
    |   `-- NoAuthProviderTests.cs
    |-- Requests
    |   |-- FhirRequestBuilderTests.cs
    |   |-- FhirRequestUriBuilderTests.cs
    |   `-- FhirResourceTypeResolverTests.cs
    |-- Responses
    |   `-- FhirResponseHandlerTests.cs
    |-- Search
    |   |-- FhirSearchParameterTests.cs
    |   `-- FhirSearchQueryTests.cs
    `-- FhirClientTests.cs

---

## 16. Client Layer 和其他 Layer 的關係

```text
Client Layer
  |-- 使用 Serialization Layer：Object -> JSON
  |-- 使用 Parser Layer：JSON -> Object
  |-- 使用 Authentication：套用認證
  |-- 使用 HTTP：發送 request
  `-- 使用 Responses：處理 response
```

Client Layer 不負責：

```text
FHIR model 定義
FHIR primitive validation
FHIR JSON serialization 細節
FHIR JSON parser 細節
Business logic
Database access
```

---

## 17. Future Enhancements

MVP 之後可以再加入：

```text
Delete
Patch
History
Conditional Create
Conditional Update
Batch
Transaction
Pagination helper
CapabilityStatement
ETag / If-Match
OperationOutcome structured mapping
FhirOperationOutcomeException
Retry policy
Logging
ApiKeyAuthProvider
SMART on FHIR Authentication
OAuth token refresh
_include / _revinclude helpers
```

---

## 18. 一句話總結

FHIR SDK Client Layer 的核心是：

```text
FHIR Object
  <-> FHIR JSON
  <-> FHIR REST API
```

它要負責建立 FHIR HTTP request、發送 request、處理 response，並整合 serializer、parser、simple authentication 和 search query，讓 SDK 使用者可以用 C# resource object 操作 FHIR Server。
