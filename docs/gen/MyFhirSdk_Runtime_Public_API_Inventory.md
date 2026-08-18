# MyFhirSdk Runtime Public API Inventory

Version 1.2

- 文件狀態：Phase A minimum contract fixed
- Baseline commit：`d41881d`
- A1 起始 commit：`a9da211`
- A2 起始 commit：`b603161`
- 目前 branch：`feat/runtime-phase-a2-primitive-contract`
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- 對應工作：Runtime Phase A / Work Package A0、A1、A2
- 實作指引：
  `docs/gen/MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md`

## 1. 基準驗證結果

在 production code 尚未進行 Phase A refactor 前，以 Release configuration 驗證：

| 項目 | 結果 |
|---|---|
| `dotnet build MyFhirSdk.sln --configuration Release --no-restore` | 0 warnings、0 errors |
| Solution tests | 286 passed、0 failed、1 skipped |
| CodeGen tests | 137 passed、0 failed |
| 略過項目 | 外部 Client integration smoke test |

`Tests/Architecture/ApprovedPublicApi.txt` 是此 baseline 的機器可比較 public API
snapshot。任何 public type/member 變更都必須更新 snapshot，並在 PR 中說明相容性與
遷移方式。

目前 snapshot 比較 exported type、base type、public interface、constructor、method、
property、field 與 event 的反射簽章。Nullable annotation 與 generic constraint 尚未納入
文字格式；若 Phase A 後續需要調整這兩類 contract，必須另加針對性 compile/API test，
或改採 Roslyn public API analyzer。

## 2. 分類定義

| 分類 | 定義 |
|---|---|
| Runtime contract | Generated models 編譯或 Runtime engine 運作所需的穩定契約 |
| SDK public API | SDK 使用者應直接呼叫或讀取的公開功能 |
| Bootstrap debt | 目前為了解決基底相依暫留，最終歸屬仍需決策 |
| Model-specific | 由 R5 規格決定，Phase B/C 應生成或移至 R5 Models |
| Internal candidate | 現在 public，但尚無外部使用理由，應在後續工作包評估收窄 |
| Separate package | 不屬於最小 Runtime，長期應由其他 package 負責 |

本文件先以 type/責任群組分類；完整 member signature 以 approved snapshot 為準。

## 3. Core public API

| API | 分類 | 理由與後續處置 |
|---|---|---|
| `FhirObject` | Runtime contract | 所有 FHIR object 的共同根型別 |
| `Base` | Runtime contract | 現有 model hierarchy 的共同分類基底 |
| `Element` | Runtime contract + Bootstrap debt | `Id`/`Extension` 同時涉及 primitive metadata 與 R5 shape |
| `DataType` | Runtime contract | Generated complex datatype 與 primitive 的分類基底 |
| `PrimitiveType<T>` | Runtime contract | 提供 `Value`、`HasValue` 與共同 primitive shape |
| `BackboneType` | Runtime contract + Bootstrap debt | 分類契約保留，`ModifierExtension` 歸屬待決 |
| `BackboneElement` | Runtime contract + Bootstrap debt | 分類契約保留，`ModifierExtension` 歸屬待決 |
| `Resource` | Runtime contract + Bootstrap debt | `ResourceType` 是 contract；R5 properties 需後續分類 |
| `DomainResource` | Runtime contract + Bootstrap debt | 分類契約保留；Narrative/Contained/Extension shape 待決 |
| `IFhirExtensionValue` | Runtime contract candidate | Parser/Serializer 用於 extension choice；Phase A 確認最小需求 |
| `FhirSdkException` | SDK public API | 公開的 parse/runtime failure contract |
| `Extension` | Bootstrap debt / Model-specific | 規格 shape 原則上屬 R5 Models，目前 base hierarchy 直接依賴 |
| `Meta` | Bootstrap debt / Model-specific | R5 structure，目前由 `Resource.Meta` 直接引用 |
| `Narrative` | Bootstrap debt / Model-specific | R5 structure，目前由 `DomainResource.Text` 直接引用 |

### 3.1 A1 Core member ownership

