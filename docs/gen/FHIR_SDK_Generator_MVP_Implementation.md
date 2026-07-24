# FHIR SDK Generator MVP 實作規格

Version 1.0

- 文件狀態：Draft / Proposed

- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9

- 上位設計文件：`FHIR_SDK_Generator_Implementation_Flow.md`

## 1. 文件目的

本文件定義 FHIR SDK Generator 第一版 MVP 的實際實作範圍、專案結構、
元件責任、輸入限制、輸出契約、測試方式與完成條件。

完整 Generator 的長期架構仍以 `FHIR_SDK_Generator_Implementation_Flow.md`
為準。本 MVP 不嘗試一次完成 Resource、Profile、slicing、Snapshot Generator
或完整 validation metadata，而是先驗證以下最小流程：

```text
FHIR R5 complex-type StructureDefinition JSON
    ↓
StructureDefinition DTO
    ↓
Generator Internal Model
    ↓
C# source rendering
    ↓
Golden File comparison
    ↓
Compilation and runtime contract tests
```

## 2. MVP 目標

MVP 的目標是從 FHIR R5 Core Package 中選取少量 general-purpose complex
datatype 的 StructureDefinition，產生符合現有 MyFhirSdk model 慣例的 C#
source，並驗證生成結果：

- 能產生決定性且可讀的 C# source。

- 能通過 C# 語法與編譯驗證。

- 能使用現有 FHIR primitive wrapper。

- 能由現有 Serializer 正確序列化。

- 能由現有 Parser 在 property type 已知時正確還原。

- 能由現有 Validator traversal 走訪其中的 primitive value。

MVP 只證明生成模型能接入現有 runtime contract，不代表已完整支援
StructureDefinition validation semantics。

## 3. MVP 範圍

### 3.1 支援範圍

- FHIR R5 5.0.0 Core StructureDefinition。

- `StructureDefinition.kind = complex-type`。

- `StructureDefinition.derivation = specialization`。

- general-purpose complex datatype。

- 輸入必須具有完整 `snapshot.element`。

- 輸入必須具有可用的 `differential.element`，用來選出 specialization
  直接宣告的元素。

- 只處理未 sliced、具有唯一 `ElementDefinition.id` 的元素。

- concrete FHIR type mapping。

- `0..1`、`1..1`、`0..*`、`1..*` cardinality。

- FHIR primitive 到現有 MyFhirSdk primitive wrapper 的 mapping。

- 已知 complex datatype 到 `MyFhirSdk.Types` 的 mapping。

- 一個 FHIR type 產生一個 C# source。

- StringBuilder renderer。

- Golden File、決定性輸出、編譯與 runtime contract 測試。

第一批建議驗證型別：

- `Address`

- `HumanName`

- `Identifier`

- `Coding`

- `Period`

若某個目標型別的官方 StructureDefinition 包含 MVP 未支援的結構，測試應先縮小
輸入型別集合或補上對應功能，不得靜默略過 element。

### 3.2 明確不支援

第一版 MVP 不支援：

- Resource generation。

- FHIR primitive wrapper generation。

- `derivation = constraint` 的 Profile。

- differential-only StructureDefinition。

- Snapshot Generator。

- slicing 與 reslicing。

- slice-specific C# property。

- choice type，例如 `value[x]`。

- `contentReference`。

- open type 或 runtime 才能決定的抽象 `DataType`。

- `Extension.value[x]`。

- binding、fixed、pattern、constraint/invariant 的 runtime validation。

- Profile validation metadata。

- generated Base validation registry。

- ValueSet、CodeSystem、SearchParameter generation。

遇到上述結構時，Generator 必須產生明確且可定位的診斷，不能輸出不完整的
C# model。

## 4. 手寫與生成程式碼共存策略

MVP 採用「測試 namespace＋Golden File」，不直接取代目前的手寫 model。

### 4.1 測試 namespace

MVP Renderer 的 namespace 必須可設定。測試輸出使用：

