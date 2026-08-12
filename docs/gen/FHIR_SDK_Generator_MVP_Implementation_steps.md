# FHIR SDK Generator MVP 實作步驟

Version 1.0

- 文件狀態：Completed
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- 依據文件：
  - `docs/gen/FHIR_SDK_Generator_MVP_Implementation.md`
  - `docs/gen/FHIR_SDK_Generator_Implementation_Flow.md`

## 1. 實作目標

本文件將 FHIR SDK Generator MVP 規格拆解為可執行、可測試與可逐步驗收的實作
步驟。

MVP 先從少量 FHIR R5 general-purpose complex datatype 的
`StructureDefinition` JSON 產生 C# source，並驗證生成模型能接入現有
Serializer、Parser 與 Validator。

第一批目標型別：

1. `Period`
2. `Coding`
3. `HumanName`
4. `Address`
5. `Identifier`

MVP 不直接取代目前 `Types/` 下的手寫模型。所有預覽生成型別使用測試 namespace：

```csharp
namespace MyFhirSdk.GeneratorFixtures.Types;
```

## 2. 整體實作流程

```text
專案隔離與測試骨架
    ↓
StructureDefinition DTO 與 Loader
    ↓
Diagnostics 與 Internal Model
    ↓
Snapshot/Differential Element Selection
    ↓
Type、Name 與 Cardinality Mapping
    ↓
HumanName 垂直切片
    ↓
五種 Datatype Golden Files
    ↓
Roslyn Compilation Validation
    ↓
Serializer／Parser／Validator Contract Tests
    ↓
安全檔案輸出、CLI 與 Determinism Test
```

## 3. Phase 0：固定 MVP 契約

### 3.1 支援條件

- 僅支援 FHIR R5 `5.0.0`。
- 僅支援 `StructureDefinition.kind = complex-type`。
- 僅支援 `StructureDefinition.derivation = specialization`。
- 輸入必須包含完整的 `snapshot.element`。
- 輸入必須包含可用的 `differential.element`。
- 只處理目前型別直接宣告的 child element。
- 只處理 concrete FHIR type。
- 支援 `0..1`、`1..1`、`0..*`、`1..*` cardinality。
- required singleton 仍輸出 nullable property，不使用 C# `required`。

### 3.2 不支援條件

- Resource generation。
- Primitive wrapper generation。
- `derivation = constraint` Profile。
- differential-only definition 與 Snapshot Generator。
- slicing、reslicing 與 slice-specific property。
- choice type，例如 `value[x]`。
- `contentReference`。
- open type 與執行期才能決定的抽象 `DataType`。
- binding、fixed、pattern、constraint/invariant validation。
- Profile validation metadata。
- generated validation registry。

遇到不支援結構時，必須讓該 definition 生成失敗並輸出可定位的診斷，不得靜默
略過會影響 model shape 的 element。

### 3.3 Phase 0 驗收條件

- 支援與不支援範圍已轉成測試案例名稱或共用常數。
- 測試 namespace、FHIR version 與 newline policy 有單一設定來源。
- 團隊確認 MVP 不會覆寫現有手寫模型。

## 4. Phase 1：建立專案骨架與隔離

### 4.1 建立目錄與專案

```text
CodeGen/
|-- MyFhirSdk.CodeGen.csproj
|-- Definitions/
|-- Diagnostics/
|-- Loading/
|-- Mapping/
|-- Models/
|-- Parsing/
|-- Rendering/
|-- Writing/
`-- Program.cs

Tests/CodeGen/
|-- MyFhirSdk.CodeGen.Tests.csproj
|-- Fixtures/
|   `-- StructureDefinitions/
`-- GoldenFiles/
    `-- R5/
        `-- Types/
```

### 4.2 專案設定

- `MyFhirSdk.CodeGen` 建立為 .NET 9 Console project。
- CodeGen project 第一版不參考 `MyFhirSdk.csproj`。
- CodeGen Tests 同時參考 CodeGen project 與 `MyFhirSdk.csproj`。
- 測試套件版本與現有測試專案保持一致。
- 將 CodeGen 與 CodeGen Tests 加入 `MyFhirSdk.sln`。

### 4.3 隔離主 SDK 編譯

在 repository root 的 `MyFhirSdk.csproj` 加入：

```xml
<ItemGroup>
  <Compile Remove="CodeGen\**\*.cs" />
