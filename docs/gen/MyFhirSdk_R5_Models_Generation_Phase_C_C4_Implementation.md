# MyFhirSdk R5 Models Generation Phase C4 Complex Datatypes

Version 1.1

- 實作狀態：Renderer、batch pipeline與full official batch完成
- Full official acceptance：Passed（39 datatypes及17 inline components）
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

Official graph的39個non-external complex datatype declarations現在全部可進入同一個generation
scope。Primitive policy `1.1.0`新增`FhirOid`、`FhirTime`及`FhirUuid`的string CLR contract、JSON
codec與official R5 format validators，因此原先受阻的11個datatype也可完整解析。

完整scope另包含17個datatype-owned inline components。官方definitions以`Element`而不是
`BackboneElement`描述這些component；C4將它們建立為`ComplexDatatypeComponent` IR、生成為
`MyFhirSdk.Types` public top-level sealed classes並直接繼承`Element`。實體檔案依owner分組，例如：

```text
Generated/R5/Types/DataRequirement/DataRequirementDateFilter.g.cs
Generated/R5/Types/ElementDefinition/ElementDefinitionBase.g.cs
```

因此full batch共有56個sources：39個datatype declarations及17個inline component declarations。
整批一次通過Roslyn，且連續兩次render的artifact集合byte-identical。`xhtml`仍由external
handwritten `Narrative`承接，不是C4 generated datatype依賴。

## 8. Verification

- CodeGen：324 passed、0 failed
- Solution：596 passed、0 failed、1 skipped
- `dotnet format --verify-no-changes`：passed
- `git diff --check`：passed

## 9. C5 handoff

C5可重用C4的source conventions與batch dependency validation方式，但必須建立獨立Resource/
Backbone renderer，處理`ResourceType`、owner folder、nested Backbone及Resource-specific closure。
C5不得改變C4已核准的choice或primitive disposition。
