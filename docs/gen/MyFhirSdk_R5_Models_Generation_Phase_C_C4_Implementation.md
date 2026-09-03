# MyFhirSdk R5 Models Generation Phase C4 Complex Datatypes

Version 1.0

- 實作狀態：Renderer、batch pipeline與supported official batch完成
- Full official acceptance：Blocked by approved Phase B unsupported primitives
- FHIR baseline：R5 `5.0.0`
- Input：C3 `ModelIrBatch`
- Output：deterministic complex datatype source artifacts

## 1. Scope

C4新增只消費renderer-ready IR的complex datatype renderer與generation pipeline。Renderer不讀
`StructureDefinitionDto`、inventory、dependency graph或C0 JSON policies；所有base、type、choice、
contentReference、名稱及artifact path都必須已由C3解析。

```text
ModelIrBatch
    ↓ filter ComplexDatatype declarations
batch dependency validation
    ↓
ComplexDatatypeRenderer
    ↓ deterministic GeneratedSource[]
RoslynCompilationValidator (one complete batch)
    ↓
ComplexDatatypeGenerationBatch
```

Resource與Backbone declarations不由C4 renderer輸出，保留給C5。

## 2. Renderer behavior

`ComplexDatatypeRenderer`依IR輸出：

- `MyFhirSdk.Types` namespace與IR指定base class
- abstract、extensible concrete或sealed leaf class modifier
- direct properties，不重複inherited properties
- nullable scalar與non-null initialized `IList<T>` collection
- ordinary choice的nullable alternative properties
- open type的單一polymorphic property
- resolved self-reference與contentReference CLR type
- 只有wire name無法由CLR property name還原時才輸出`JsonPropertyName`
- XML documentation、LF line endings及deterministic using/order

例如`Reference.reference`輸出：

```csharp
[JsonPropertyName("reference")]
public FhirString? ReferenceValue { get; set; }
```

Required scalar仍保持nullable public shape；required validation由C6 metadata/rules負責。這與既有
Runtime API及「允許建立invalid object後再取得validation issues」的模型一致。

## 3. Inheritance與class modifiers

C3 IR modifier resolution在C4 integration中補正為：

- official abstract definition：`abstract`
- 有generated derived specialization的concrete definition：保持可繼承
- 無derived specialization的concrete leaf：`sealed`
- Backbone：仍由C0 policy固定為`sealed`

因此`Quantity`保持可供`Duration`繼承，而`Duration`與`Period`為sealed leaf。

## 4. Batch integrity

`ComplexDatatypeGenerationPipeline`在render/compile前確認：

- batch至少包含一個complex datatype
- generated base datatype必須存在於同一batch
- non-external、non-primitive property dependency必須存在於同一batch
- external bootstrap與Phase B primitive可以作為terminal reference
- sources按artifact path ordinal排序
- full batch一次交給Roslyn，不逐檔依賴手寫同名datatype補齊

Missing generated dependency產生`FSG0032`，renderer unsupported shape產生`FSG0039`，Roslyn
failure沿用`FSG0012`。失敗時不回傳partial batch。

## 5. Compatibility與runtime evidence

C4 tests將新renderer產生的`Address`、`Coding`、`HumanName`、`Identifier`與`Period`動態編譯，
並與目前Runtime types比較：

- abstract/sealed shape
- base CLR identity
- declared public property名稱與型別
- nullable annotations
- setter accessibility
- `JsonPropertyName`

Generated `Period`另以動態Resource container驗證：

- serialize → parse → serialize JSON stability
- primitive value與metadata可還原
- `FhirValidator`可走訪generated datatype並回報nested primitive path

## 6. Golden與shape matrix

Tests涵蓋：

- official `Period` golden source
- `Reference.ReferenceValue` JSON wire rename
- `Quantity → Duration` inheritance與modifier
- ordinary choice split properties
- self-reference
- resolved contentReference
- collection/nullability rendering
- missing generated dependency fail-before-compilation
- two-run byte-identical artifacts
- existing five MVP type API compatibility
- runtime JSON round-trip與Validator traversal

## 7. Official batch result

Official graph有39個non-external complex datatype declarations。目前28個datatype seeds的完整
closure不會觸及unsupported primitive，28個source可在單一batch中render並通過Roslyn：

```text
Address, Age, Attachment, CodeableConcept, CodeableReference, Coding,
ContactDetail, ContactPoint, Contributor, Count, Distance, Duration,
Expression, ExtendedContactDetail, HumanName, Identifier, MarketingStatus,
MonetaryComponent, Money, Period, ProductShelfLife, Quantity, Range, Ratio,
RatioRange, Reference, RelatedArtifact, VirtualServiceDetail
```

其餘11個datatype的selected closure會觸及C0/Phase B明確標記unsupported的`oid`、`time`或
`uuid`：

```text
Annotation, Availability, DataRequirement, Dosage, ElementDefinition,
ParameterDefinition, SampledData, Signature, Timing, TriggerDefinition,
UsageContext
```

Full 39-type scope因此在C2/C3 render-before gate失敗。依C0-006，C4不得：

- 將它們映射成`string`或`object`
- 略過choice alternative或property
- 建立未核准的fallback wrapper
- 依賴手寫同名type掩蓋missing mapping

解除blocker需要獨立核准`oid`、`time`、`uuid`的Runtime CLR/codec/validator contracts，更新Phase B
primitive policy與generated primitive baseline，再重跑C2-C4 formal batch。`xhtml`目前只在external
handwritten `Narrative`，不屬C4 generated datatype declaration blocker。

## 8. Verification

- CodeGen：325 passed、0 failed
- Solution：581 passed、0 failed、1 skipped
- `dotnet format --verify-no-changes`：passed
- `git diff --check`：passed

## 9. C5 handoff

C5可重用C4的source conventions與batch dependency validation方式，但必須建立獨立Resource/
Backbone renderer，處理`ResourceType`、owner folder、nested Backbone及Resource-specific closure。
C5不得為了繞過C4 primitive blocker改變C0 choice或primitive disposition。
