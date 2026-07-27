# FHIR SDK Generator Validation Generation 討論紀錄

Version 0.1

- 文件狀態：Discussion / Non-binding

- 適用範圍：FHIR R5、MyFhirSdk Generator 後續階段

- 決策狀態：Deferred，完成 Generator MVP 後再選擇實作方向

## 1. 文件目的

本文件整理 FHIR SDK Generator 未來產生 validation metadata 與 validation rules
的可能方向、取捨、現有 SDK 接點及待確認問題。

本文件只是討論紀錄，不代表已決定：

- metadata schema。

- generated artifact 格式。

- Validator public API。

- Base FHIR 與 Profile 的最終執行策略。

- 是否直接生成 typed validation rules。

目前 Generator MVP 仍以 general-purpose complex datatype 的 model generation、
Golden File、編譯及 runtime contract 測試為優先。本文件中的方案不應擴大 MVP
範圍。

## 2. 討論背景

StructureDefinition 中有許多資訊不能只靠 C# property shape 完整表達，例如：

- `min` / `max` cardinality。

- choice element 互斥與必要性。

- allowed type、profile 與 targetProfile。

- fixed value 與 pattern value。

- terminology binding。

- FHIRPath invariant。

- slicing 與 slice cardinality。

- `mustSupport`、`isModifier`、obligation 與其他非 instance-validation metadata。

Base C# model 應表達一個 FHIR type 可承載的資料形狀；Base Definition 或 Profile
對該形狀施加的限制，可能需要 metadata、validation rule 或外部 validation engine
才能執行。

因此後續 Generator 需要回答：

```text
StructureDefinition constraint
    ↓
要保留成什麼 normalized representation？
    ↓
要生成宣告式 metadata、可執行 rule，或兩者？
    ↓
現有 FhirValidator / ProfileValidator 如何載入並執行？
```

## 3. 名詞

### 3.1 Validation Metadata

Validation metadata 是對限制條件的宣告式描述，本身不執行驗證。

概念範例：

```csharp
new ElementConstraintDescriptor
{
    DefinitionCanonical = "http://hl7.org/fhir/StructureDefinition/Patient",
    ElementId = "Patient.identifier",
    ElementPath = "Patient.identifier",
    Min = 1,
    Max = "*"
};
```

Metadata 回答「規則是什麼」，但仍需 Validator 或 adapter 解讀。

### 3.2 Validation Rule

Validation rule 是可執行的程式，直接檢查 instance 並產生 `ValidationIssue`。

概念範例：

```csharp
RequiredFieldRule<Patient>.ForList(
    "identifier",
    patient => patient.Identifier);
```

Rule 回答「如何執行檢查」。

### 3.3 Validation Descriptor

本文件暫時以 descriptor 指稱一筆 normalized runtime metadata。它可能生成為 C#、
JSON 或其他格式。實際類別名稱與 schema 尚未決定。

### 3.4 Generated Rule Adapter

Generated rule adapter 是把 descriptor 或 Generator Internal Model 轉成現有
typed rule API 的生成程式碼。它不是 metadata 本身。

## 4. 現有 Validation 架構

### 4.1 Base Validator

現有 [`FhirValidator`](../../Validation/FhirValidator.cs)：

1. 使用 `FhirObjectGraphWalker` 走訪 object graph。

2. 對 primitive 執行 `PrimitiveFormatRule`。

3. 依 runtime type 從 `ResourceRuleRegistry` 取得明確註冊的 rules。

4. 執行每個 `IFhirValidationRule`。

現有 [`ResourceRuleRegistry`](../../Validation/Rules/ResourceRuleRegistry.cs) 以手寫
typed lambda 註冊 required 與 choice rules。

### 4.2 現有 Rule 能力

- `RequiredFieldRule<T>` 支援 required singleton 與 required collection。

- `ChoiceElementRule<T>` 支援 AtMostOne 與 ExactlyOne。

- `CardinalityRule` 目前只協助回報 collection 為 null 或包含 null item。

- 尚未有通用的 collection `min` / `max` count rule。

- 尚未有 fixed、pattern、binding、targetProfile 或 FHIRPath invariant engine。

### 4.3 Profile Validator

現有 [`ProfileValidator`](../../Validation/Profiles/ProfileValidator.cs)：

- 先執行 Base `IFhirValidator`。

- 依 profile canonical 找到 `IImplementationGuidePackage`。

- 從 package 取得 `IProfileValidationRule` 並執行。

現有 `IImplementationGuidePackage` 提供的是可執行 rules，不是可查詢的 normalized
StructureDefinition metadata。

