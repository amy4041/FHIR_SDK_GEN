# MyFhirSdk Runtime Phase A：固定 Runtime Contract 實作指引

Version 1.0

- 文件狀態：Proposed
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- 上位架構文件：
  `docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`
- 後續階段：Primitive generation、完整 R5 Models generation、CodeGen local tool 發布

## 1. 文件目的

本文件將上位架構文件中的「Phase A：固定 Runtime contract」拆解為可實作、可測試、
可分批合併的工作項目，定義每項工作的目標、方法、完成標準與驗收方式。

Phase A 的重點是建立穩定邊界並移除 Runtime 對具體 model 名稱的硬編碼，不是開始
生成 primitive 或 Resource。所有現有手寫 models 在本階段保留，作為後續 generation
的 regression oracle。

## 2. Phase A 目標

Phase A 完成後應達成：

1. 明確定義 generated models 可以依賴的最小 public Runtime contract。
2. Primitive 的 value/metadata 機制與 codec/validation 行為具有明確內部契約。
3. Primitive wrapper 不公開 `IsValid()`，使用者只能透過統一 Validator 取得問題。
4. Serializer/Parser 不再以 `FhirDecimal`、`FhirInteger64` 等 C# 類別名稱決定行為。
5. Runtime engine 不再直接維護 concrete R5 Resource/Datatype 的型別清單與規則清單。
6. Model-specific metadata 仍可暫時手寫，但必須透過可替換的 provider/registry 邊界
   提供，讓 Phase B/C 能以 generated metadata 取代。
7. 現有 JSON、validation、Client 與 CodeGen 行為保持相容，全部 regression tests 通過。
8. 不因追求專案拆分而提前形成 Runtime 與 Models 的循環依賴。

## 3. 非目標

Phase A 不包含：

- 將 CodeGen 打包為 .NET local tool。
- 建立 `MyFhirSdk.Runtime.csproj` 或 `MyFhirSdk.R5.Models.csproj` 的實體拆分。
- 生成 primitive wrapper。
- 生成所有 complex datatype、Resource 或 Profile。
- 刪除現有手寫 `Primitives/`、`Types/` 或 `Resources/`。
- 改變 FHIR JSON wire format 或既有 public model shape。
- 完成所有 binding、fixed、pattern、invariant validation。
- 把 HTTP Client 納入 Runtime。

若實作過程發現必須改 public API、FHIR JSON 行為或 model shape，應另立架構決策與
遷移計畫，不得隱含在 internal refactor 中。

## 4. 實作原則與決策閘門

### 4.1 先固定邏輯邊界，再拆 assembly

Phase A 先在現有 solution 中建立 namespace、interface、provider 與測試邊界。只有在
Runtime engine 不再直接依賴 concrete models 後，才評估實體拆分。這可避免 `Element`
引用 `Extension`、`Resource` 引用 `Meta`、`DomainResource` 引用 `Narrative` 所造成的
循環依賴。

### 4.2 Public API 採最小化

Public API 只保留 SDK 使用者與 generated source 必須使用的契約：

- `FhirObject` 及必要的分類 base types。
- `PrimitiveType<T>.Value`、`HasValue` 與必要的 primitive metadata。
- Serializer、Parser、Validator 的公開入口與結果型別。
- generated source 編譯所需的 metadata attribute/contract；只有確定跨 assembly 必須
  使用時才公開。

下列內容維持 `internal`：

- primitive codec、validator、definition 與 registry。
- reflection accessor/cache。
- model metadata provider 的具體實作。
- rule registry 的組裝細節。

### 4.3 不以類別名稱作為行為契約

Runtime 不得以下列方式決定 primitive 或 model 行為：

```csharp
type.Name == "FhirDecimal"
type.Name == "FhirInteger64"
type.FullName == "MyFhirSdk.Types.Coding"
```

應改由明確 descriptor、codec、metadata provider 或 generated registry 決定。

