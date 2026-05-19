# FHIR SDK Client 功能設計文件

## 1. Client Layer 的角色

FHIR SDK 的 Client Layer 是用來和 FHIR Server 溝通的模組。

它的核心責任是：

```text
FHIR Resource Object
    ↓
Serializer
    ↓
FHIR JSON
    ↓
HTTP Request
    ↓
FHIR Server
    ↓
HTTP Response
    ↓
Parser
    ↓
FHIR Resource Object
```

也就是說，Client Layer 不是單純包裝 `HttpClient`，而是 FHIR-aware REST Client。

---

## 2. 建議 Project Structure

```text
MyFhirSdk.Client
├── FhirClient.cs
├── Abstractions
├── Http
├── Requests
├── Responses
├── Search
├── Authentication
├── Configuration
└── Exceptions
```

---

## 3. FhirClient.cs

### 目的

`FhirClient` 是 SDK 使用者主要接觸的入口。

### 負責功能

- Read Resource
- Create Resource
- Update Resource
- Delete Resource
- Search Resource
- 串接 Serializer / Parser / HTTP Sender / Response Handler

### 使用範例

```csharp
var patient = await client.ReadAsync<Patient>("123");

var created = await client.CreateAsync(patient);

var result = await client.SearchAsync<Patient>("name=John");
```

### 建議 API

```csharp
public interface IFhirClient
{
    Task<TResource> ReadAsync<TResource>(string id)
        where TResource : Resource;

    Task<TResource> CreateAsync<TResource>(TResource resource)
        where TResource : Resource;

    Task<TResource> UpdateAsync<TResource>(TResource resource)
        where TResource : Resource;

    Task DeleteAsync<TResource>(string id)
        where TResource : Resource;

    Task<Bundle> SearchAsync<TResource>(string query)
        where TResource : Resource;
}
```

---

## 4. Abstractions

### 目的

放置 Client Layer 的 interface，讓各功能可以替換、測試、注入。

### 建議包含

```text
IFhirClient
IFhirHttpSender
IFhirRequestBuilder
IFhirResponseHandler
IAuthProvider
```

### 範例

```csharp
public interface IFhirHttpSender
{
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
```

### 適合原因

- 方便 Unit Test
- 可替換 HTTP 實作
- 可搭配 Dependency Injection
- 避免 FhirClient 直接依賴具體實作

---

## 5. Http

### 目的

處理真正的 HTTP 傳送與 HTTP 內容包裝。

### 建議包含

```text
FhirHttpSender
FhirHttpContent
FhirHttpHeaders
```

### 功能

- 發送 `HttpRequestMessage`
- 設定 FHIR MIME type
- 管理 HTTP header
- 包裝 request body

### FHIR JSON MIME Type

```http
Content-Type: application/fhir+json
Accept: application/fhir+json
```

### 範例

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

## 6. Requests

### 目的

負責建立符合 FHIR REST API 規則的 HTTP Request。

### 建議包含

```text
FhirRequestBuilder
FhirRequestMethod
FhirRequestUriBuilder
```

### 負責功能

- 建立 Read request
- 建立 Create request
- 建立 Update request
- 建立 Delete request
- 建立 Search request
- 設定 HTTP method
- 設定 URL
- 加入 FHIR JSON body

### FHIR REST 對應

| 操作 | HTTP Method | URL |
|---|---|---|
| Read | GET | `/Patient/123` |
| Create | POST | `/Patient` |
| Update | PUT | `/Patient/123` |
| Delete | DELETE | `/Patient/123` |
| Search | GET | `/Patient?name=John` |

### Read Request 範例

```csharp
public HttpRequestMessage BuildReadRequest<TResource>(string id)
    where TResource : Resource
{
    var resourceName = typeof(TResource).Name;

    return new HttpRequestMessage(
        HttpMethod.Get,
        $"{resourceName}/{id}");
}
```

### Create Request 範例

```csharp
public HttpRequestMessage BuildCreateRequest<TResource>(
    TResource resource,
    string json)
    where TResource : Resource
{
    var resourceName = typeof(TResource).Name;

    var request = new HttpRequestMessage(
        HttpMethod.Post,
        resourceName);

    request.Content = new StringContent(
        json,
        Encoding.UTF8,
        "application/fhir+json");

    return request;
}
```

---

## 7. Responses

### 目的

負責處理 FHIR Server 回傳的 HTTP Response。

### 建議包含

```text
FhirResponseHandler
FhirOperationResult
OperationOutcomeMapper
```

### 負責功能

- 檢查 HTTP status code
- 讀取 response body
- 成功時呼叫 Parser
- 失敗時處理 OperationOutcome
- 轉成 SDK exception 或 result object

### 範例

```csharp
public sealed class FhirResponseHandler : IFhirResponseHandler
{
    private readonly IFhirParser _parser;

    public FhirResponseHandler(IFhirParser parser)
    {
        _parser = parser;
    }

    public async Task<TResource> HandleAsync<TResource>(
        HttpResponseMessage response)
        where TResource : Resource
    {
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new FhirClientException(json);
        }

        return _parser.Parse<TResource>(json);
    }
}
```

### 後續可擴充

- Parse `OperationOutcome`
- 回傳 `FhirOperationResult<T>`
- 支援 ETag / VersionId
- 支援 response headers

---

## 8. Search

### 目的

負責建立 FHIR Search Query。

### 建議包含

```text
SearchQueryBuilder
SearchParameter
SearchResult
```