```csharp
namespace MyFhirSdk.GeneratorFixtures.Types;
```

因此即使同時存在手寫的：

```csharp
namespace MyFhirSdk.Types;

public sealed class HumanName : DataType
{
}
```

測試仍可編譯生成版本：

```csharp
namespace MyFhirSdk.GeneratorFixtures.Types;

public sealed class HumanName : DataType
{
}
```

測試 namespace 只用於 Generator MVP 驗證，不是正式 SDK public API。正式取代
手寫型別前，必須另行審查 API compatibility、serialization behavior 與 migration
方式。

### 4.2 Golden File

每個目標型別保留一份人工審查過的預期輸出：

```text
Tests/CodeGen/GoldenFiles/R5/Types
|-- Address.golden.cs.txt
|-- HumanName.golden.cs.txt
|-- Identifier.golden.cs.txt
|-- Coding.golden.cs.txt
`-- Period.golden.cs.txt
```

Golden File 使用 `.golden.cs.txt`，避免被 SDK-style project 當成 C# source
自動編譯。

Golden test 應以 exact text 比較：

- namespace

- using

- class name 與 base type

- property name、type、nullable 與 collection shape

- XML documentation

- property 順序

- whitespace 與換行

更新 Golden File 必須是明確的開發動作，不得在一般 test run 中自動接受新輸出。

### 4.3 正式 Generated 目錄

長期正式輸出位置保留為：

```text
Generated/R5
|-- Types
|-- Resources
`-- Validation
```

但 MVP 階段不將與手寫型別同名的 source 寫入 `Generated/R5/Types` 並納入正式
SDK Build。MVP 以 test output、暫存編譯與 Golden File 驗證為主。

## 5. Solution 與專案結構

建議結構：

```text
/
|-- core
|-- Primitives
|-- Types
|-- Resources
|-- Serialization
|-- Validation
|
|-- CodeGen
|   |-- MyFhirSdk.CodeGen.csproj
|   |-- Definitions
|   |   |-- StructureDefinitionDto.cs
|   |   |-- ElementDefinitionDto.cs
|   |   `-- ElementTypeDto.cs
|   |-- Models
|   |   |-- FhirTypeModel.cs
|   |   |-- FhirPropertyModel.cs
|   |   `-- CardinalityModel.cs
|   |-- Loading
|   |   |-- FhirPackageLoader.cs
|   |   `-- StructureDefinitionLoader.cs
|   |-- Parsing
|   |   `-- StructureDefinitionParser.cs
|   |-- Mapping
|   |   |-- CSharpTypeMapper.cs
|   |   `-- CSharpNameConverter.cs
|   |-- Rendering
|   |   `-- CSharpClassRenderer.cs
|   |-- Writing
|   |   `-- GeneratedFileWriter.cs
|   `-- Program.cs
|
|-- Generated
|   `-- R5
|       |-- Types
|       |-- Resources
|       `-- Validation
|
`-- Tests
    `-- CodeGen
        |-- MyFhirSdk.CodeGen.Tests.csproj
        |-- Fixtures
        |   `-- StructureDefinitions
        `-- GoldenFiles
            `-- R5
                `-- Types
```

### 5.1 CodeGen project 隔離

`CodeGen` 必須是獨立的 Console project，不得成為正式 SDK assembly 的一部分。
由於目前 `MyFhirSdk.csproj` 位於 repository root，主專案必須排除 CodeGen source：

```xml
<ItemGroup>
  <Compile Remove="CodeGen\**\*.cs" />
</ItemGroup>
```

`Generated/**/*.cs` 不應全域排除；未來正式生成的 model 需要編入 SDK。MVP 的
同名 preview source 則只存在於 test temporary directory 或 Golden File。

### 5.2 CodeGen dependency 原則

CodeGen project 第一版不直接 reference `MyFhirSdk.csproj`。Type mapper 使用穩定的
完整型別名稱，例如：