</ItemGroup>
```

不要全域排除 `Generated/**/*.cs`，以保留未來正式 generated model 編入 SDK 的
能力。

### 4.4 Phase 1 驗收條件

- `dotnet build MyFhirSdk.sln` 成功。
- `dotnet test MyFhirSdk.sln` 成功。
- CodeGen source 不會被主 SDK 重複編譯。
- CLI 尚未有完整功能時可顯示 usage，且缺少必要參數時回傳非零 exit code。

## 5. Phase 2：DTO、Loader 與 Diagnostics

### 5.1 建立 Definition DTO

建立：

- `StructureDefinitionDto`
- `ElementDefinitionDto`
- `ElementTypeDto`
- snapshot 與 differential 容器 DTO

`StructureDefinitionDto` 至少保留：

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

`ElementDefinitionDto` 至少保留：

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

實作原則：

- 使用明確的 `JsonPropertyName`。
- 未知 JSON property 可以忽略。
- DTO 不包含 C# namespace、property name 或 type mapping 決策。
- `sliceName` 與 `contentReference` 即使不支援，也必須保留供 Parser 偵測。

### 5.2 建立 StructureDefinitionLoader

第一版支援：

- 讀取單一 JSON 檔案。
- 讀取指定目錄中的 StructureDefinition JSON。
- 以固定 ordinal 規則排序輸入檔案。
- 捕捉檔案存取與 JSON 反序列化錯誤。
- 驗證 `resourceType` 與 MVP 必要欄位。

FHIR package metadata loader 可留到 CLI 基本流程完成後加入。DTO 與 Parser 不得
依賴硬編碼 fixture 路徑。

### 5.3 建立結構化 Diagnostics

診斷模型至少包含：

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

MVP 診斷碼：

| Code | 狀況 |
|---|---|
| `FSG0001` | JSON 無法讀取或反序列化 |
| `FSG0002` | FHIR version 不符 |
| `FSG0003` | 缺少 snapshot |
| `FSG0004` | 缺少 differential |
| `FSG0005` | 不支援的 kind 或 derivation |
| `FSG0006` | 發現 slicing |
| `FSG0007` | 發現 choice type |
| `FSG0008` | 發現 contentReference |
| `FSG0009` | FHIR type 無 C# mapping |
| `FSG0010` | C# 名稱衝突 |
| `FSG0011` | 輸出路徑不安全或不允許 |
| `FSG0012` | 生成 source 無法編譯 |

建議所有會失敗的元件回傳共用結果型別，例如：

```text
GenerationResult<T>
|-- Value
|-- Diagnostics
`-- IsSuccess
```

### 5.4 Phase 2 測試

- 完整 DTO 反序列化。
- 未知 JSON property 不影響載入。
- 非 StructureDefinition JSON 產生診斷。
- JSON 格式錯誤產生 `FSG0001`。
- FHIR version 不符產生 `FSG0002`。
- 缺少 snapshot 產生 `FSG0003`。
- 缺少 differential 產生 `FSG0004`。
- 不支援的 kind 或 derivation 產生 `FSG0005`。

### 5.5 Phase 2 驗收條件

- Loader 不拋出未處理的輸入例外。
- 每個失敗診斷都能指出來源檔案及 definition canonical。
- 相同目錄內容以固定順序載入。

## 6. Phase 3：Internal Model、Element Selection 與 Mapping

### 6.1 建立 Generator Internal Model

建立：

- `FhirTypeModel`
- `FhirPropertyModel`
- `CardinalityModel`

`FhirTypeModel` 至少包含：

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

`FhirPropertyModel` 至少包含：

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

Internal Model 建議使用 immutable record 或 read-only collection，避免 Renderer
修改 Parser 的決策結果。

### 6.2 實作 Snapshot/Differential Element Selection

Selection 流程：

1. 驗證 snapshot 第一個 element 是目前 definition 的 root。
2. 驗證 differential 第一個 element 是目前 definition 的 root。
3. 以 differential 中非 root element 作為直接宣告或覆寫的 candidate。
4. 使用 `ElementDefinition.id` 在 snapshot 中尋找同一 element。
5. 使用 snapshot 中已解析完成的 type、min、max 與 documentation。
6. 只接受目前型別的直接 child element。
7. inherited element 由 C# base type 提供，不重複生成。
8. 偵測 slice、choice、contentReference、深層 child 及無法分類的 override。
9. 無法安全生成完整 model shape 時，使整個 definition 失敗。

