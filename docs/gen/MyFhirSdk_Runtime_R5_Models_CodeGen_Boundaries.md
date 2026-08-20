# MyFhirSdk Runtime、R5 Models 與 CodeGen 責任邊界

Version 1.2

- 文件狀態：Runtime Phase A Completed；Phase B handoff ready
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- 目標交付形式：`MyFhirSdk.CodeGen` 作為 .NET local tool
- 相關文件：
  - `docs/gen/FHIR_SDK_Generator_Implementation_Flow.md`
  - `docs/gen/FHIR_SDK_Generator_MVP_Implementation.md`
  - `docs/gen/FHIR_SDK_Generator_MVP_Implementation_steps.md`
  - `docs/gen/MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md`
  - `docs/gen/MyFhirSdk_Primitive_Generation_Phase_B_Handoff.md`
  - `docs/gen/MyFhirSdk_Primitive_Generation_Phase_B_Implementation_Guide.md`

## 1. 文件目的

本文件定義 `MyFhirSdk.Runtime`、R5 Models 與 `MyFhirSdk.CodeGen` 三者的責任、
依賴方向及最小手寫範圍，作為將 Generator 發布為 .NET local tool，以及後續以
Generator 取代大量手寫 FHIR model 的架構基準。

本文件中的 Runtime 是 MyFhirSdk 提供的執行核心，不是 Microsoft .NET Runtime。

## 2. 核心決策

MyFhirSdk 採用「手寫執行核心、生成規格模型」的分工：

```text
官方 FHIR R5 definitions + MyFhirSdk generation policy
                         │
                         ▼
               MyFhirSdk.CodeGen.Tool
                         │ 產生
                         ▼
                 MyFhirSdk.R5.Models
                         │ 依賴
                         ▼
                  MyFhirSdk.Runtime
```

主要原則如下：

1. CodeGen 是建置工具，負責把 FHIR 規格轉成 C# source，不提供 SDK 執行行為。
2. R5 Models 是 CodeGen 的主要輸出，負責表達 R5 的型別、屬性與規格 metadata。
3. Runtime 提供 generated models 執行時需要的穩定機制，保持小型且盡量不依賴
   特定 R5 concrete model。
4. CodeGen 不得依賴手寫的 concrete `Types` 或 `Resources` 來完成相同型別的生成。
5. 成為 local tool 不代表 generated source 必須自包含；generated models 可以且應該
   明確依賴相容版本的 `MyFhirSdk.Runtime`。
6. Runtime 不反向依賴 R5 Models，以避免循環依賴。

目標依賴關係：

```text
MyFhirSdk.CodeGen.Tool ───────► MyFhirSdk.Runtime.Abstractions（如有必要）
          │
          └── produces ───────► MyFhirSdk.R5.Models

MyFhirSdk.R5.Models ──────────► MyFhirSdk.Runtime

MyFhirSdk.Runtime ──X─────────► MyFhirSdk.R5.Models
MyFhirSdk.CodeGen.Tool ─X─────► 手寫 concrete R5 Types/Resources
```

## 3. MyFhirSdk.Runtime 的最小手寫範圍

Runtime 只保留無法由 StructureDefinition 完整推導，或所有 generated models 都需要
共用的執行機制。

### 3.1 公開的最小 model contract

Runtime 應提供 generated source 可以依賴的穩定公開契約：

- FHIR object 的最小根型別或 marker contract。
- `DataType`、`BackboneType`、`Resource` 等 generated model 必須使用的最小分類契約。
- `PrimitiveType<T>` 的共同 value 與 primitive metadata 行為。
- Resource type identity 所需的最小契約，例如唯讀的 `ResourceType`。
- Serializer、Parser 與 Validator 的公開入口及結果型別。
- 穩定的例外、diagnostic 與 validation result contract。

公開契約應只包含使用者操作 SDK 所需的內容，不應暴露 Runtime 的 codec、registry、
primitive validator 或 reflection cache。