```text
MyFhirSdk.Core.DataType
MyFhirSdk.Primitives.FhirString
MyFhirSdk.Types.CodeableConcept
```

生成 source 是否真的能與 SDK 一起編譯，由 CodeGen integration test 驗證。這可避免
Generator 與尚未產生完成的 SDK model 形成 build bootstrapping dependency。

## 6. MVP 元件與責任

| 元件 | 輸入 | 輸出 | 責任 |
|---|---|---|---|
| `FhirPackageLoader` | package path | StructureDefinition file list | 讀取 package metadata、確認 FHIR version 並篩選正式定義。 |
| `StructureDefinitionLoader` | JSON file/stream | `StructureDefinitionDto` | 使用 System.Text.Json 忠實反序列化，不做 C# 決策。 |
| `StructureDefinitionParser` | DTO | `FhirTypeModel` | 驗證 MVP 支援條件，選擇直接宣告元素並建立 Internal Model。 |
| `CSharpTypeMapper` | FHIR type code | C# type name | 映射 primitive 與已知 complex datatype。 |
| `CSharpNameConverter` | FHIR name/path | C# identifier | 處理 PascalCase、保留字、特殊字元與衝突。 |
| `CSharpClassRenderer` | `FhirTypeModel` | source string | 產生合法、固定格式的 C# source，不重新解析 FHIR 規則。 |
| `GeneratedFileWriter` | source＋relative path | generated file | 驗證路徑、固定 encoding、暫存寫入與 deterministic output。 |
| `Program` | CLI arguments | exit code＋diagnostics | 組合完整流程，不承擔解析或 rendering 規則。 |

第一版可以先讓 `StructureDefinitionLoader` 接受單一 JSON file 或目錄。
`FhirPackageLoader` 可在第二個 milestone 補上，但 DTO 與 Parser 不應假設輸入永遠
來自單一硬編碼路徑。

## 7. Definition DTO

DTO 應忠實反映 Generator 所需的 JSON 欄位，不能包含 C# rendering 決策。

### 7.1 StructureDefinitionDto

最小欄位：

```text
resourceType
id
url
version
name
type
kind
abstract
baseDefinition
derivation
snapshot.element
differential.element
```

MVP 以 differential 選擇 specialization 直接宣告的元素，再由 snapshot 取得
已解析完成的 type 與 cardinality；不在 MVP 內自行合併兩者或產生 snapshot。

### 7.2 ElementDefinitionDto

最小欄位：

```text
id
path
sliceName
min
max
contentReference
type.code
type.profile
type.targetProfile
short
definition
```

`sliceName` 與 `contentReference` 在 MVP 中用於偵測並拒絕未支援結構。
不得因為第一版不處理而從 DTO 移除。

DTO 應使用明確的 `JsonPropertyName`，並設定未知 JSON property 可被忽略。缺少
MVP 必要欄位時，由 Loader 或 Parser 產生結構化診斷。

## 8. Generator Internal Model

Internal Model 必須與 JSON DTO 分離，並只保存 Renderer 需要的已決策資訊。

### 8.1 FhirTypeModel

至少包含：

```text
FhirName
CSharpName
Namespace
CSharpBaseType
IsAbstract
SourceCanonical
SourceVersion
Properties
```

### 8.2 FhirPropertyModel

至少包含：

```text
ElementId
ElementPath
FhirName
CSharpName
CSharpType
IsCollection
IsRequired
Min
Max
Documentation
Order
```

`Min` 與 `Max` 即使暫時不產生 validation rule，也必須保留，避免 Internal Model
失去原始 cardinality 決策。

### 8.3 Cardinality mapping

| FHIR cardinality | C# property shape | MVP validation |
|---|---|---|
| `0..1` | `T?` | 無 required rule |
| `1..1` | `T?` | 暫不生成 required rule |
| `0..*` | `IList<T>` 並初始化 | 無最小數量規則 |
| `1..*` | `IList<T>` 並初始化 | 暫不生成最小數量規則 |