Element selection 必須是獨立且可測試的元件，不得放進 Renderer。

### 6.3 實作 CSharpTypeMapper

Primitive mapping 至少涵蓋 MVP 規格所列型別：

- `boolean`
- `string`
- `code`
- `id`
- `uri`
- `url`
- `canonical`
- `integer`
- `decimal`
- `date`
- `dateTime`
- `instant`
- `positiveInt`
- `unsignedInt`
- `base64Binary`
- `markdown`

已知 complex datatype 預設映射到：

```text
MyFhirSdk.Types.{FHIR type name}
```

Mapper 的結果應能表達：

- primitive 或 complex datatype。
- 完整 C# type name。
- 是否需要 using。
- 應引用正式 SDK namespace 或同批 preview generated namespace。

同批生成的 preview type 必須一致引用測試 namespace。例如同時生成
`HumanName` 與 `Period` 時，preview `HumanName.Period` 不得意外指向正式手寫
`MyFhirSdk.Types.Period`。

### 6.4 實作 CSharpNameConverter

集中處理：

- FHIR type name 轉合法 PascalCase class name。
- element name 轉合法 PascalCase property name。
- C# keyword 消歧。
- 非法 identifier 字元的移除或替換。
- 空名稱與無法建立合法 identifier 的錯誤。
- 同一型別內 property name 衝突。

Renderer 不得再次修改已決定的 C# 名稱。

### 6.5 實作 Cardinality Mapping

| FHIR cardinality | C# property shape |
|---|---|
| `0..1` | `T?` |
| `1..1` | `T?` |
| `0..*` | `IList<T>` 並初始化 |
| `1..*` | `IList<T>` 並初始化 |

Internal Model 仍保留 `Min`、`Max` 與 `IsRequired`，但 MVP 不生成 required 或
collection min/max validation rule。

### 6.6 實作 StructureDefinitionParser

建立 `CodeGen/Parsing/StructureDefinitionParser.cs`，作為 Loader/DTO 與
Internal Model 之間的組裝邊界。Parser 只負責解析 generator 輸入，
不得與 SDK runtime 的 JSON Parser 混用。

建議介面：

```csharp
GenerationResult<FhirTypeModel?> Parse(
    LoadedStructureDefinition loadedDefinition,
    string targetNamespace,
    IReadOnlySet<string> previewFhirTypeNames)
```

Parser 必須組合：

- `StructureDefinitionElementSelector`。
- `CSharpTypeMapper`。
- `CSharpNameConverter`。
- `CardinalityMapper`。

Parsing 流程：

1. 從 `LoadedStructureDefinition` 取得來源檔案與 definition。
2. 驗證建立 Internal Model 所需的 `type`、`url`、`version`、
   `baseDefinition` 與 `abstract`。
3. 使用 `ConvertTypeName` 建立 class name，並決定 target namespace。
4. MVP general-purpose complex datatype 的 base type 映射為 `DataType`；
   未知或不支援的 `baseDefinition` 必須產生診斷。
5. 呼叫 `StructureDefinitionElementSelector` 取得依 differential 順序排列、
   並由 snapshot 補齊的 element。
6. 對每個 selected element 使用 `ConvertPropertyName` 建立 property
   name，並以同一個 ordinal name set 偵測型別內衝突。
7. 只接受 selector 確認的單一 `type.code`，使用 `CSharpTypeMapper`
   決定 primitive、正式 SDK complex type 或同批 preview type。
8. 使用 snapshot element 的 `min` 與 `max` 呼叫 `CardinalityMapper`。
9. 建立 `FhirPropertyModel`，保留 element id/path、FHIR/C# 名稱、
   C# type、cardinality、documentation 與 order。
10. 依 selector 已決定的 ordinal order 建立 properties，最後組成
    immutable `FhirTypeModel`。

診斷與失敗原則：

- 未知 type mapping 產生 `FSG0009`。
- property name 衝突產生 `FSG0010`。
- 名稱、cardinality、base type 或必要欄位無法解析時，產生包含
  source file、definition canonical、element id/path 的診斷。