### 4.4 目前主要接點缺口

現有架構沒有：

- generated metadata registry。

- descriptor loader。

- metadata-to-rule adapter。

- Base rule registry 與 generated registry 的合併 API。

- ProfileValidator 載入 generated descriptors 的正式介面。

- 通用 cardinality、terminology、FHIRPath、slice validation engine。

因此即使 Generator 保留了 `min`、`max` 或 choice metadata，runtime 目前也不會
自動執行。

## 5. 可由 StructureDefinition 推導的資訊

下表是初步分類，不是最終支援承諾。

| StructureDefinition 資訊 | 建議保留 Metadata | 可能轉成 Rule | 執行需求 |
|---|---:|---:|---|
| `min` | 是 | 是 | Required/Cardinality validator |
| `max` | 是 | 是 | Cardinality validator |
| choice allowed types | 是 | 是 | Choice/type validator |
| `type.profile` | 是 | 是 | Datatype/Profile conformance |
| `type.targetProfile` | 是 | 是 | Reference resolution/Profile conformance |
| `fixed[x]` | 是 | 是 | Deep value comparison |
| `pattern[x]` | 是 | 是 | Pattern matching |
| binding | 是 | 是 | Terminology validator |
| invariant/constraint | 是 | 是 | FHIRPath engine |
| slicing | 是 | 是 | Slice discriminator engine |
| slice cardinality | 是 | 是 | Slice＋Cardinality validator |
| `mustSupport` | 是 | 通常否 | Capability/obligation interpretation |
| `isModifier` | 是 | 不一定 | Safety/consumer behavior |
| short/definition/comment | 可保留 | 否 | 文件與 diagnostics |

不能因為目前 Validator 尚未支援某項規則，就在 Generator normalization 階段直接
丟棄該資訊。是否所有資訊都進入 runtime artifact，仍待後續決策。

## 6. 可能方向 A：直接生成 Typed Validation Rules

### 6.1 概念

Generator 直接產生符合現有 rule API 的 C#：

```csharp
internal static class GeneratedPatientRules
{
    internal static IReadOnlyList<IFhirValidationRule> Create()
    {
        return
        [
            RequiredFieldRule<Patient>.ForList(
                "identifier",
                patient => patient.Identifier),

            ChoiceElementRule<Patient>.AtMostOne(
                "deceased[x]",
                patient => patient.DeceasedBoolean,
                patient => patient.DeceasedDateTime)
        ];
    }
}
```

產物可能位於：

```text
Generated/R5/Validation/Rules
|-- PatientRules.g.cs
`-- PractitionerRules.g.cs
```

### 6.2 可能優點

- 最接近現有 `ResourceRuleRegistry`。

- typed lambda 可在編譯期檢查 property 名稱和型別。

- runtime 不需要解析 metadata 或反射查找 property。

- 執行路徑容易除錯。

- required 與 choice 的第一版接入成本可能較低。

### 6.3 可能限制

- Generated code 與現有 internal rule API 高度耦合。

- Rule constructor/API 變動時需要重新生成。

- 不容易在 runtime 動態加入新的 IG package。

- Binding、FHIRPath、slicing 仍需要額外 engine，無法只靠 typed lambda 解決。

- 如果只生成目前能執行的 rules，可能遺失尚未支援的 metadata。

- 每個 Profile/package 可能產生大量 C# 並需要重新編譯 SDK 或額外 assembly。

### 6.4 適合評估的情境

- 固定版本的 Base FHIR rules。

- required、cardinality、choice 等簡單規則。

- 產物與 SDK 一起編譯、發布。

## 7. 可能方向 B：生成 Metadata，由通用 Validator 解讀

### 7.1 概念

Generator 產生 descriptors：

```csharp
internal static class GeneratedPatientMetadata
{
    internal static readonly ElementConstraintDescriptor[] Constraints =
    [
        new()
        {
            ElementPath = "Patient.identifier",
            Min = 1,
            Max = "*"
        }
    ];
}
```

通用 Validator 解讀：

```text
ElementConstraintDescriptor
    ↓
Property accessor / path resolver
    ↓
Cardinality validator
    ↓
