# FHIR SDK MVP - Product Requirement Document (PRD)

## 1. Product Overview

### Product Name

MyFhirSdk

---

### Product Vision

提供一個輕量化、易擴充、符合 FHIR R5 的 .NET SDK，
讓醫療系統能快速進行：

- FHIR Resource 建立
- FHIR JSON 序列化/反序列化
- FHIR REST API 整合
- 基本 Validation
- 醫療申報資料交換

本 SDK 將以最小可行性產品（MVP）為目標，
優先聚焦於醫療申報與醫院資訊系統整合場景。

---

## 2. Problem Statement

目前醫療系統導入 FHIR 時，
開發者通常需要自行處理：

- FHIR JSON 格式
- Resource Mapping
- Datatype 處理
- REST API 呼叫
- Validation
- Bundle Parsing

導致：

- 重複開發
- 規格不一致
- 維護困難
- 新人學習成本高
- HIS/FHIR 整合效率低

因此需要一套：

- 可維護
- 可擴充
- 型別安全
- 符合 FHIR R5

的 SDK 作為基礎框架。

---

# 3. Product Goals

## MVP Goals

第一版 SDK 需要能夠：

1. 建立基本 FHIR Resource
2. 產生合法 FHIR JSON
3. 解析 FHIR JSON
4. 與 FHIR Server REST API 互動
5. 支援醫療申報相關 Resource
6. 提供基本 Validation

---

# 4. Scope

## 4.1 In Scope

---

### FHIR Version

- FHIR R5 only

---

### Primitive Types

支援以下 Primitive Types：

- boolean
- string
- uri
- canonical
- code
- id
- integer
- decimal
- date
- dateTime
- instant

---

### General-purpose Datatypes

支援以下 Datatypes：

- Identifier
- HumanName
- Address
- ContactPoint
- Coding
- CodeableConcept
- Quantity
- Period
- Reference

---

### Resources

MVP Resource 範圍：

- Patient
- Organization
- Practitioner
- Encounter
- Coverage
- Claim
- Bundle

---

### Serialization

支援：

- FHIR JSON Serialization
- FHIR JSON Deserialization

不支援：

- XML

---

### REST Client

支援：

- Read
- Create
- Update
- Search

---

### Validation

支援：

- Required field validation
- Primitive format validation
- Basic cardinality validation

---

## 4.2 Out of Scope

第一版不包含：

- FHIR R5
- XML Serialization
- FHIRPath
- Profile Validation
- StructureDefinition Validation
- Terminology Service
- ValueSet Expansion
- SMART on FHIR
- GraphQL
- Subscription
- Batch/Transaction Bundle
- Code Generation
- Multi-version Support

---

# 5. Target Users

本 SDK 目標使用者：

- HIS 開發工程師
- 醫療資訊工程師
- FHIR API 開發者
- 醫療申報系統開發團隊
- 醫療資料交換平台

---

# 6. Technical Architecture

---

## 6.1 Project Structure

```text
MyFhirSdk
│
├── MyFhirSdk.Core
│
├── MyFhirSdk.Primitives
│
├── MyFhirSdk.Types
│
├── MyFhirSdk.Resources
│
├── MyFhirSdk.Serialization
│
├── MyFhirSdk.Validation
│
├── MyFhirSdk.Client
│    │
│    ├── Http
│    ├── Operations
│    ├── Search
│    ├── Bundle
│    ├── Authentication
│    ├── Exceptions
│    └── Configuration
│
└── MyFhirSdk.Tests
```

---

## 6.2 FHIR Type Hierarchy

```text
Base
│
└── Element
      │
      └── DataType
            │
            ├── PrimitiveType
            │
            └── Complex Datatype

Resource
│
└── DomainResource
```

---

## 6.3 Data Layer Hierarchy

