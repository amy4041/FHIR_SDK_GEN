# MyFhirSdk R5 Models Generation Phase C5：Resources 與 Backbones

Version 1.0

- 狀態：Implemented
- FHIR baseline：R5 `5.0.0`
- 輸入：C3 `ModelIrBatch`
- 輸出：deterministic Resource、Backbone 及其 datatype closure source batch

## 1. Scope

C5 將 renderer-ready IR 轉為 Resource 與 Resource-owned Backbone 的 C# source。Renderer 不重新解析
StructureDefinition、canonical、choice、contentReference 或命名規則；這些決策都必須已存在於 IR。

```text
ModelIrBatch
  -> validate generated dependency and Backbone owner closure
  -> dispatch datatype / Resource / Backbone renderer
  -> order by artifact path
  -> compile the complete source batch once with Roslyn
  -> ResourceBackboneGenerationBatch
```

C5 只產生記憶體中的 artifacts。正式寫入 `Generated/R5`、切換 handwritten ownership 與刪除舊檔仍屬
C7/C8。

## 2. Renderer behavior

`ResourceBackboneRenderer` 與 C4 datatype renderer 共用 `ModelDeclarationSourceRenderer`，因此 property、
collection、nullable、JSON rename、XML documentation、using ordering 與 LF newline 規則一致。

Resource 規則：

- namespace 固定為 `MyFhirSdk.Resources`；
- 繼承 IR 已解析的 Resource base type；
- concrete Resource 產生帶 `[JsonPropertyName("resourceType")]` 的 read-only override；
- override 值使用 exact FHIR type name；
- abstract Resource 不產生固定 `ResourceType` implementation；
- ordinary choice 維持每個 alternative 一個 nullable property。

Backbone 規則：

- 產生 `public sealed` top-level class；
- namespace 維持 `MyFhirSdk.Resources`；
- 直接繼承 `BackboneElement`，nested path 不形成 CLR inheritance；
- artifact 置於 owner folder，例如
  `Generated/R5/Resources/Patient/PatientContact.g.cs`；
- `contentReference` 使用 IR 已解析的 CLR property type，不輸出 reference 字串或重複 declaration。

## 3. Profile-narrowed CLR type

C5 public API comparison 發現官方 `Claim` 與 `Coverage` 的部分 `Quantity` elements 帶有
`SimpleQuantity` profile。只看 `type.code` 會把既有 `SimpleQuantity` API 放寬成 `Quantity`。

`r5-model-naming-policy.json` 因此明確記錄 approved profile type override：

```text
http://hl7.org/fhir/StructureDefinition/SimpleQuantity
  -> MyFhirSdk.Types.SimpleQuantity
  -> retained-handwritten-constraint-profile
```

IR resolution 在 profile edge 確實存在時套用這個 override。FHIR choice suffix 與 JSON wire name仍取自
原始 `type.code`，因此 `valueQuantity` 不會變成 `valueSimpleQuantity`。未核准的 constraint profile 不會
依名稱猜測 CLR type。

## 4. Batch integrity

`ResourceBackboneGenerationPipeline` 對同一 scope 內四種 declaration 一次處理：

- `ComplexDatatype`
- `ComplexDatatypeComponent`
- `Resource`
- `Backbone`

batch 在 render 前檢查：

- 至少有一個 Resource；
- 每個非 primitive、非 external dependency 都存在於 batch；
- 每個 Backbone 的 `ResourceOwnerCanonical` 都對應 batch 內的 Resource；
- artifact path 以 ordinal ordering 產生。

所有 sources 會一次送進 Roslyn。dependency、owner、renderer shape 或 compilation 失敗時，不回傳
partial batch。

## 5. Official full batch result

官方 R5 package 的完整 C5 scope 為：

| IR category | Count |
|---|---:|
| Resource | 160 |
| Resource-owned Backbone | 613 |
| Complex datatype | 39 |
| Datatype-owned component | 17 |
| Total source artifacts | 829 |

完整 829-source batch 可共同編譯。將輸入 declarations 反向後再次生成，artifact path、順序及內容仍
byte-identical。每個 concrete Resource 的固定 `ResourceType` 亦逐一與 IR 的 FHIR name 比對。

## 6. Compatibility and runtime evidence

測試涵蓋：

- C0 snapshot 中 39 個既有 `MyFhirSdk.Resources` public types 全部能在完整 IR 找到；
- 既有 type modifier、base type、declared property CLR type 與 effective JSON name 均被保留；
- official Patient golden source；
- `PatientContact` owner folder 與 public top-level placement；
- nested `ClaimSubDetail` 直接繼承 `BackboneElement`；
- Patient choice properties；
- profile-narrowed `SimpleQuantity`；
- resolved Resource contentReference；
- generated Patient serialize/parse/serialize JSON stability；
- generated Patient 可包含 polymorphic contained Organization；
- `FhirValidator` 能走訪 generated Patient 並回報 `Patient.birthDate` primitive format path。

完整 generated metadata、所有 generated Resource 的 non-generic runtime dispatch，以及 validation rule
generation 仍由 C6 負責。

## 7. C6 handoff

C6 應直接使用這一階段證明可共同編譯的完整 model surface，建立 deterministic model metadata、
Resource factory、datatype/choice dispatch 與 validation rule registration。C6 不應重新掃描
StructureDefinition 或重新推導 C5 的 public shape。