ValidationIssue
```

Metadata 也可以保存為 package JSON，而不是 C#。

### 7.2 可能優點

- Metadata 與執行引擎分離。

- 可完整保留 Validator 尚未支援的 StructureDefinition 資訊。

- 較適合動態載入 IG/Profile package。

- 同一套 descriptors 可供 Validator、文件、UI 或其他工具使用。

- Validator engine 改善後，不一定需要重新生成 metadata。

### 7.3 可能限制

- 需要先設計穩定的 descriptor schema。

- 需要 property/path resolver，可能使用 reflection 或 cached accessor。

- 問題可能延後到 runtime 才發現。

- 型別安全性比 typed rule 低。

- Descriptor versioning 與 package compatibility 需要額外設計。

- Validator 必須新增一套 metadata execution pipeline。

### 7.4 適合評估的情境

- IG/Profile 動態載入。

- Binding、invariant、slicing 等需要專用 engine 的規則。

- 希望 metadata 可被 validation 以外功能重用。

## 8. 可能方向 C：Metadata 為主，生成 Typed Rule Adapter

### 8.1 概念

先由 DefinitionParser 建立單一 normalized constraint model：

```text
StructureDefinition
    ↓
Normalized Constraint Model
    ├─ Metadata Renderer
    │    ↓
    │  Generated descriptors
    │
    `─ Rule Adapter Renderer
         ↓
       Generated typed rules
```

Metadata 是主要事實來源；typed rules 是可選的編譯後執行形式。

### 8.2 可能優點

- 保留完整 metadata，同時可利用 typed rule 的效能與編譯檢查。

- Base FHIR 可使用 compiled adapter，Profile 可使用 metadata interpreter。

- Rule API 變動時可重新生成 adapter，而不必重新解析原始 package。

- 可以逐步實作：先支援 cardinality/choice adapter，再增加專用 engines。

### 8.3 可能限制

- 元件與測試數量最多。

- 必須避免 metadata interpreter 與 typed adapter 行為不一致。

- 需要定義哪一份產物是 authoritative。

- Build、package 與版本管理較複雜。

### 8.4 適合評估的情境

- Base 與 Profile 有不同部署需求。

- 需要長期保留 metadata，但也重視固定 Base rules 的效能與型別安全。

## 9. 可能方向 D：Runtime 直接解讀 StructureDefinition

### 9.1 概念

不生成專用 validation artifact，由 Validator 在 runtime 載入 package、
StructureDefinition、snapshot 與 differential 後直接驗證。

### 9.2 可能優點

- 最接近原始 FHIR artifact。

- 可動態加入 package。

- 不需要為每個 Profile 重新編譯 C#。

### 9.3 可能限制

- Runtime 必須承擔 package resolution、snapshot generation、slicing、FHIRPath
  與 terminology 等完整複雜度。

- 啟動、記憶體與驗證成本可能較高。

- 目前 SDK 架構與能力距離最大。

- 問題較晚暴露，diagnostics 和除錯較複雜。

- 與「先用 Generator 正規化決策」的既有方向差異較大。

### 9.4 適合評估的情境

- 未來需要高度動態、任意 package 的 Profile Validator。

- 願意建立較完整的 runtime conformance engine。

## 10. 方向比較

| 評估面向 | A：直接 Typed Rules | B：Metadata Interpreter | C：Metadata＋Adapter | D：Runtime StructureDefinition |
|---|---|---|---|---|
| 與現有 Base Validator 接近度 | 高 | 中 | 中高 | 低 |
| 初期實作量 | 低至中 | 中至高 | 高 | 很高 |
| 編譯期型別安全 | 高 | 低至中 | 高 | 低 |
| 動態 IG/Profile | 低 | 高 | 高 | 很高 |
| 完整保留原始限制 | 低至中 | 高 | 高 | 很高 |
| Runtime 複雜度 | 低 | 中至高 | 中至高 | 很高 |
| 固定 Base rule 效能 | 高 | 取決於 accessor/cache | 高 | 較難預測 |
| Metadata 重用性 | 低 | 高 | 高 | 高 |
| 與目前 MVP 的距離 | 近 | 中 | 中至遠 | 遠 |

此表只是初步比較，必須在 MVP 完成後透過 prototype 驗證。

## 11. Base FHIR 與 Profile 是否採相同策略

目前不必假設兩者一定使用同一種產物。

### 11.1 可能組合一

```text
Base FHIR
    → Generated typed rules

IG/Profile
    → Generated/runtime-loaded metadata
    → Profile Validator interpreter
```

理由可能包括：

- Base FHIR 版本固定並隨 SDK 編譯。

- IG/Profile package 需要動態選擇與版本隔離。

### 11.2 可能組合二

```text
Base FHIR＋IG/Profile
    → 相同 descriptor schema
    → 相同通用 Validator
```

理由可能包括：

- 避免兩套 validation semantics。

- 降低 Base/Profile 結果不一致風險。

### 11.3 可能組合三

```text
Base FHIR＋IG/Profile
    → 相同 normalized metadata
    ├─ Base 生成 typed adapter
    `─ Profile 使用 interpreter