MVP 保持與現有 SDK 一致：FHIR required singleton 仍使用 nullable property，
required semantics 由未來 Validator rule 負責，不使用 C# `required` keyword
改變現有 SDK 建構方式。

## 9. C# 型別與命名映射

### 9.1 Primitive mapping

第一版至少支援目標 complex datatype 所需的 primitive：

| FHIR type | C# type |
|---|---|
| `boolean` | `MyFhirSdk.Primitives.FhirBoolean` |
| `string` | `MyFhirSdk.Primitives.FhirString` |
| `code` | `MyFhirSdk.Primitives.FhirCode` |
| `id` | `MyFhirSdk.Primitives.FhirId` |
| `uri` | `MyFhirSdk.Primitives.FhirUri` |
| `url` | `MyFhirSdk.Primitives.FhirUrl` |
| `canonical` | `MyFhirSdk.Primitives.FhirCanonical` |
| `integer` | `MyFhirSdk.Primitives.FhirInteger` |
| `decimal` | `MyFhirSdk.Primitives.FhirDecimal` |
| `date` | `MyFhirSdk.Primitives.FhirDate` |
| `dateTime` | `MyFhirSdk.Primitives.FhirDateTime` |
| `instant` | `MyFhirSdk.Primitives.FhirInstant` |
| `positiveInt` | `MyFhirSdk.Primitives.FhirPositiveInt` |
| `unsignedInt` | `MyFhirSdk.Primitives.FhirUnsignedInt` |
| `base64Binary` | `MyFhirSdk.Primitives.FhirBase64Binary` |
| `markdown` | `MyFhirSdk.Primitives.FhirMarkdown` |

遇到沒有 mapping 的 type code 必須失敗並指出 definition canonical 與 element id。

### 9.2 Complex type mapping

已知 complex datatype 預設映射至：

```text
MyFhirSdk.Types.{FHIR type name}
```

當目前正在生成的型別引用另一個 preview generated type 時，test compilation
harness 應將相關 source 一起編譯，並使用同一個 test namespace。Mapper 不應讓
單一 source 同時引用正式與 preview namespace 中的同名型別。

### 9.3 名稱轉換

名稱轉換集中在 `CSharpNameConverter`：

- FHIR type name 轉合法 PascalCase class name。

- element name 轉合法 PascalCase property name。

- C# keyword 使用穩定的消歧規則。

- 移除或替換不合法 identifier 字元。

- 同一型別內產生重複 property name 時回報錯誤。

- 不由 Renderer 再次修改名稱。

## 10. Snapshot element 選擇

snapshot 包含 inherited element。MVP 生成 complex datatype 時，不應把所有
snapshot element 重新宣告。

第一版 selection 規則：

1. 驗證 snapshot 存在且第一個 root element 與 StructureDefinition type 一致。

2. 驗證 differential 存在，且 differential root element 與 StructureDefinition
   type 一致。

3. 以 differential 中的非 root element 作為目前 specialization 直接宣告或覆寫的
   candidate。

4. 以 `ElementDefinition.id` 在目前 snapshot 中找到相同元素，使用 snapshot
   取得已解析完成的 type、min、max 與 documentation。

5. 第一版只選擇目前 type 的直接 child element；differential 中若包含 inherited
   element override、較深層 child 或無法分類的項目，回報 unsupported diagnostic。

6. inherited element 由 C# base type 提供，不重複生成。

7. 若同一路徑出現 slice、choice、contentReference 或無法判定的 override，回報
   unsupported diagnostic。

MVP 不實作通用 snapshot merge，也不處理 constraint Profile。Element selection
仍必須獨立成可測試邏輯，不能埋在 Renderer。

## 11. Renderer 輸出契約

第一版 source 形狀：