### 基本功能

- 建立 query string
- 支援多個 search parameters
- 支援 `_include`
- 支援 `_revinclude`
- 支援 `_sort`
- 支援 `_count`
- 支援 pagination

### 使用範例

```csharp
var query = SearchQuery
    .For<Patient>()
    .Where("name", "John")
    .Where("birthdate", "1990-01-01")
    .Build();
```

### 產生結果

```text
Patient?name=John&birthdate=1990-01-01
```

### MVP 可先支援

```text
Patient?name=John
Patient?identifier=123
Claim?patient=Patient/123
```

---

## 9. Authentication

### 目的

負責處理 request 認證。

### 建議包含

```text
IAuthProvider
NoAuthProvider
BearerTokenAuthProvider
ApiKeyAuthProvider
```

### Interface 範例

```csharp
public interface IAuthProvider
{
    Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
```

### Bearer Token 範例

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

### MVP 建議

第一版可以先提供：

```text
NoAuthProvider
BearerTokenAuthProvider
```

SMART on FHIR / OAuth 可以放到後續版本。

---

## 10. Configuration

### 目的

集中管理 Client 設定。

### 建議包含

```text
FhirClientOptions
FhirClientFactory
```

### Options 範例

```csharp
public sealed class FhirClientOptions
{
    public string BaseUrl { get; set; } = "";

    public TimeSpan Timeout { get; set; } =
        TimeSpan.FromSeconds(30);

    public bool ValidateBeforeSend { get; set; } = false;

    public bool ThrowOnOperationOutcome { get; set; } = true;
}
```

### 使用範例

```csharp
var options = new FhirClientOptions
{
    BaseUrl = "https://server.example.org/fhir",
    Timeout = TimeSpan.FromSeconds(30),
    ValidateBeforeSend = false
};

var client = new FhirClient(options);
```

---

## 11. Exceptions

### 目的

定義 Client Layer 專用錯誤型別。

### 建議包含

```text
FhirClientException
FhirHttpException
FhirOperationOutcomeException
FhirInvalidResponseException
```

### 使用情境

| Exception | 使用時機 |
|---|---|
| FhirClientException | Client 通用錯誤 |
| FhirHttpException | HTTP status code 失敗 |
| FhirOperationOutcomeException | Server 回傳 OperationOutcome |
| FhirInvalidResponseException | Response body 無法 parse |

### 範例

```csharp
public class FhirHttpException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public FhirHttpException(
        HttpStatusCode statusCode,
        string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
```

---

## 12. CreateAsync 完整流程範例

### 使用者程式碼

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

var created = await client.CreateAsync(patient);
```

### 內部流程

```text
FhirClient.CreateAsync(patient)
  ↓
Serializer.Serialize(patient)
  ↓
FhirRequestBuilder.BuildCreateRequest(patient, json)
  ↓
Authentication.ApplyAsync(request)
  ↓
FhirHttpSender.SendAsync(request)
  ↓
FhirResponseHandler.HandleAsync<Patient>(response)
  ↓
Parser.Parse<Patient>(responseJson)
  ↓
return Patient
```

### HTTP Request

```http
POST /Patient
Content-Type: application/fhir+json
Accept: application/fhir+json
```

### Request Body

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

## 13. MVP 最少應該完成的功能

### 必做

```text
FhirClient.cs
Requests/FhirRequestBuilder.cs
Http/FhirHttpSender.cs
Responses/FhirResponseHandler.cs
Configuration/FhirClientOptions.cs
Exceptions/FhirClientException.cs
```

### REST 操作

```text
Read
Create
Update
Delete
Search
```

### HTTP 規則

```text
Content-Type: application/fhir+json
Accept: application/fhir+json
BaseUrl handling
Status code handling
Response parsing
```

### 測試

```text
Mock HTTP request test
Create request body test
Read URL test
Search URL test
Response parser test
Error response test
```

---

## 14. Client Layer 測試建議

### Unit Test

使用 mock `HttpMessageHandler` 或 mock `IFhirHttpSender`。

測試重點：

```text
HTTP Method 是否正確
URL 是否正確
Header 是否正確
Body 是否為合法 FHIR JSON
Response 是否正確 parse
Error 是否正確處理
```

### Integration Test

使用公開測試 FHIR Server，例如 HAPI FHIR Test Server。

測試重點：

```text
Create Patient
Read Patient
Search Patient
Update Patient
Delete Patient
```

---

## 15. Client Layer 和其他 Layer 的關係

```text
Client Layer
  ├── 使用 Serialization Layer：Object → JSON
  ├── 使用 Parser Layer：JSON → Object
  ├── 使用 Authentication：套用認證
  ├── 使用 Http：發送 request
  └── 使用 Responses：處理 response
```

Client Layer 是 orchestration layer。

它本身不應該負責：

```text
FHIR model 定義
FHIR primitive validation
FHIR JSON serialization 細節
FHIR JSON parser 細節
Business logic
Database access
```

---

## 16. 後續可擴充功能

MVP 之後可以再加入：

```text
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
Retry policy
Logging
SMART on FHIR Authentication
```

---

## 17. 一句話總結

FHIR SDK Client Layer 的核心是：

```text
FHIR Object
    ↔
FHIR JSON
    ↔
FHIR REST API
```

它要負責建立 FHIR HTTP Request、發送 Request、處理 Response，並整合 Serializer 與 Parser，讓 SDK 使用者可以用 C# Resource Object 操作 FHIR Server。