### 3.2 Primitive runtime

Primitive runtime 維持手寫，負責 primitive「如何運作」：

- `PrimitiveType<T>` 共同基底。
- raw CLR value、`HasValue` 及 primitive element metadata。
- FHIR JSON raw value 與 `_property` metadata 的讀寫及陣列對齊。
- CLR value 與 FHIR JSON token 間的 codec。
- `decimal` literal precision、R5 `integer64` JSON string 等特殊 wire-format 行為。
- `date`、`dateTime`、`instant`、`base64Binary` 等特殊解析與格式驗證。
- internal primitive definition、codec、validator 與 lookup registry。

Primitive format validation 不放在 public primitive interface。對外只提供統一驗證入口：

```csharp
ValidationResult Validate(Resource resource);
```

使用者可以取得 primitive validation issue，但不能呼叫、替換或修改內部 primitive
validator。Phase A 已實作的內部契約為：

```csharp
internal interface IPrimitiveCodec
{
    // Parse and write FHIR JSON primitive values.
}

internal interface IPrimitiveValidator
{
    bool IsValid(object? value);
}

internal interface IPrimitiveDefinition
{
    string FhirTypeName { get; }
    Type PrimitiveType { get; }
    Type ValueType { get; }
    IPrimitiveCodec Codec { get; }
    IPrimitiveValidator Validator { get; }
}
```

可見性與責任邊界不得改變：行為 contract 保持
`internal`，FHIR primitive wrapper 保持薄且不公開 `IsValid()`。

### 3.3 Serialization 與 parsing engine

Runtime 手寫保留：

- FHIR JSON serializer/parser 的通用流程。
- primitive raw value 與 metadata 的特殊 JSON 規則。
- collection、null、nested object 與 extension value 的通用處理。
- 根據 generated registry 建立 concrete Resource/DataType 的機制。
- JSON token 與 CLR value 的安全轉換。

Runtime 不應手寫列舉所有 R5 Resource 或 property。Resource 名稱至 CLR type、choice
property、extension value type 等規格清單應由 CodeGen 產生 registry/metadata，再由
Runtime engine 消費。

### 3.4 Validation engine

Runtime 手寫保留：

- object graph traversal 與 FHIR path 組合。
- validation pipeline、rule execution 與結果彙整。
- primitive format validator 的執行機制。
- cardinality、required、choice、binding、fixed/pattern、invariant 等通用 rule engine。
- validation issue、severity、source 與 diagnostic contract。

下列內容不應長期手寫在 Runtime：

- 每個 Resource 的 property rule 清單。
- 每個 R5 property 的 cardinality。
- 每個 choice element 的選項。
- 每個 binding、fixed、pattern 或 invariant 的 model-specific metadata。

上述規格資料應由 CodeGen 生成，Runtime 只負責執行。

### 3.5 Runtime metadata infrastructure

Runtime 可手寫 metadata 的抽象與查詢機制，例如：

- FHIR type descriptor contract。
- primitive definition contract。
- Resource factory/registry contract。
- property、choice、cardinality 與 validation metadata contract。
- metadata lookup、cache 與錯誤處理。

每個 R5 型別的實際 descriptor 和 registry entries 應由 CodeGen 產生。

### 3.6 不屬於最小 Runtime 的內容

下列內容不納入最小 Runtime：

- `Patient`、`Observation`、`Encounter` 等 concrete Resource。
- `HumanName`、`Address`、`Coding` 等 concrete complex datatype。
- R5 全部 primitive wrapper 的重複 class declaration。
- R5 model-specific registries 與 validation rule registrations。
- FHIR package loader、StructureDefinition DTO、Parser、Renderer 與 source writer。
- Golden File 與 Roslyn generation validation harness。
- HTTP Client、authentication、search builder 與 transport implementation。
- Implementation Guide 或 Profile 專屬規則。