### 4.4 每一步保持可回復與全測試通過

每個工作包應先加入 characterization/contract tests，再替換實作。不得在同一個 PR
同時移除舊路徑、導入未驗證的新 abstraction 並改變輸出格式。

## 5. 目標結構

Phase A 完成後的邏輯結構建議如下；本階段不要求資料夾或 project 完全一致：

```text
Runtime contracts
├─ FhirObject / Base / Element / DataType / Resource
├─ PrimitiveType<T>
├─ IFhirSerializer / IFhirParser / IFhirValidator
└─ result / diagnostic contracts

Runtime engines（internal implementation）
├─ Primitive
│  ├─ IPrimitiveDefinition
│  ├─ IPrimitiveCodec
│  ├─ IPrimitiveValidator
│  └─ PrimitiveDefinitionRegistry
├─ Serialization
│  ├─ FhirJsonSerializer
│  └─ FhirJsonParser
├─ Validation
│  ├─ graph traversal
│  └─ rule execution
└─ Metadata
   ├─ model metadata provider contract
   └─ lookup/cache

R5 model metadata（本階段暫時手寫）
├─ resource type/factory entries
├─ datatype/extension value entries
└─ validation rule entries
```

Phase B/C 將最下層的手寫 R5 metadata 改由 CodeGen 產生，而不更改 Runtime engine。

## 6. Work Package A0：建立基準與 public API inventory

- 實作狀態：Completed（2026-08-14）
- Baseline commit：`d41881d`
- API inventory：`docs/gen/MyFhirSdk_Runtime_Public_API_Inventory.md`
- API snapshot：`Tests/Architecture/ApprovedPublicApi.txt`

### 6.1 目標

在 refactor 前固定目前行為、測試結果與 public surface，讓後續變更可以判斷是預期
遷移還是 regression。

### 6.2 方法

1. 記錄 Release build 與全 solution test baseline。
2. 列出 `core/`、`Serialization/`、`Validation/`、`Primitives/` 中所有 public types
   與 public members。
3. 將 public types 分類：
   - generated source 必須使用；
   - SDK 使用者必須使用；
   - 目前 public、但應 internal；
   - model-specific，未來應生成。
4. 為下列行為補足 characterization tests：
   - primitive singleton 與 primitive array raw/metadata 對齊；
   - `decimal` literal round-trip；
   - `integer64` JSON string round-trip；
   - 每種現有 primitive 的 valid/invalid case；
   - abstract Resource parsing 與 concrete Resource parsing；
   - extension `value[x]` 名稱與型別解析；
   - required、choice 與 primitive format issue path。
5. 建立 public API baseline。可採用 API approval/snapshot 工具，或先使用 repository 中
   可審查的文字清單；重點是 public surface 變更必須在 diff 中可見。

### 6.3 完成標準

- 有一份可審查的 public API inventory。
- 所有即將重構的特殊行為都有測試保護。
- baseline build/test 結果已記錄。
- 沒有 production behavior change。

### 6.4 驗收方式

```powershell
dotnet build MyFhirSdk.sln --configuration Release
dotnet test MyFhirSdk.sln --configuration Release --no-build
```

人工審查 public API inventory，確認每個 public member 都有保留理由或後續處置。

## 7. Work Package A1：固定最小 public Runtime contract

### 7.1 目標

確認 generated models 與 SDK 使用者真正需要的 public contract，避免 internal Runtime
機制在 Phase B/C 被迫成為長期相容 API。

### 7.2 方法

1. 逐一審查：
   - `FhirObject`、`Base`、`Element`、`DataType`；
   - `BackboneType`、`BackboneElement`；
   - `Resource`、`DomainResource`；
   - `PrimitiveType<T>`；
   - `IFhirSerializer`、`IFhirParser`、`IFhirValidator`；
   - validation result 與 exception types。