- 任一 element 失敗時，該 definition 不得回傳部分
  `FhirTypeModel`，且 result `Value` 必須為 `null`；但應盡可能收集同一
  definition 內的可定位診斷。
- Parser 不得重新實作 Selector 或 Mapper 已決定的規則。

### 6.7 Phase 3 測試

- 全部 primitive mapping。
- 已知 complex datatype mapping。
- 未知 type 產生 `FSG0009`。
- 四種 cardinality mapping。
- PascalCase、C# keyword 及非法字元。
- 重複 property name 產生 `FSG0010`。
- snapshot 不重複輸出 inherited property。
- slice、choice、contentReference 分別產生 `FSG0006`～`FSG0008`。
- 無法由 differential id 在 snapshot 找到 element 時生成明確診斷。
- Parser 能將有效 definition 組成包含固定順序 properties 的
  `FhirTypeModel`。
- Parser 的 property 型別、cardinality 與 documentation 來自 selector
  配對的 snapshot element。
- Parser 對同批 preview type 使用 target namespace。
- 任一 property 解析失敗時，Parser 回傳診斷且不產生部分 model。

### 6.8 Phase 3 驗收條件

- Parser 的輸出不包含尚未決定的 FHIR 規則。
- Renderer 所需資訊都已存在 Internal Model。
- unsupported element 不會被靜默忽略。

## 7. Phase 4：完成 HumanName 垂直切片

### 7.1 建立固定 Fixture

在 `Tests/CodeGen/Fixtures/StructureDefinitions/` 放置固定的 HumanName
StructureDefinition fixture。

Fixture 應：

- 來源與 FHIR version 清楚。
- 包含 MVP 所需 snapshot 與 differential。
- 不在一般測試中從網路下載。
- 不因外部 package 更新而自動改變。

### 7.2 實作 CSharpClassRenderer

Renderer 輸出契約：

- `// <auto-generated />`。
- UTF-8 without BOM。
- LF newline。
- 固定 using 順序。
- 固定 namespace。
- class name、base type 與 abstract/sealed 決策。
- XML documentation escaping。
- ordinal property ordering。
- singleton nullable property。
- collection property 初始化。
- 相同 Internal Model 產生完全相同的 source。

Renderer 只接受 `FhirTypeModel`，不得重新解析 DTO 或判斷 FHIR element 規則。

### 7.3 建立 HumanName Golden File

新增：

```text
Tests/CodeGen/GoldenFiles/R5/Types/HumanName.golden.cs.txt
```

Golden test 流程：

1. 載入固定 fixture。
2. 解析成 Internal Model。
3. Render C# source。
4. 將比較內容正規化為 LF。
5. exact text comparison。
6. 失敗時顯示可讀差異。
7. 不得在一般 test run 自動更新 Golden File。

### 7.4 Phase 4 驗收條件

- HumanName 從 JSON 到 C# source 的端到端流程成功。
- Golden File 經人工審查。
- 同一 model render 兩次內容完全相同。
- 生成的 property shape 與現有 SDK model 慣例一致。

## 8. Phase 5：擴充五種 MVP Datatype

建議依下列順序加入：

1. `Period`：驗證 primitive singleton。
2. `Coding`：驗證多種 primitive wrapper。
3. `HumanName`：驗證 primitive collection 與 nested Period。
4. `Address`：驗證較多 property 與 collection。
5. `Identifier`：驗證 complex datatype reference。

每個型別都必須加入：

- StructureDefinition fixture。
- Parser/Internal Model assertion。
- Golden File。
- exact comparison test。

若官方 StructureDefinition 含有 MVP 不支援結構：

- 不得靜默略過該 element。
- 優先確認是否能在不擴張 MVP 的情況下修正 parser。
- 若需要 choice、slice 或 contentReference，應明確延後該型別或另立功能項目。

### Phase 5 驗收條件

- 五種型別都有經人工審查的 Golden File。
- 五種型別可在同一批 generation 中產生。
- 型別間引用使用一致的 preview namespace。
- 輸入檔案順序不同時，輸出型別與 property 順序不變。

## 9. Phase 6：Roslyn Compilation Validation

### 9.1 建立 Compilation Harness

在 CodeGen Tests 加入 Roslyn C# compilation 支援，將五份 generated source
放入同一個 compilation。