| Type/member | Owner | A1 決策 |
|---|---|---|
| `FhirObject` type | Runtime contract | 保留 public abstract，作為所有 FHIR object 的穩定根型別 |
| `Base` type | Runtime contract | 保留 public abstract，作為 generated type hierarchy 的分類基底 |
| `Element` type | Runtime contract | 保留 public abstract，generated datatype 必須可繼承 |
| `Element.Id`、`Element.Extension` | R5 model shape / Bootstrap debt | Phase A 保留相容；未宣告為最終最小 Runtime 資料成員 |
| `DataType` type 與 `IFhirExtensionValue` | Runtime contract | Generated datatype 與 extension `value[x]` 需要此分類契約 |
| `BackboneType`、`BackboneElement` type | Runtime contract | 保留 public abstract，供 generated model hierarchy 繼承 |
| `BackboneType.ModifierExtension`、`BackboneElement.ModifierExtension` | R5 model shape / Bootstrap debt | Phase A 暫留，待 model assembly 邊界確定後移交 |
| `Resource` type、`Resource.ResourceType` | Runtime contract | Generated resource 必須繼承並提供穩定的 FHIR type identity |
| `Resource.Id`、`Meta`、`ImplicitRules`、`Language` | R5 model shape / Bootstrap debt | Phase A 保留相容，不視為 Runtime engine implementation API |
| `DomainResource` type | Runtime contract | Generated domain resources 的分類基底 |
| `DomainResource.Text`、`Contained`、`Extension`、`ModifierExtension` | R5 model shape / Bootstrap debt | Phase A 暫留，最終由 R5 Models 擁有 |
| `PrimitiveType<T>` type 與 protected constructors | Runtime contract | Generated primitive wrapper 必須能在外部 assembly 繼承與建構 |
| `PrimitiveType<T>.Value`、`HasValue` | Runtime contract | Runtime parser、serializer、validator 與 generated wrapper 的共同 value contract |
| `PrimitiveType<T>.ToString()` | SDK compatibility API | Phase A 保留既有行為；primitive wire format 不得依賴此方法 |
| `Extension`、`Meta`、`Narrative` declarations | R5 model shape / Bootstrap debt | Phase A 不搬移、不複製；離開 bootstrap 前須由 model provider/assembly 接手 |

上述「Runtime contract」會由 API snapshot 與 external consumer compilation test 保護；
「Bootstrap debt」只代表目前 public 且必須維持相容，不代表其 declaration 最終應留在
`MyFhirSdk.Runtime` assembly。

## 4. Primitive public API

目前所有 primitive wrapper 都是 public sealed classes，具有 public 無參數/value
constructor，並繼承 `PrimitiveType<T>`。

| API 群組 | 分類 | Phase A/Phase B 處置 |
|---|---|---|
| `FhirBoolean`、`FhirInteger` | Model-specific wrapper | Phase A 保留；Phase B 生成 declaration |
| `FhirPositiveInt`、`FhirUnsignedInt` | Model-specific wrapper | Phase A 保留；Phase B 生成 declaration |
| `FhirInteger64`、`FhirDecimal` | Model-specific wrapper + 特殊 runtime 行為 | declaration 生成；literal/codec 留 Runtime |
| `FhirString`、`FhirMarkdown`、`FhirCode`、`FhirId` | Model-specific wrapper | declaration 生成；format validator 留 Runtime |
| `FhirUri`、`FhirUrl`、`FhirCanonical` | Model-specific wrapper | declaration 生成；format validator 留 Runtime |
| `FhirDate`、`FhirDateTime`、`FhirInstant` | Model-specific wrapper + 特殊 runtime 行為 | declaration 生成；temporal codec/validator 留 Runtime |
| `FhirBase64Binary` | Model-specific wrapper + 特殊 runtime 行為 | declaration 生成；base64 validator 留 Runtime |

`IFhirValidatablePrimitive` 已是 internal，不在 public snapshot。Phase A 後續將驗證演算法
移至 internal Runtime validator/definition，wrapper 不增加 public `IsValid()`。

## 5. Serialization public API

| API | 分類 | 理由與後續處置 |
|---|---|---|
| `IFhirSerializer` | SDK public API | 對外 serialization contract |
| `IFhirParser` | SDK public API | 對外 parsing contract |
| `FhirJsonSerializer` | SDK public API | JSON serializer concrete implementation |
| `FhirJsonParser` | SDK public API | JSON parser concrete implementation |

