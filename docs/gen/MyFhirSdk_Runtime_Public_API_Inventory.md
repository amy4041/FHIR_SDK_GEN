# MyFhirSdk Runtime Public API Inventory

Version 1.0

- 文件狀態：Baseline
- Baseline commit：`d41881d`
- Baseline branch：`feat/runtime-phase-a0-baseline`
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- 對應工作：Runtime Phase A / Work Package A0
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

## 7. Models、Client 與其他公開 API

| API 群組 | 分類 | 後續處置 |
|---|---|---|
| `MyFhirSdk.Types.*` | Model-specific | Phase C 由 CodeGen 生成 |
| `MyFhirSdk.Resources.*` | Model-specific | Phase C 由 CodeGen 生成 |
| `MyFhirSdk.Client.*` | Separate package | 不屬於最小 Runtime，長期拆為 Client package |
| `ImplementationGuides.*` | Separate package / Model-specific | 不屬於最小 Runtime |

Public API snapshot 涵蓋 `Core`、`Primitives`、`Serialization` 與 `Validation` namespaces，
對應 Phase A 的 Runtime surface。Models、Client 與 Implementation Guides 仍由既有
功能測試保護，不納入本次 Runtime contract approval scope。

## 8. Characterization coverage matrix

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

## 9. A0 變更規則

- A0 不修改 production behavior。
- Snapshot 更新必須與刻意的 public API 決策同一個 PR，並在 PR 說明差異。
- Characterization test 只固定現有合約；若測試揭露 bug，先記錄問題，修正另開工作項目。
- Phase A 後續工作包執行後，持續以本 inventory 與 snapshot 判斷相容性。

## 10. A0 完成驗證

- 驗證日期：2026-08-14
- Release build：0 warnings、0 errors。
- Solution tests：292 passed、0 failed、1 skipped。
- Architecture tests：2 passed，包含 public API snapshot 與 primitive validation
  accessibility contract。
- Parser tests：18 passed，包含 A0 新增的 4 個 characterization cases。
- CodeGen tests：137 passed，MVP generated datatype contract 未受影響。
- Production code：無異動。