參考來源至少包含：

- .NET 9 reference assemblies。
- MyFhirSdk assembly。
- 同批 preview generated syntax trees。

### 9.2 驗證內容

- C# syntax 正確。
- `DataType` base type 可解析。
- primitive wrapper type 可解析。
- complex type reference 可解析。
- collection type 可解析。
- property name 沒有重複。
- nullable 與 collection 宣告合法。

Roslyn error 應轉換或包裝成 `FSG0012`，並保留原始 diagnostic id、訊息與檔名。

### 9.3 Phase 6 驗收條件

- 五份 generated source 可一起編譯。
- compilation error 能回報到對應 generated file。
- compilation validation 失敗時不進行正式檔案替換。

## 10. Phase 7：Runtime Contract Tests

### 10.1 Serializer Tests

驗證：

- singleton primitive。
- repeated primitive。
- nested concrete datatype。
- null singleton 不輸出。
- empty collection 不輸出。
- primitive raw value 與 metadata。

現有 Serializer 使用 public property reflection。Generated property 名稱、型別與
collection shape 必須符合現有 Serializer contract。

### 10.2 Parser Tests

建立 test-only Resource 或容器，其 property 直接宣告為 generated concrete type。

驗證：

- Parser 可從 concrete property type 建立 generated instance。
- primitive singleton 正確還原。
- primitive collection 正確還原。
- nested datatype 正確還原。
- serialize → parse → serialize 結果一致。

MVP 不加入：

- abstract `DataType` 推斷。
- open choice。
- Extension value type inference。
- generated parser registry。

### 10.3 Validator Tests

驗證現有 object graph walker：

- 能走入 generated datatype。
- 能找到 nested primitive。
- 能產生正確 primitive path。
- invalid primitive 能產生 `PrimitiveFormat` issue。

MVP 不測試或宣稱已支援：

- required rule。
- collection min/max。
- choice rule。
- binding。
- fixed/pattern。
- invariant。

### 10.4 Phase 7 驗收條件

- Serializer、Parser round-trip 通過。
- Validator 能走訪 generated datatype。
- 測試描述不把未生成的 validation semantics 誤列為已支援。

## 11. Phase 8：GeneratedFileWriter 與安全輸出

### 11.1 實作 GeneratedFileWriter

Writer 必須：

- 將 output root 正規化為 absolute path。
- 拒絕 path traversal。
- 拒絕 repository root。
- 拒絕直接輸出到 `core`、`Types`、`Resources`、`Serialization` 或
  `Validation`。
- 使用 UTF-8 without BOM。
- 將 newline 固定為 LF。
- 以固定 ordinal 規則決定檔名與寫入順序。
- 先寫入暫存位置。
- 整批通過後才替換目標輸出。
- 內容未變更時避免重寫檔案。
- 失敗時不留下部分成功的正式輸出。

### 11.2 Writer 測試

- 合法 temporary output directory。
- repository root 被拒絕。
- SDK source directory 被拒絕。
- `..` path traversal 被拒絕。
- UTF-8 without BOM。
- LF newline。
- 相同內容重跑不改變檔案。
- 寫入失敗產生可定位 diagnostic。

### 11.3 Phase 8 驗收條件

- Writer 不會意外覆寫現有手寫模型。
- 整批 generation 具有 all-or-nothing 行為。
- unsafe path 產生 `FSG0011`。

## 12. Phase 9：CLI 與決定性驗證

### 12.1 CLI 參數

支援：

```powershell
dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- `
  --input <structure-definition-file-or-directory> `
  --output <output-directory> `
  --namespace MyFhirSdk.GeneratorFixtures.Types `
  --fhir-version 5.0.0 `
  --type HumanName `
  --type Address
```

必要行為：

- `--input` 必須明確提供。
- `--output` 必須明確提供。
- `--namespace` 必須是合法 C# namespace。
- `--fhir-version` 必須與輸入相符。
- `--type` 可重複指定。
- 未找到指定型別時回報失敗。
- diagnostics 使用穩定且可讀格式輸出。

### 12.2 Exit Code

建議：

| Exit code | 狀況 |
|---|---|
| `0` | 全部成功 |
| `1` | CLI 參數錯誤 |
| `2` | 輸入、檔案或 JSON 錯誤 |
| `3` | Unsupported definition 或 mapping 失敗 |
| `4` | Render 或 compilation 失敗 |
| `5` | 輸出寫入失敗 |