2. 對每個 member 標記 owner：Runtime contract、R5 model shape 或暫時 bootstrap。
3. 暫不搬移 `Extension`、`Meta`、`Narrative`；在 inventory 中標記為 bootstrap debt。
4. 決定 Validator 的 public scope：
   - Phase A 預設保留 `Validate(Resource)` 以維持相容；
   - 若要新增 `Validate(FhirObject)`，必須先定義 DataType/primitive 單獨驗證的 path、
     null、rule selection 與相容策略，並以新增 overload 優先，避免直接破壞既有 API。
5. 確認不新增 public `IFhirPrimitive.IsValid()`、public codec 或可替換的 primitive
   validator registration API。
6. 新增 architecture/API tests，確保 internal types 不被意外公開。

### 7.3 完成標準

- 最小 public contract 已以程式可見性與 API baseline 固定。
- 每個 bootstrap type 有明確註記，不被誤認為最終 Runtime 邊界。
- `IsValid()`、codec、primitive validator 與內部 registry 不在 public surface。
- 既有使用者程式可繼續編譯；若有例外，具備明確 migration note。

### 7.4 驗收方式

- API snapshot/approval test 通過。
- 建立一個外部 consumer compile test，只能使用 public API，並確認下列程式不可編譯：

```csharp
// 必須不可見或不存在
primitive.IsValid();
PrimitiveRegistry.Register(...);
new DecimalPrimitiveCodec();
```

- 現有 Serializer、Parser、Validator public contract tests 全數通過。

## 8. Work Package A2：建立 primitive definition/codec/validator contract

### 8.1 目標

將 primitive 的共同 value access、JSON codec、format validation 與型別 identity 分開，
建立 Phase B generated wrapper 可以使用、但不會暴露行為實作的 Runtime 邊界。

### 8.2 建議模型

以下為責任示意，不要求直接照抄簽章：

```csharp
internal interface IPrimitiveDefinition
{
    string FhirTypeName { get; }
    Type PrimitiveType { get; }
    Type ValueType { get; }
    IPrimitiveCodec Codec { get; }
    IPrimitiveValidator Validator { get; }
}

internal interface IPrimitiveCodec
{
    // 建立 primitive、讀取 raw JSON、寫出 raw JSON。
}

internal interface IPrimitiveValidator
{
    bool IsValid(object primitive);
}
```

`PrimitiveType<T>` 可在 Runtime assembly 內實作 internal 的非泛型 value accessor，使
derived wrapper 即使未來位於另一個 assembly，也不需要自行實作 internal interface：

```csharp
internal interface IPrimitiveValueAccessor
{
    object? UntypedValue { get; }
    Type ValueType { get; }
}

public abstract class PrimitiveType<T> : DataType, IPrimitiveValueAccessor
{
    public T? Value { get; set; }
    public bool HasValue => Value is not null;

    object? IPrimitiveValueAccessor.UntypedValue => Value;
    Type IPrimitiveValueAccessor.ValueType => typeof(T);
}
```

若 Parser 需要設定 untyped value，應使用受控的 internal accessor 或 codec，不應把
`UntypedValue` setter 公開。

### 8.3 方法

1. 新增 internal primitive contracts 與 registry。
2. 先為 registry 建立現有 primitive definitions，wrapper 暫時不改。
3. 將 primitive 分組實作 codec/validator：
   - string-like：`string`、`markdown`、`code`、`id`、`uri`、`url`、`canonical`；
   - numeric：`integer`、`positiveInt`、`unsignedInt`、`integer64`、`decimal`；
   - temporal：`date`、`dateTime`、`instant`；
   - other：`boolean`、`base64Binary`。
4. 把 wrapper 中的 `IFhirValidatablePrimitive.IsValid()` 邏輯移入 internal validators。
5. `PrimitiveFormatRule` 改為依 registry/definition 執行 validator。
6. `Resource.Id`、`Element.Id` 仍使用相同 `id` validator，不再臨時建立 `FhirId` 並
   cast 至 wrapper interface。