```text
Base
│
├── Element
│    │
│    ├── PrimitiveType
│    │     ├── boolean
│    │     ├── string
│    │     ├── date
│    │     ├── dateTime
│    │     ├── code
│    │     └── ...
│    │
│    ├── DataType
│    │     ├── Identifier
│    │     ├── HumanName
│    │     ├── Address
│    │     ├── Coding
│    │     ├── CodeableConcept
│    │     └── ...
│    │
│    └── BackboneElement
│
└── Resource
      │
      └── DomainResource
            ├── Patient
            ├── Claim
            ├── Coverage
            └── ...
```

---

# 7. Functional Requirements

---

## FR-001 Resource Creation

SDK 必須能建立 FHIR Resource。

Example:

```csharp
var patient = new Patient();
```

---

## FR-002 JSON Serialization

SDK 必須能將 Resource 轉為合法 FHIR JSON。

Example:

```csharp
string json = serializer.Serialize(patient);
```

---

## FR-003 JSON Deserialization

SDK 必須能將 FHIR JSON 解析為 Resource。

Example:

```csharp
Patient patient = parser.Parse<Patient>(json);
```

---

## FR-004 REST Read

SDK 必須支援 Read API。

Example:

```csharp
await client.ReadAsync<Patient>("Patient/123");
```

---

## FR-005 REST Create

SDK 必須支援 Create API。

Example:

```csharp
await client.CreateAsync(patient);
```

---

## FR-006 REST Update

SDK 必須支援 Update API。

Example:

```csharp
await client.UpdateAsync(patient);
```

---

## FR-007 REST Search

SDK 必須支援 Search API。

Example:

```csharp
await client.SearchAsync<Patient>("name=amy");
```

---

## FR-008 Bundle Parsing

SDK 必須能解析 Bundle。

Example:

```csharp
Bundle bundle = parser.Parse<Bundle>(json);
```

---

## FR-009 Validation

SDK 必須支援基本 Validation。

Example:

```csharp
var result = validator.Validate(patient);
```

---

# 8. Non-Functional Requirements

---

## Performance

- Standard Resource Serialization < 100ms
- Search Response Parsing < 200ms

---

## Compatibility

- .NET 9
- ASP.NET Core
- Blazor
- SQLite Compatible

---

## Test Coverage

- Unit Test Coverage >= 80%

---

## Maintainability

- Modular architecture
- Layer separation
- Extensible datatype design

---

# 9. Development Roadmap

---

## Phase 1 — Core Foundation

建立：

- Base
- Element
- DataType
- PrimitiveType

---

## Phase 2 — Primitive Types

建立：

- FhirString
- FhirBoolean
- Date
- DateTime
- Code
- Id
- Uri
- Decimal

---

## Phase 3 — Complex Datatypes

建立：

- Identifier
- HumanName
- Address
- Coding
- CodeableConcept
- Quantity
- Reference

---

## Phase 4 — Serialization

建立：

- FHIR JSON Serializer
- FHIR JSON Parser

---

## Phase 5 — Resources

建立：

- Patient
- Organization
- Practitioner
- Encounter
- Coverage
- Claim
- Bundle

---

## Phase 6 — REST Client

建立：

- Read
- Create
- Update
- Search

---

## Phase 7 — Validation

建立：

- Required validation
- Cardinality validation
- Primitive format validation

---

# 10. Success Criteria

MVP 完成標準：

- 能建立 Patient Resource
- 能產生合法 FHIR JSON
- 能解析 FHIR JSON
- 能與 HAPI FHIR Server 互通
- 能成功執行 CRUD API
- 能解析 Search Bundle
- 能完成基本 Validation
- 能支援 Claim exchange

---

# 11. Future Enhancements

未來版本預計支援：

- FHIR R5
- XML Serialization
- StructureDefinition Validation
- FHIRPath
- Terminology Service
- Code Generator
- Multi-version Support
- SMART on FHIR
- Batch/Transaction
- Subscription

---

# 12. MVP Definition

本 MVP 定義為：

能讓 HIS 系統：

1. 建立 Patient / Coverage / Claim Resource
2. 轉換為合法 FHIR JSON
3. 呼叫 FHIR REST API
4. 接收並解析 Bundle
5. 執行基本 Validation

即視為 MVP 成功。