A1 固定 `IFhirSerializer.Serialize<TResource>(TResource)` 與
`IFhirParser.Parse<TResource>(string)`，並保留 `where TResource : Resource` generic
constraint。這些是 SDK 使用者入口，也是 Runtime 與 generated Resource 的交界。

下列實作細節必須保持 internal：JSON conventions、primitive codec、reflection cache、
Resource/Datatype resolution、Extension `value[x]` lookup。

## 6. Validation public API

| API | 分類 | 理由與後續處置 |
|---|---|---|
| `IFhirValidator`、`FhirValidator` | SDK public API | 使用者的統一 validation 入口 |
| `ValidationResult`、`ValidationIssue` | SDK public API | 結構化 validation output |
| `ValidationIssueCode`、`ValidationSeverity` | SDK public API | 穩定的 issue classification |
| `ValidationRuleSource` | SDK public API | issue/rule 來源資訊 |
| Profile validation public types | SDK public API candidate | 屬 Profile subsystem，Phase A 保持相容 |
| `IFhirValidationRule` | Internal candidate | 目前位於 internal namespace responsibility，需確認外部擴充需求 |
| `CardinalityRule`、`ChoiceElementRule<T>`、`RequiredFieldRule<T>` | Internal candidate | 通用 engine 可保留，但不應無意形成外部規則 API |
| `ValidationValuePresence` | Internal candidate | Runtime helper，不是使用者 contract |
| `FhirObjectGraphWalker`、`FhirPathFormatter` | Internal candidate | traversal implementation，不是公開 SDK 入口 |

目前部分列為 internal candidate 的 type 在 C# 可見性上已是 internal；snapshot 只會列出
真正 exported types。分類表保留它們是為了後續 architecture review。

### 6.1 A1 Validator scope 決策

- 最小 public validation 入口固定為
  `ValidationResult IFhirValidator.Validate(Resource resource)`。
- `FhirValidator` 維持相同的 `Validate(Resource)` concrete API。
- A1 不新增 `Validate(FhirObject)`。DataType/primitive 單獨驗證的 path、null handling、
  rule selection 尚未形成穩定語意；未來若完成設計，應以新增 overload 評估。
- `ValidationResult`、`ValidationIssue`、issue enums 與 `FhirSdkException` 保留為 SDK
  public result/error contract。
- Profile validation API 為既有相容 surface，但不屬於 generated R5 Models 所需的最小
  Runtime contract；未來可獨立封裝，不在 A1 收窄 accessibility。

## 7. A1 internal implementation boundary

下列能力不得成為 exported/public API：

- primitive 自我驗證介面或 `IsValid()`；
- primitive definition、codec、validator 與 registry；
- 可由 SDK 使用者替換內建 primitive validator 的 registration API；
- JSON convention、reflection cache、resource/datatype resolution helper；
- object graph traversal 與內建 base validation rules。

A1 不使用 `InternalsVisibleTo` 將上述能力暴露給 R5 Models。Generated models 只能依賴
本文件標記的 public/protected Runtime contract。A2 新增 primitive internal contracts 時，也必須
符合這項 boundary。

## 8. Models、Client 與其他公開 API

| API 群組 | 分類 | 後續處置 |
|---|---|---|
| `MyFhirSdk.Types.*` | Model-specific | Phase C 由 CodeGen 生成 |
| `MyFhirSdk.Resources.*` | Model-specific | Phase C 由 CodeGen 生成 |
| `MyFhirSdk.Client.*` | Separate package | 不屬於最小 Runtime，長期拆為 Client package |
| `ImplementationGuides.*` | Separate package / Model-specific | 不屬於最小 Runtime |

Public API snapshot 涵蓋 `Core`、`Primitives`、`Serialization` 與 `Validation` namespaces，
對應 Phase A 的 Runtime surface。Models、Client 與 Implementation Guides 仍由既有
功能測試保護，不納入本次 Runtime contract approval scope。

## 9. Characterization coverage matrix