7. 所有 primitive wrapper 保持 `sealed`，並移除 `IFhirValidatablePrimitive` 實作；
   最終刪除該 interface。

### 8.4 完成標準

- Primitive wrapper 不包含 format validation algorithm。
- `IFhirValidatablePrimitive` 已移除，或只保留有明確期限的 deprecated adapter。
- `PrimitiveFormatRule` 不依賴 concrete primitive wrapper interface。
- 所有 primitive definition 都有唯一 FHIR type name、wrapper type、CLR value type、
  codec 與 validator。
- duplicate/missing registration 會 deterministic failure，不能靜默 fallback。
- internal registry 不提供 public replacement API。

### 8.5 驗收方式

- 現有每種 primitive 的 valid/invalid tests 全數通過。
- `Resource.Id`、`Element.Id` issue path 與 message 保持相容。
- 直接建構 invalid primitive 後，以公開 `FhirValidator` 驗證 Resource，仍取得相同
  `PrimitiveFormat` issue。
- API baseline 證明未新增 public `IsValid()`、codec 或 validator。
- 搜尋確認 wrapper 不再實作舊 interface：

```powershell
rg "IFhirValidatablePrimitive|IsValid\(" Primitives
```

預期只允許測試或已核准的過渡 adapter；完成時 production wrappers 應無命中。

## 9. Work Package A3：移除 primitive 類別名稱分支

### 9.1 目標

讓 Serializer/Parser 透過 primitive definition/codec 處理特殊 wire format，不再將 C#
類別名稱當作執行契約。

### 9.2 方法

1. 將下列行為移入 codec：
   - `decimal` 接受 JSON number、保存 literal、以 raw number 寫出；
   - R5 `integer64` 接受/寫出 JSON string、保存 literal；
   - 一般 string/boolean/numeric primitive 的 token kind 與 CLR conversion。
2. `FhirJsonParser.Primitives` 依 primitive type 查 definition，再委派 codec 建立或設定
   wrapper。
3. `FhirJsonSerializer.Primitives` 依 definition/codec 判斷 raw value 是否存在並寫出。
4. `FhirJsonConventions` 只保留通用 property naming、reflection cache 與 JSON convention；
   移除 `TryGetDecimalLiteral`、`TryGetInteger64Literal` 等 concrete-type 分支。
5. Codec 必須區分：
   - JSON token/type error：Parser error；
   - 可解析但不符合 FHIR lexical/value constraint：validation issue。
6. 保留 decimal trailing zero、integer64 string representation 與 null/metadata-only
   primitive 的現有行為。

### 9.3 完成標準

- Runtime production code 不含 `FhirDecimal`、`FhirInteger64` 類別名稱比較。
- 新增特殊 primitive 不需修改 Serializer/Parser 的主流程。
- 每個 codec 對接受與拒絕的 JSON token kind 有明確測試。
- JSON output 與目前 contract byte-for-byte 相容，除非另有核准的 bug fix。

### 9.4 驗收方式

```powershell
rg 'Name.*FhirDecimal|Name.*FhirInteger64|"FhirDecimal"|"FhirInteger64"' `
  Serialization Validation