### 12.3 Determinism Test

使用相同 definition 集合，以不同檔案列舉順序執行兩次，驗證：

- 輸出檔名集合一致。
- 輸出檔案順序一致。
- 每個檔案內容 byte-for-byte 一致。
- diagnostics 順序一致。

### 12.4 Phase 9 驗收條件

- CLI smoke test 通過。
- 同一輸入重跑結果完全一致。
- 任一必要型別失敗時，不留下半套正式輸出。
- CLI 不提供會預設覆寫正式 SDK source 的行為。

## 13. 建議 PR 拆分

| PR | 內容 | 主要驗收成果 |
|---|---|---|
| PR 1 | 專案骨架、solution、主專案隔離 | Solution build/test 成功 |
| PR 2 | DTO、Loader、Diagnostics | JSON 載入與輸入錯誤測試 |
| PR 3 | Internal Model、Element Selector、Mapping | Parser unit tests |
| PR 4 | Renderer、HumanName fixture 與 Golden File | 第一個端到端 generation |
| PR 5 | Period、Coding、Address、Identifier | 五份 Golden File |
| PR 6 | Roslyn compilation validation | Generated source 可共同編譯 |
| PR 7 | Serializer、Parser、Validator tests | Runtime contract 通過 |
| PR 8 | Writer、CLI、determinism | 完整 CLI smoke test |

每個 PR 應保持：

- 可獨立 build。
- 現有測試全部通過。
- 不包含與該階段無關的 SDK model 改寫。
- 新增行為都有對應測試。

## 14. MVP 完成條件

全部符合下列條件才算完成：

- CodeGen 是獨立專案，不會被主 SDK 重複編譯。
- 五種 datatype 都能從固定 R5 StructureDefinition fixture 生成。
- 五份 Golden File 經人工審查並通過 exact comparison。
- Generated source 可由 Roslyn 一起編譯。
- Serializer 與 Parser round-trip 測試通過。
- Validator 可以走訪 generated datatype 中的 primitive。
- 相同輸入重跑後輸出檔名及內容完全一致。
- Unsupported definition 會失敗並提供可定位診斷。
- CLI 不會意外覆寫現有手寫模型。
- 一般 `dotnet test` 不會自動修改 Golden File 或 Generated output。
- 文件清楚標示 Resource、Profile、choice、slicing 與 validation metadata 尚未支援。

### 14.1 MVP 驗證紀錄

- 驗證日期：2026-08-12
- 驗證提交：`b5ab83d`（`chore: remove temp file`）
- GitHub Actions：重新執行成功，CI 狀態為綠燈。
- Release build：`dotnet build MyFhirSdk.sln --configuration Release --no-restore`
  通過，0 warnings、0 errors。
- Solution tests：`dotnet test MyFhirSdk.sln --configuration Release --no-build --no-restore`
  通過，共 286 個測試通過、0 個失敗；另有 1 個與 Generator MVP 無關的外部
  Client integration smoke test 略過。
- CodeGen tests：137 個測試全部通過，涵蓋五種 datatype Golden File exact
  comparison、Roslyn compilation validation、Serializer/Parser round-trip、Validator
  traversal、GeneratedFileWriter、CLI 與 determinism。
- CLI smoke test：以固定 R5 fixtures 執行成功，exit code 為 `0`，並產生
  `Period`、`Coding`、`HumanName`、`Address`、`Identifier` 五個 generated source
  檔案。
- 驗證結論：第 14 節列出的 MVP 完成條件全部符合，MVP 狀態確認為
  `Completed`。

## 15. 後續階段

MVP 完成後，再依完整 Generator 設計逐步加入：

1. FHIR package metadata 與 dependency loader。
2. Primitive wrapper generation。
3. Resource 與 BackboneElement generation。
4. Choice type。
5. slicing 與 reslicing。
6. `contentReference`。
7. generated parser registry。
8. Base validation registry。
9. constraint Profile validation metadata。
10. 全 R5 Core Package smoke test。

上述功能不得在 MVP 實作期間隱含加入；若實際 fixture 迫使範圍擴張，應先更新
MVP 規格、測試範圍與完成條件。