Client 可以另為 `MyFhirSdk.Client`，並依賴 Runtime 與 R5 Models，但 model generation
與 model 執行不應依賴 Client。

## 4. R5 Models 的責任

R5 Models 是由 CodeGen 依固定版本的官方 FHIR definitions 及 MyFhirSdk generation
policy 產生的版本化模型層。

### 4.1 應生成的內容

- R5 primitive wrapper class declaration。
- R5 complex datatypes。
- R5 Resources。
- BackboneElement/BackboneType 的具體結構。
- property type、cardinality、collection shape、choice property 與繼承關係。
- XML documentation 與必要的 source provenance。
- Resource type registry、factory entries 及 parser metadata。
- primitive wrapper registry。
- serializer/parser 所需的 model metadata。
- cardinality、choice、binding、constraint 等 validation metadata。

### 4.2 Primitive wrapper 的範圍

Generated primitive wrapper 負責宣告 FHIR 型別身分，不負責實作 codec 或 validation
algorithm。例如：

```csharp
// Generated
public sealed class FhirString : PrimitiveType<string>
{
    public FhirString()
    {
    }

    public FhirString(string? value)
        : base(value)
    {
    }
}
```

CodeGen 生成 wrapper 時需要兩個輸入：

1. 官方 primitive StructureDefinition：提供 FHIR 名稱、canonical、版本、繼承及文件。
2. MyFhirSdk primitive generation policy：指定 CLR backing type、Runtime primitive
   definition/codec key 及必要的 C# 命名決策。

官方 StructureDefinition 不能單獨決定 `date` 使用 `string` 或 `DateOnly`、`decimal`
如何保留 literal、`integer64` 如何寫入 JSON；這些是 MyFhirSdk 的手寫 Runtime policy。

### 4.3 R5 Models 不負責的內容

- 不自行實作 JSON serializer/parser engine。
- 不自行實作 validation traversal 或 rule engine。
- 不自行實作 HTTP transport。
- 不把複雜 primitive codec/validator 複製進每個 wrapper。
- 不包含 CodeGen 的 DTO、parser 或 renderer。

## 5. MyFhirSdk.CodeGen 的責任

CodeGen 是 build-time tool，負責把規格資料轉換成可重現的 R5 Models source。

### 5.1 輸入

- 固定版本的官方 FHIR R5 definitions/package。
- 要生成的型別或 package 範圍。
- 目標 namespace、輸出目錄與 FHIR version。
- MyFhirSdk generation policy，包括 primitive CLR mapping 與 Runtime contract mapping。

### 5.2 處理責任

- package metadata、definition inventory 與 dependency resolution。
- StructureDefinition 載入與診斷。
- snapshot/differential element selection。
- primitive、complex datatype、Resource、Backbone 與 Profile 的 internal model 建立。
- C# name、type、cardinality、choice、inheritance 與 namespace mapping。
- deterministic source rendering。
- generated registries 與 validation metadata rendering。
- all-or-nothing、安全路徑的檔案輸出。
- generated batch 的 Roslyn compilation validation。
- 可定位、穩定排序的 diagnostics 與 exit codes。

### 5.3 不負責的內容

- 不在生成期間執行 FHIR Resource validation。
- 不實作應用程式執行期的 serializer/parser/validator engine。
- 不提供 HTTP Client。
- 不以現存手寫 concrete model 作為完整生成的必要 type whitelist。
- 不寫入或覆蓋 Runtime 手寫 source 目錄。

## 6. Local tool 的交付模型

建議將 CodeGen 發布為 .NET tool package：

```text
PackageId:       MyFhirSdk.CodeGen.Tool
ToolCommandName: myfhir-codegen
```

repository 使用 local tool manifest 固定版本：

```powershell
dotnet tool restore
dotnet myfhir-codegen --input <definitions> --output <generated> ...
```