```

Production code預期無命中。另執行：

- decimal/integer64 parse、serialize、round-trip tests；
- primitive singleton/array alignment tests；
- metadata-only primitive tests；
- malformed JSON token negative tests；
- 全部 Parser/Serializer tests。

## 10. Work Package A4：分離 model metadata provider 與 Runtime engine

### 10.1 目標

讓 Parser、Serializer 與 Validator 只依賴 metadata/provider contract，不直接掃描或
列舉 concrete R5 models。Phase A 仍可使用手寫 provider；Phase B/C 再以 generated
provider 取代。

### 10.2 需分離的現況

- `FhirJsonParser.ResourceTypesByName` 掃描 `typeof(Resource).Assembly`。
- `FhirJsonParser.ComplexDataTypes` 掃描現有 assembly。
- `ResolveDataType` 對 `Coding` 與 property name 有特例。
- `FhirExtensionValuePropertyNames` 直接引用 `SimpleQuantity` 並掃描 assembly。
- `ResourceRuleRegistry.CreateDefault()` 直接列舉 `Bundle`、`Claim`、`Patient` 等型別。

### 10.3 建議 provider 能力

Provider contract 應依使用案例拆分，避免建立無限制的 service locator。概念能力包括：

```text
Resource type name  ──► concrete CLR Type / factory
Declared property  ───► concrete datatype/choice metadata
Extension value[x] ───► CLR Type and JSON property name
CLR model Type     ───► validation rules/metadata
```

可採單一 facade 搭配數個小 registry，或直接注入小型介面。簽章在實作前以 spike 與
tests 決定，但必須符合：

- Runtime engine 不引用 `MyFhirSdk.Resources` 或 `MyFhirSdk.Types` concrete classes。
- 缺少、重複與衝突 metadata 有明確錯誤。
- lookup 使用 ordinal comparison 且結果 deterministic。
- provider 的 model entries 可在 Phase C 由 CodeGen 生成。

### 10.4 方法

1. 先抽出 resource type/factory provider，保留目前掃描邏輯於手寫 R5 provider。
2. Parser 改為依 provider 解析 abstract Resource。
3. 抽出 datatype/choice resolution metadata，移除 `security`、`tag`、`Coding` 特例。
4. 抽出 extension value property provider，移除 Runtime 對 `SimpleQuantity` 的引用。
5. 將 `ResourceRuleRegistry` 分成：
   - Runtime rule execution/lookup；
   - R5 model-specific rule entries provider。
6. `FhirValidator`、`FhirJsonParser` 透過 internal constructor 接收 provider，public
   constructor 使用目前的 default R5 provider。
7. 為 tests 建立 fake provider，證明 Runtime engine 能處理未在現有手寫 model 清單
   中硬編碼的測試型別。

### 10.5 完成標準

- Runtime engine namespace 不直接 `using MyFhirSdk.Resources` 或具體 `Types`。
- Parser/Validator 主流程不包含 `typeof(Patient)`、`typeof(Bundle)`、
  `typeof(SimpleQuantity)` 等 model-specific 參考。
- model-specific entries 集中在可被 generated provider 替換的位置。
- Runtime engine 測試可使用 fake metadata provider，不需修改 static global state。
- provider 初始化為 immutable，避免使用者在 runtime 任意替換內建 FHIR 規格。

### 10.6 驗收方式

- Resource abstract/concrete parse tests 通過。
- contained Resource、DataType、Extension `value[x]` tests 通過。
- required/choice/cardinality/primitive validation tests 通過。
- fake provider contract tests 通過。
- 搜尋 Runtime engine 的 concrete model references；只允許集中式手寫 R5 provider
  在過渡期命中。

## 11. Work Package A5：加入架構與契約測試

### 11.1 目標

用自動化測試防止未來功能開發重新把 concrete model、public primitive validation 或
類別名稱特例塞回 Runtime engine。

### 11.2 方法

新增或擴充下列測試：

1. Public API approval test：偵測 public surface 變更。
2. Accessibility test：codec、validator、definition、registry 不可由外部 assembly 存取。
3. Dependency/architecture test：Runtime engine 不直接引用 concrete R5 Resources/Types。
4. Primitive registration test：完整、唯一且 deterministic。
5. Codec conformance test：各 primitive JSON token、parse/write/round-trip matrix。
6. Validation contract test：使用者只經 `FhirValidator` 取得 primitive issue。
7. Metadata provider contract test：unknown、duplicate、conflict、factory failure。
8. Existing generated datatype runtime contract test：確保 MVP generated datatype 仍可
   serialize、parse、validate。

Architecture test 可以使用 assembly metadata/reflection，或採用小型 dependency testing
library；避免只用易受註解或測試文字影響的 `rg` 作為唯一 CI gate。`rg` 適合人工與
PR 快速檢查，但正式規則應盡量由編譯或測試強制。

### 11.3 完成標準

- 每項關鍵邊界至少有一個會在違反時失敗的自動化測試。
- 測試不依賴網路、目前時間或未固定的 assembly enumeration order。
- 一般 `dotnet test` 可執行所有 Phase A contract tests。

### 11.4 驗收方式

- 刻意建立測試分支違反規則，確認相應 test 會失敗，再撤回違規變更。
- Release configuration 執行全 solution tests。
- CI 在 Windows/Linux 至少現有目標平台上結果一致。

## 12. Work Package A6：清理、文件與 Phase B handoff

### 12.1 目標

移除過渡 adapter、記錄最終 contract，並提供 Phase B primitive generation 所需的明確
輸入邊界。

### 12.2 方法

1. 移除未再使用的 interface、reflection helper、類別名稱特例與 static registry。
2. 更新上位責任邊界文件中已確定的 Proposed 決策。
3. 記錄 primitive definition matrix：
   - FHIR type name；
   - wrapper type；
   - CLR backing type；
   - JSON representation；
   - codec key；
   - validator key；
   - literal preservation requirement。
4. 定義 Phase B generation policy schema，但不在 Phase A 生成 wrapper。
5. 記錄 bootstrap debt：`Extension`、`Meta`、`Narrative` 及 base model property 的暫時
   歸屬和後續決策點。
6. 更新 README/開發文件，說明 public validation 入口和 internal primitive 行為。

### 12.3 完成標準

- Production code 不含無期限的 transitional adapter。
- Primitive matrix 足以讓 Phase B 實作 mapper/renderer，不需重新猜測 Runtime 行為。
- 所有 bootstrap debt 都有 owner、原因與後續階段。
- 上位文件、實作與 tests 對 Runtime contract 的描述一致。

### 12.4 驗收方式

- 文件 review 加上 Runtime、Serialization、Validation、CodeGen 四個角度的 checklist。
- 全 solution build/test 通過。
- 以一個手寫的薄 wrapper 模擬 Phase B output，證明只依賴 public Runtime contract 即可
  編譯，並能被 Runtime serializer/parser/validator 處理。

## 13. 建議實作順序與 PR 拆分

PR、branch 與 commit 說明皆以本文件的 Work Package 編號為準，避免另設一套 PR 編號。

| Work Package | 範圍 | 必要成果 |
|---|---|---|
| A0 | Baseline、public API inventory、characterization tests | 無 production behavior change |
| A1 | 最小 public contract 與 accessibility tests | public/internal 邊界固定 |
| A2 | Primitive definition、codec、validator contract | wrappers 不再擁有 validation algorithm |
| A3 | Primitive codec migration | 無 primitive 類別名稱分支 |
| A4 | Model metadata provider | Runtime engine 與 concrete R5 entries 分離 |
| A5 | Architecture 與 contract tests | 關鍵 Runtime 邊界皆有自動化測試保護 |
| A6 | Cleanup、文件與 Phase B handoff | Phase A 驗收條件全部通過 |

每個 PR 必須：

- 可獨立 build/test。
- 不同時混入 primitive generation 或 Resource generation。
- 說明 public API 是否變更。
- 列出新增/移除的 temporary adapter。
- 提供對應的 automated tests。

## 14. Phase A 完成標準

全部符合才算 Phase A 完成：

- 有核准的 public Runtime API baseline。
- `FhirObject`、base types、`PrimitiveType<T>` 及公開 service contract 的 owner 明確。
- Primitive wrapper 不公開或實作 `IsValid()` contract。
- Primitive codec、validator、definition 與 registry 保持 internal。
- Serializer/Parser 不以 `FhirDecimal`、`FhirInteger64` 類別名稱分支。
- Runtime engine 不直接列舉 concrete R5 Resources/Types；model entries 經 provider 提供。
- Validation rule engine 與 R5 model-specific rule entries 已分離。
- 使用者不能透過 public API 替換內建 primitive validator。
- 使用者經統一 Validator 可取得 primitive format issue。
- 現有手寫 models 未刪除，並繼續作為 regression oracle。
- MVP generated datatype runtime contract 仍通過。
- 所有 solution tests 通過，Release build 為 0 errors。
- Phase B 所需 primitive definition matrix 與 generation policy boundary 已記錄。

## 15. 最終驗收流程

### 15.1 自動化驗收

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln --configuration Release --no-restore
dotnet test MyFhirSdk.sln --configuration Release --no-build --no-restore
```