| 行為 | A0 狀態 | 驗證位置 |
|---|---|---|
| Primitive singleton raw value/metadata | 已覆蓋 | Serializer/Parser primitive fixtures |
| Metadata-only primitive | A0 補強 | `FhirJsonParserCharacterizationTests` |
| Primitive array raw/metadata alignment | 已覆蓋 | primitive array alignment fixtures |
| Decimal literal/trailing zero | 已覆蓋 | Practitioner decimal fixture |
| Integer64 JSON string/literal | 已覆蓋 | Practitioner integer64 fixture |
| Integer64 錯誤 JSON number | A0 補強 | `FhirJsonParserCharacterizationTests` |
| Decimal 錯誤 JSON string | A0 補強 | `FhirJsonParserCharacterizationTests` |
| Abstract Resource resolution | A0 補強 | `FhirJsonParserCharacterizationTests` |
| Concrete Resource parsing | 已覆蓋 | Parser fixture tests |
| Extension `value[x]` | 已覆蓋 | extension value fixtures |
| Primitive valid/invalid format | 已覆蓋 | `PrimitiveFormatRuleTests` |
| Required/choice issue path | 已覆蓋 | Validation rule tests |
| Generated datatype runtime contract | 已覆蓋 | CodeGen runtime contract tests |
| Public API change detection | A0 新增 | Architecture public API snapshot test |
| External generated model/consumer compile | A1 新增 | `RuntimeContractCompilationTests` |
| Internal primitive API compile rejection | A1 新增 | `RuntimeContractCompilationTests` |
| Validator remains Resource-scoped | A1 新增 | `RuntimeContractAccessibilityTests` |
| Internal implementation not exported | A1 新增 | `RuntimeContractAccessibilityTests` |
| Primitive definition matrix | A2 新增 | `PrimitiveRuntimeContractTests` |
| Primitive codec JSON round-trip matrix | A2 新增 | `PrimitiveRuntimeContractTests` |
| Primitive validator valid/invalid matrix | A2 新增 | `PrimitiveRuntimeContractTests` |
| Duplicate/missing registration failure | A2 新增 | `PrimitiveRuntimeContractTests` |
| Resource/Element id path and message | A2 補強 | `PrimitiveFormatRuleTests` |

## 10. Phase A contract 變更規則

- A0 不修改 production behavior。
- Snapshot 更新必須與刻意的 public API 決策同一個 PR，並在 PR 說明差異。
- Characterization test 只固定現有合約；若測試揭露 bug，先記錄問題，修正另開工作項目。
- Phase A 後續工作包執行後，持續以本 inventory 與 snapshot 判斷相容性。
- A1 固定的 minimum contract 若需修改，必須同時更新 API/compile tests，並說明 breaking
  change 或相容策略。
- Bootstrap debt 的 public API 在正式移交 R5 Models 前仍受相容性保護，不能只因為不屬於
  最終 Runtime owner 就直接移除。

## 11. A0 完成驗證

- 驗證日期：2026-08-14
- Release build：0 warnings、0 errors。
- Solution tests：292 passed、0 failed、1 skipped。
- Architecture tests：2 passed，包含 public API snapshot 與 primitive validation
  accessibility contract。
- Parser tests：18 passed，包含 A0 新增的 4 個 characterization cases。
- CodeGen tests：137 passed，MVP generated datatype contract 未受影響。
- Production code：無異動。

## 12. A1 contract 決策摘要

- A1 未新增或移除 production public API，approved snapshot 維持不變。
- Generated models 的最小依賴固定為 core hierarchy、`PrimitiveType<T>`、
  `IFhirExtensionValue` 與 `ResourceType` contract。
- SDK 使用者入口固定為 Parser、Serializer、Resource-scoped Validator，以及結構化
  validation result/error contract。
- Primitive validation、codec、definition、validator、registry 維持 internal；使用者只從
  `FhirValidator` 取得 primitive validation issues。
- `Extension`、`Meta`、`Narrative` 及 base class 上的 R5 properties 明確登記為 bootstrap
  debt，A1 不提前搬移。
- 既有 Profile validation public surface 為相容 API，不納入 generated models 的最小依賴。

## 13. A1 完成驗證

- 驗證日期：2026-08-17。
- Release build：0 warnings、0 errors。
- Solution tests：299 passed、0 failed、1 skipped。
- Architecture tests：9 passed，包含 public API snapshot、external generated
  model/consumer compilation、internal API compile rejection、Validator scope 與 exported
  implementation boundary。