Local tool package 可以攜帶自己執行所需的 Runtime assembly；這只表示工具可以獨立
安裝和執行，不表示 generated source 不需要 Runtime package。

產生的 R5 Models project 應明確引用相容的 Runtime：

```xml
<PackageReference Include="MyFhirSdk.Runtime" Version="0.1.0" />
```

CodeGen tool、Runtime 與 generated R5 Models 必須定義相容版本。至少應在 generated
source 或 manifest 記錄：

- FHIR specification/package version。
- CodeGen version。
- Runtime contract version。
- generation policy version。

## 7. Base types 的邊界與 bootstrap 問題

目前 `core/` 同時包含兩類內容：

1. 執行契約，例如 `FhirObject`、`DataType`、`PrimitiveType<T>`。
2. R5 結構欄位，例如 `Resource.Meta`、`DomainResource.Text`、`Contained` 與 extensions。

最終邊界不應直接以目前資料夾位置決定。需逐一分類：

| 內容 | 目標歸屬 |
|---|---|
| model 分類、共同執行 contract | Runtime |
| primitive value/metadata 機制 | Runtime |
| 由 StructureDefinition 定義的 R5 property shape | R5 Models/generated metadata |
| serializer/parser/validator engine | Runtime |
| `Extension`、`Meta`、`Narrative` 的規格結構 | 原則上 R5 Models |

但 `Element`、`Resource` 等基底目前直接引用 `Extension`、`Meta`、`Narrative`，若立刻
拆 assembly 會形成 Runtime 與 Models 的循環依賴。因此遷移期可暫時將這些 bootstrap
types 留在 Runtime，直到完成下列其中一種設計：

- Runtime base class 只保留行為 contract，R5 Models 生成版本化的中介 base models；或
- Runtime 使用不依賴 concrete R5 type 的 metadata/abstraction 表示共同欄位；或
- 明確接受少量 foundational FHIR types 為 Runtime 的版本化 bootstrap contract。

在選定方案前，不應為了形式上的專案拆分破壞目前 serializer、parser 與 validator
contract。Local tool 的發布不以完成此拆分為必要條件。

## 8. 目前實作與目標架構的差距

Phase A 已移除 Parser/Serializer primitive 類別名稱分支，並把 Parser、Serializer、
Validator 與 concrete R5 metadata 分離。目前仍有以下已指派 owner 的後續工作：

- `MyFhirSdk.CodeGen` 以 `ProjectReference` 依賴整個 `MyFhirSdk.csproj`。
- Roslyn validator 直接使用 `DataType` 所在的現有 SDK assembly。
- `CSharpTypeMapper` 的 primitive mapping 由 Phase B versioned generation policy 取代；
  手寫 complex type whitelist 由 Phase C definition inventory 取代。
- generated datatype 依賴目前手寫的 `Core`、`Primitives` 與部分 `Types`。
- 17 個 primitive wrapper declarations 與 default registry entries 仍為手寫，交由 Phase B
  依 `MyFhirSdk_Primitive_Generation_Phase_B_Handoff.md` 生成。
- R5 resource、datatype、extension 與 validation entries 已集中於 `ModelMetadata/R5`，但
  仍是手寫/assembly scan，交由 Phase C generated provider 取代。
- Runtime、R5 Models 目前仍編譯於單一 SDK assembly；bootstrap debt 與未來 assembly seam
  已在 Phase B handoff 登錄，不在 Phase A 強制拆分。

上述項目不影響已完成的五種 datatype MVP，但必須在完整 SDK generation 前逐步移除。

## 9. 建議遷移順序

### Phase A：固定 Runtime contract

詳細工作分解、完成標準與驗收方式見
`docs/gen/MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md`。

1. 盤點 public API，定義 Runtime 必須保留的最小 base contracts。
2. 定義 internal primitive definition、codec 與 validator contract。
3. 移除 Parser/Serializer 依 primitive 類別名稱分支的行為。
4. 將 model-specific registry 與通用 engine 分離。
5. 維持現有手寫 models 作為 regression oracle，不立即刪除。