另執行：

- public API approval tests；
- architecture/dependency tests；
- primitive codec/validator matrix tests；
- generated datatype runtime contract tests；
- CLI MVP smoke test，確認 Phase A 未破壞既有 Generator。

### 15.2 靜態快速檢查

以下指令只作為 review 輔助，不取代 automated architecture tests：

```powershell
rg '"FhirDecimal"|"FhirInteger64"' Serialization Validation
rg 'IFhirValidatablePrimitive|IsValid\(' Primitives
rg 'using MyFhirSdk.Resources|typeof\((Patient|Bundle|Claim)' Serialization Validation
```

完成狀態的 production code 應無前兩類命中；第三類只允許在集中式、明確標示為
Phase A 過渡的 R5 metadata provider 中命中。

### 15.3 人工驗收情境

至少人工確認：

1. SDK 使用者建立含 invalid primitive 的 Resource。
2. 使用者只能呼叫 `FhirValidator`，不能呼叫 primitive `IsValid()`。
3. Validator 回傳正確 path、code、severity 與 message。
4. `decimal` trailing zero、`integer64` JSON string、primitive metadata array 均能
   round-trip。
5. 以 fake/generated-style metadata provider 加入測試型別時，不需修改 Serializer、
   Parser 或 Validator 主流程。