```csharp
// <auto-generated />

using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.GeneratorFixtures.Types;

/// <summary>
/// Human-readable definition from StructureDefinition.
/// </summary>
public sealed class HumanName : DataType
{
    public FhirString? Family { get; set; }

    public IList<FhirString> Given { get; set; } = new List<FhirString>();
}
```

Renderer 必須維持：

- UTF-8 without BOM。

- 固定 newline policy；MVP test 預設使用 LF。

- ordinal property ordering。

- 固定 using ordering。

- `// <auto-generated />` 標記。

- XML documentation 特殊字元 escaping。

- 一個型別一個 source string。

- 相同 Internal Model 產生完全相同的輸出。

## 12. CLI

建議 MVP CLI：

```powershell
dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- `
  --input <structure-definition-directory> `
  --output <output-directory> `
  --namespace MyFhirSdk.GeneratorFixtures.Types `
  --fhir-version 5.0.0 `
  --type HumanName `
  --type Address
```

必要參數：

| 參數 | 說明 |
|---|---|
| `--input` | StructureDefinition JSON file、目錄或 package path。 |
| `--output` | 允許寫入的生成根目錄。 |
| `--namespace` | 生成 C# namespace；MVP test 使用 fixture namespace。 |
| `--fhir-version` | 預期 FHIR version。 |
| `--type` | 要生成的 FHIR type，可重複指定。 |

CLI 不得預設覆寫正式 `Types/` 或 `Generated/`。MVP 的預設行為應要求明確
`--output`，並拒絕輸出到 repository root、`core`、`Types`、`Resources`、
`Serialization` 或 `Validation`。

成功回傳 exit code `0`；輸入錯誤、unsupported definition、mapping failure、
render failure 或 write failure 回傳非零 exit code。

## 13. 診斷與失敗策略

診斷至少包含：

```text
Code
Severity
Message
SourceFile
DefinitionCanonical
DefinitionVersion
ElementId
ElementPath
```

建議第一版診斷碼：

| Code | 狀況 |
|---|---|
| `FSG0001` | JSON 無法讀取或反序列化。 |
| `FSG0002` | FHIR version 不符。 |
| `FSG0003` | 缺少 snapshot。 |
| `FSG0004` | 缺少 differential。 |
| `FSG0005` | 不支援的 StructureDefinition kind 或 derivation。 |
| `FSG0006` | 發現 slicing。 |
| `FSG0007` | 發現 choice type。 |
| `FSG0008` | 發現 contentReference。 |
| `FSG0009` | FHIR type 無 C# mapping。 |
| `FSG0010` | C# 名稱衝突。 |
| `FSG0011` | 輸出路徑不安全或不允許。 |
| `FSG0012` | 生成 source 無法編譯。 |

不支援的 element 不得只記 warning 後繼續生成完整類別。只要遺漏 element 會改變
model shape，就必須使該 definition 生成失敗。

## 14. 現有 Runtime 串接

### 14.1 Serializer

現有 `FhirJsonSerializer` 使用 reflection 讀取 public property，並依 C# property
name 推導 lower camel case JSON name。MVP generated type 必須驗證：

- singleton primitive。

- repeated primitive。

- nested concrete complex datatype。

- null singleton 不輸出。

- empty collection 不輸出。

- primitive raw value 與 primitive metadata。

Serializer 不讀取 Generator Internal Model，因此 JSON name 與 property shape
必須已反映在生成 C# source。

### 14.2 Parser

MVP 只測試 property 已宣告 concrete generated type 的情況。Parser 可從
`PropertyInfo.PropertyType` 建立 instance，不需要第一版 generated parser registry。

下列情況留待後續：

- abstract `DataType`。

- Extension value type。

- open choice。

- 依 JSON object shape 推斷 concrete datatype。

### 14.3 Validator

現有 object graph walker 可走訪繼承 `DataType` 的 generated instance，並對其中
實作 `IFhirValidatablePrimitive` 的 primitive 執行格式驗證。

MVP runtime test 只保證：

- generated datatype 可以被 walker 走訪。

- nested primitive path 正確。