Phase A 的 contract、provider injection、architecture gates 與 A6 handoff 已完成；後續以
`MyFhirSdk_Primitive_Generation_Phase_B_Handoff.md` 作為 Phase B 唯一 primitive policy
交接基準。

### Phase B：建立 primitive generation

1. 建立 primitive generation policy。
2. 載入官方 R5 primitive StructureDefinitions。
3. 生成 primitive wrappers 與 internal registry。
4. 以現有 primitive tests 驗證行為完全相容。
5. generated wrappers 通過後再移除對應手寫 wrapper。

### Phase C：完整 model generation

1. 建立完整 definition inventory 與 dependency graph。
2. 移除手寫 complex type whitelist。
3. 生成所有 complex datatypes、base model shape 與 registries。
4. 生成 Resources、Backbone structures、choice 與 validation metadata。
5. 使用完整 R5 batch 進行 deterministic、Roslyn 與 runtime contract tests。

### Phase D：local tool 發布

Local tool 的技術包裝可提早進行，但正式支援範圍必須清楚標示。發布前至少應：

1. 將 CLI 與 generation pipeline 保持分離。
2. 移除對 repository root 的非必要假設。
3. 讓 Roslyn validation 使用明確的 Runtime reference，而非搜尋原始專案輸出。
4. 加入 `PackAsTool`、`ToolCommandName`、`PackageId` 與版本 metadata。
5. 使用 local tool manifest 做安裝、restore、generation 與 upgrade smoke test。
6. 驗證 tool package 不需要 clone 本 repository 即可執行。

## 10. 驗收原則

完成此架構遷移後應符合：

- CodeGen tool 可透過 `dotnet tool restore` 安裝與執行。
- Runtime 不引用 R5 Models 或 CodeGen。
- R5 Models 只依賴 Runtime，不依賴 CodeGen。
- CodeGen 不依賴手寫 concrete R5 Types/Resources 完成生成。
- R5 Models 可從固定官方 definitions 與 policy 重現。
- generated primitive wrappers 不公開 `IsValid()`、codec 或 validator。
- 使用者透過統一 `FhirValidator` 取得包含 primitive issue 的驗證結果。
- 使用者不能替換或修改內建 primitive validation algorithm。
- Serializer/Parser 不以 primitive 或 Resource 的 C# 類別名稱硬編碼行為。
- generated output 與 Runtime contract version 不相容時，能在 build 或 generation
  階段明確失敗。
- Runtime、R5 Models 與 CodeGen 都能各自進行有意義的單元及整合測試。

## 11. 最終分工摘要

| 問題 | Runtime | R5 Models | CodeGen |
|---|---:|---:|---:|
| FHIR object 最小執行 contract | 手寫 | 使用 | 映射 |
| primitive value/metadata 機制 | 手寫 | 使用 | 不實作 |
| primitive codec/format validation | 手寫、internal | 不實作 | 選擇 policy key |
| primitive wrapper declaration | Phase A 手寫 oracle；Phase B 後不手寫 | generated | 生成 |
| complex datatype/Resource property | 不手寫 | generated | 生成 |
| serializer/parser engine | 手寫 | 提供 metadata | 不實作 |
| Resource/type registry entries | 執行 registry | generated | 生成 |
| validation rule engine | 手寫 | 提供 metadata | 生成 metadata |
| StructureDefinition/package processing | 不負責 | 不負責 | 實作 |
| deterministic source output | 不負責 | 被產生 | 實作 |
| HTTP Client | 另立 Client package | 被 Client 使用 | 不負責 |

最終原則：Runtime 定義「FHIR model 如何運作」，R5 Models 定義「R5 有哪些 model
以及它們的結構」，CodeGen 定義「如何從官方規格重現這些 R5 Models」。