## 16. 風險與對策

| 風險 | 對策 |
|---|---|
| Internal abstraction 過度設計 | 只從現有 decimal、integer64、validation、registry 使用案例抽取 |
| Refactor 改變 JSON 格式 | 先建立 byte-level/semantic round-trip characterization tests |
| Registry 改造產生 static global state | 使用 immutable provider 與 internal constructor injection |
| Public API 意外擴張 | API approval test，codec/validator 全部 internal |
| Runtime/Models 過早拆分造成循環依賴 | Phase A 只固定邏輯邊界，保留 bootstrap types |
| 手寫 provider 變成永久方案 | Phase B/C 明確以 generated provider 取代，並記錄 debt |
| Primitive parse error 與 validation issue 混淆 | 為 token type error及 lexical/value error建立不同測試 |
| Reflection 行為依 assembly 順序 | ordinal sort、duplicate detection、immutable deterministic registry |

## 17. 文件編排建議

建議維持下列層次，避免單一文件同時承擔架構、實作與操作說明：

```text
MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md
└─ 架構原則、責任與依賴方向

MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md
└─ 本文件：Runtime contract refactor 的實作與驗收

MyFhirSdk_Primitive_Generation_Phase_B_Implementation_Guide.md（後續）
└─ primitive policy、model、renderer、generated registry

MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md（後續）
└─ full datatype/Resource/dependency graph generation

MyFhirSdk_CodeGen_Local_Tool_Release_Guide.md（後續）
└─ PackAsTool、manifest、versioning、publish/upgrade smoke test
```

若 Phase A 實作過程出現會長期影響 public API 或 assembly dependency 的選擇，建議另建
簡短 ADR，例如：

```text
docs/gen/adr/
├─ 0001-runtime-public-contract.md
├─ 0002-primitive-definition-and-codec.md
└─ 0003-model-metadata-provider.md
```

ADR 記錄「選了什麼、為什麼、替代方案及後果」；本指引則持續記錄「如何實作與驗收」。