- invalid primitive 可以產生 PrimitiveFormat issue。

MVP 不產生 required、collection min/max、choice、binding 或 invariant rule。
測試與文件不得把這些未實作能力描述為已支援。

## 15. 測試策略

### 15.1 Unit tests

- StructureDefinition JSON DTO deserialization。

- 缺少必要欄位的診斷。

- FHIR type mapping。

- C# name conversion。

- keyword 與名稱衝突。

- cardinality mapping。

- snapshot declared element selection。

- unsupported slice、choice 與 contentReference。

- renderer formatting。

- output path validation。

### 15.2 Golden tests

每個第一批型別至少一個 Golden File。測試流程：

1. 載入固定 StructureDefinition fixture。

2. 轉換成 Internal Model。

3. 產生 C# source。

4. 將 newline 正規化為 LF。

5. 與 `.golden.cs.txt` exact comparison。

6. 差異發生時顯示可讀 diff，但不自動改寫 Golden File。

### 15.3 Compilation test

將一組互相依賴的 generated source 與必要 SDK reference 交給 Roslyn Compilation，
並確認：

- 沒有 syntax error。

- base type 可解析。

- primitive 與 complex type reference 可解析。

- property name 沒有重複。

- nullable 與 collection 宣告合法。

### 15.4 Runtime contract tests

建立 test-only Resource 或容器，property 使用
`MyFhirSdk.GeneratorFixtures.Types` 中的 generated type，驗證：

- Serializer 產生預期 FHIR JSON。

- Parser 還原 concrete generated type。

- serialize → parse → serialize 結果一致。

- Validator traversal 可到達 generated datatype 中的 primitive。

### 15.5 Determinism test

同一組輸入以不同檔案列舉順序執行兩次，所有輸出檔名與內容必須完全一致。

## 16. 實作順序

1. 建立 `MyFhirSdk.CodeGen` 與 `MyFhirSdk.CodeGen.Tests` project。

2. 從主 SDK project 排除 `CodeGen/**/*.cs`。

3. 建立最小 StructureDefinition DTO 與 JSON Loader。

4. 建立 Internal Model。

5. 實作 primitive/complex type mapper 與 name converter。

6. 實作 snapshot declared element selection 與 unsupported feature diagnostics。

7. 實作 cardinality mapping。

8. 實作 StringBuilder renderer。

9. 建立 HumanName 第一個 Golden File。

10. 加入 Address、Identifier、Coding 與 Period Golden Files。

11. 加入 Roslyn compilation test。

12. 加入 Serializer、Parser 與 Validator runtime contract tests。

13. 加入 deterministic output 與 CLI smoke test。

## 17. MVP 完成條件

MVP 完成必須同時符合：

- CodeGen 是獨立 project，沒有被編入正式 SDK assembly。

- 至少三個選定 general-purpose complex datatype 能從固定 R5
  StructureDefinition fixture 成功生成。

- 每個成功生成的型別都有人工審查過的 Golden File。

- Golden File test 通過。

- generated source compilation test 通過。

- Serializer 與 Parser round-trip test 通過。

- Validator traversal 與 primitive format test 通過。

- 相同輸入的輸出完全一致。

- slice、choice、contentReference、constraint Profile 與未知 type mapping
  都會明確失敗。

- 沒有與正式 `MyFhirSdk.Types` 手寫型別發生重複定義。

- 一般 test run 不會自動修改 Golden File 或正式 Generated 目錄。

## 18. MVP 後續工作

MVP 完成後，再依優先順序評估：

- 正式取代部分手寫 complex datatype。

- generated source 與 hand-written `partial` extension 策略。

- FHIR Package dependency loader。

- differential 與 Snapshot Generator。

- choice type。

- contentReference。

- BackboneType 與更複雜 datatype。

- Resource 與頂層 BackboneElement generation。

- generated parser type registry。

- generated Base validation registry。

- Profile、slicing 與 Profile validation metadata。