```

這是方向 C 的一種部署方式，目前只列為候選。

## 12. 初步 Metadata 分層構想

以下只是討論用草圖，不是已核准 API。

### 12.1 Definition identity

```text
PackageId
PackageVersion
FhirVersion
DefinitionCanonical
DefinitionVersion
BaseDefinition
Derivation
```

### 12.2 Element identity

```text
ElementId
ElementPath
SliceName
SourceFile
```

### 12.3 Constraint descriptors

可能拆分為：

```text
ElementConstraintDescriptor
CardinalityConstraintDescriptor
ChoiceConstraintDescriptor
TypeConstraintDescriptor
ReferenceTargetConstraintDescriptor
FixedValueConstraintDescriptor
PatternConstraintDescriptor
BindingDescriptor
InvariantDescriptor
SlicingDescriptor
SliceConstraintDescriptor
ObligationDescriptor
```

是否使用單一大 descriptor 或多種小 descriptor，留待 prototype 比較。

### 12.4 Diagnostic traceability

每個 generated descriptor/rule 應可回溯：

- package id/version。

- profile canonical/version。

- StructureDefinition source。

- element id/path/slice。

- constraint key。

這些資訊應能填入 `ValidationIssue.Source`、`PackageId`、`ProfileUrl` 與 `RuleId`。

## 13. Metadata 到 Rule 的可能對應

| Metadata | 可執行方式候選 |
|---|---|
| `min = 1` singleton | Required rule |
| `min >= 1` collection | Collection min-count rule |
| finite `max` collection | Collection max-count rule |
| choice `min = 0` | AtMostOne |
| choice `min = 1` | ExactlyOne |
| allowed type subset | Type/choice presence rule |
| fixed value | Fixed deep-comparison rule |
| pattern value | Pattern matcher |
| binding | Terminology engine |
| invariant | FHIRPath engine |
| slicing | Slice discriminator＋slice validators |
| targetProfile | Reference resolver＋Profile Validator |
| mustSupport | Metadata/obligation handling，不直接等同 required |

同一 constraint 不應同時被手寫 rule 與 generated rule 重複執行。未來需要 rule
identity、來源與 precedence/deduplication 策略。

## 14. 與現有 SDK 的可能接法

### 14.1 ResourceRuleRegistry

候選方向：

- `CreateDefault()` 直接加入 generated typed rules。

- 增加 `AddGeneratedRules(...)`。

- 增加 `AddMetadataRegistry(...)` 並由 `FhirValidator` 執行 generic validators。

- 將手寫 Base rules 與 generated Base rules 組合成 immutable registry。

目前 constructor 和 registry 都是 internal，修改 public API 的必要性尚未確認。

### 14.2 FhirObjectGraphWalker

現有 walker 已提供 runtime value、type 與 path。Metadata interpreter 仍可能需要：

- element path 到 `PropertyInfo` 的穩定 mapping。

- choice group 到多個 properties 的 mapping。

- slice instance matching。

- inherited property 與 generated top-level BackboneElement mapping。

- cached accessor，避免每次驗證重做 reflection。

### 14.3 ProfileValidator

候選方向：

- generated package adapter 實作現有 `IImplementationGuidePackage`。

- 擴充 package contract，讓 package 同時提供 metadata 與 executable rules。

- 新增獨立 `IProfileMetadataPackage`，由 ProfileValidator adapter 執行。

- ProfileValidator 只協調，另由新的 conformance engine 解讀 descriptors。

是否保留目前 public `IImplementationGuidePackage` 相容性，是後續決策項目。

## 15. Hand-written Rules 與 Generated Rules 的邊界

Generator 適合處理可從 StructureDefinition 決定性推導的規則。

手寫規則仍可能需要保留：

- SDK compatibility rule。

- 無法由 StructureDefinition 表達的安全檢查。

- 本地業務規則。

- 跨 Resource/workflow rule。

- 需要外部系統資料的 rule。

- Generator 尚未支援、但產品目前必須執行的過渡規則。

未來需要定義：

- Generated 與手寫 rule 的執行順序。

- 相同 rule identity 的覆蓋或去重方式。

- 重新生成時不得覆寫手寫檔案。

- Base FHIR、IG/Profile、BusinessRule 的 `ValidationRuleSource`。

## 16. 不論選哪個方向都應保留的原則

以下原則可作為候選方案共同約束，但仍可在正式決策時調整：

- StructureDefinition DTO 不是 runtime validation metadata。

- DefinitionParser 先建立 normalized constraint model，Renderer 不重新解讀原始 JSON。

- 生成結果必須 deterministic。

- 未支援的限制不得被靜默忽略。

- Metadata/rule 必須可追溯至 package、profile 與 element。

- `mustSupport` 不得直接等同 `min = 1`。

- Constraint Profile 不因 inherited override 而重複生成 C# property。

- Generated artifact 與手寫程式碼分離。

- Base、Profile 與 Business rule 的來源必須能在 `ValidationIssue` 中辨識。

- FHIRPath expression 不應直接拼接為未經控制的 C# source。

- Terminology、reference resolution 與 FHIRPath 應保留獨立 engine 邊界。

## 17. MVP 完成後的比較實驗

完成目前 Generator MVP 後，建議建立小型 spike，不直接進入正式架構。

### 17.1 測試規則集合

以同一組簡單限制測試各方向：

1. required singleton，例如 `Claim.status 1..1`。

2. required collection，例如某 element `1..*`。

3. finite collection max。

4. optional choice AtMostOne。

5. required choice ExactlyOne。

6. inherited element override，例如 Profile 將 `Patient.identifier` 改為 `1..*`。

### 17.2 Prototype A

直接生成 typed rules，接入 `ResourceRuleRegistry`。

觀察：

- 生成程式碼大小。

- 編譯錯誤可讀性。

- 與現有手寫 rules 的合併方式。

- Rule API 耦合程度。

### 17.3 Prototype B

生成最小 descriptors，由 generic Validator 解讀。

觀察：

- Descriptor schema 是否足夠。

- Path/property mapping 複雜度。

- Reflection cache 的必要性。

- Diagnostics 是否能保留來源資訊。

### 17.4 Prototype C

從相同 normalized metadata 同時產生 descriptors 與 typed adapter。

觀察：

- 如何證明兩條執行路徑結果一致。

- 是否值得增加 build/artifact 複雜度。

### 17.5 暫不優先的 Prototype

Runtime 直接解讀完整 StructureDefinition 的方案可保留研究，但除非需求明確要求
任意 package 動態 validation，否則不建議作為 MVP 後第一個 spike。

## 18. 決策評估條件

完成 prototype 後，至少依下列條件比較：

- 與現有 Validator API 的相容性。

- Base FHIR 與 IG/Profile 的共同需求。

- 是否需要 runtime 動態載入 package。

- Generated artifact 數量與可維護性。

- Metadata 完整性。

- 編譯期型別安全。

- Runtime 效能與記憶體。

- Diagnostics 與來源追蹤能力。

- Rule/metadata schema versioning。

- 測試成本。

- 手寫規則的保留與去重。

- 未來 terminology、FHIRPath、slicing 的擴充能力。

## 19. 待決問題

1. Metadata 是否為唯一 authoritative artifact？

2. Base FHIR rules 是否值得生成 typed adapter？

3. IG/Profile 是否必須支援不重新編譯 SDK即可載入？

4. Metadata 應生成為 C#、JSON，或保留在 package cache？

5. Descriptor 應以 element path、element id、C# property identity，或多者組合索引？

6. 如何處理 Profile canonical version 與 package dependency precedence？

7. Base rule 與 Profile rule 如何合併 cardinality？

8. Re-profile 與多個同時宣告的 profiles 如何合併或分別驗證？

9. Generic Validator 是否使用 reflection、compiled expression 或 generated accessor？

10. Binding 與 FHIRPath engine 是 SDK 內建、可插拔服務，或外部整合？

11. Slicing validation 應在第一版 Profile generation 支援到什麼程度？

12. 現有手寫 `ResourceRuleRegistry` 如何逐步遷移而不改變既有行為？

13. `IImplementationGuidePackage` 是否需要保持完全相容？

14. Generated rule identity 如何避免與手寫 rule 重複？

15. 哪些 metadata 需要公開給 SDK 使用者，哪些只供 internal validation？

## 20. 目前結論

目前只確認：

- 完整 Generator 需要保留可由 StructureDefinition 推導的 validation 資訊。

- Metadata 與 executable rule 是不同責任，不應混為同一概念。

- 現有 Validator 無法直接消費 Generator Internal Model。

- 直接 typed rules、metadata interpreter、hybrid adapter 與 runtime
  StructureDefinition 都是可評估方向。

- Base FHIR 與 IG/Profile 不一定必須採用完全相同的部署方式。

- 正式選擇延後至 Generator MVP 完成，並以小型 prototype 和本文件的評估條件作決策。

本文件不授權目前 MVP 實作 validation metadata/rule generation，也不修改現有
Generator MVP 的完成範圍。