- Parser、Serializer、Validation 與 CodeGen tests 全數通過。
- Production code：無異動；approved public API snapshot 無變更。

## 14. A2 primitive Runtime contract

A2 新增下列 internal contracts；全部位於 `MyFhirSdk.Primitives`，不屬於 exported API：

- `IPrimitiveValueAccessor`：由 `PrimitiveType<T>` 顯式實作，提供受控的 untyped value
  讀寫及 CLR value type，不要求外部 generated wrapper 實作 internal interface。
- `IPrimitiveDefinition`：連結 FHIR type name、wrapper type、CLR value type、codec 與
  validator。
- `IPrimitiveCodec`：建立 wrapper、判斷 raw value、讀寫 JSON primitive value。
- `IPrimitiveValidator`：驗證 wrapper 或 base property 的 raw value。
- `PrimitiveRegistry`：以 FHIR type name 或 wrapper CLR type 查找唯一 definition。

Default registry 固定以下 17 筆 definition：

| FHIR type | Wrapper | CLR value | Codec group |
|---|---|---|---|
| `base64Binary` | `FhirBase64Binary` | `string` | string |
| `boolean` | `FhirBoolean` | `bool?` | boolean |
| `canonical` | `FhirCanonical` | `string` | string |
| `code` | `FhirCode` | `string` | string |
| `date` | `FhirDate` | `string` | string |
| `dateTime` | `FhirDateTime` | `string` | string |
| `decimal` | `FhirDecimal` | `decimal?` | literal-preserving JSON number |
| `id` | `FhirId` | `string` | string |
| `instant` | `FhirInstant` | `string` | string |
| `integer` | `FhirInteger` | `int?` | JSON number |
| `integer64` | `FhirInteger64` | `long?` | literal-preserving JSON string |
| `markdown` | `FhirMarkdown` | `string` | string |
| `positiveInt` | `FhirPositiveInt` | `int?` | JSON number |
| `string` | `FhirString` | `string` | string |
| `unsignedInt` | `FhirUnsignedInt` | `int?` | JSON number |
| `uri` | `FhirUri` | `string` | string |
| `url` | `FhirUrl` | `string` | string |

Registry construction 對 duplicate FHIR type name、duplicate wrapper type 直接拋出
`InvalidOperationException`；required lookup 缺少 registration 時拋出
`KeyNotFoundException`，不使用 fallback。

## 15. A2 migration boundary

- 所有 primitive wrapper 已移除 `IFhirValidatablePrimitive` 與 `IsValid()` 實作。
- `IFhirValidatablePrimitive` 已刪除；format algorithms 集中於 internal validators。
- `PrimitiveFormatRule` 透過 registry/definition 驗證，不再依賴 concrete wrapper
  validation interface。
- `Resource.Id` 與 `Element.Id` 直接使用 `id` definition 的 raw-value validator，不再暫時
  建立 `FhirId`。
- Parser/Serializer 在 A2 尚未切換至 registry codec；A3 將以這次建立的 codec contract
  移除 `FhirDecimal`、`FhirInteger64` 類別名稱分支。
- A2 修正既有 base64 validator 使用零長度 destination buffer 的問題；合法非空
  base64（例如 `QQ==`）現在可正確通過公開 `FhirValidator`。
- Production public API 沒有新增或移除，approved snapshot 維持不變。

## 16. A2 完成驗證

- 驗證日期：2026-08-17。
- Release build：0 warnings、0 errors。
- Solution tests：354 passed、0 failed、1 skipped。
- Architecture tests：62 passed，包含 17 筆 definition matrix、17 組 codec JSON
  round-trip、32 組 validator valid/invalid cases，以及 duplicate/missing registration。
- Validation tests：72 passed，包含 Resource/Element `id` path/message 相容與合法
  base64 regression case。
- Parser、Serializer 與 CodeGen tests 全數通過；A2 未提前執行 A3 codec migration。
- `ApprovedPublicApi.txt` 無變更，public API snapshot test 通過。
- 所有 17 個 production wrapper 皆維持 `sealed`，且不再包含 `IsValid()` 或 regex
  validation algorithm。
