# MyFhirSdk R5 Models Generation Phase C6：Model metadata、factory 與 validation composition

Version 1.0

- 狀態：Implemented
- FHIR baseline：R5 `5.0.0`
- 輸入：C3 `ModelIrBatch` 與 C5 完整 model source batch
- 輸出：deterministic model metadata、Resource factory 與 executable validation rules

## 1. Scope

C6 將 renderer-ready IR 中已核准的 metadata 轉成 Runtime 可直接載入的 immutable registry。Renderer
不重新解析 `StructureDefinition`，Parser、Serializer 與 Validator 也不掃描 assembly 來猜測 R5
model surface。

```text
ModelIrBatch
  -> ModelMetadataIrBuilder
  -> validate metadata coverage and conflicts
  -> ModelMetadataRenderer + ValidationCompositionRenderer
  -> combine with the complete C5 source batch
  -> compile all 831 sources once with Roslyn
  -> ModelMetadataGenerationBatch
```

## 2. Metadata IR

`ModelMetadataIrBatch` 是 C6 的 deterministic intermediate representation，包含：

- concrete Resource 的 FHIR type name、CLR type 與 factory；
- concrete datatype inventory；
- Extension `value[x]` 的 CLR type 與 exact JSON property name；
- general open type property 的 declaring type、property、runtime value type 與 JSON property name；
- required scalar、required collection、at-most-one choice 與 exactly-one choice rules。

Builder 依 stable ordinal key 排序，並拒絕重複 Resource name/type、重複 open-type identity 與
Extension JSON identity conflict。相同 IR 不受輸入順序影響，會產生 byte-identical artifacts。

External IR 永遠保留官方 `Extension.value[x]` 的完整 54-type set；registry composition 則依本次
generation scope 輸出可用 entries。Runtime primitives 與 external bootstrap types 可直接納入，
generated datatype 只有在本次 model declarations 中存在時才輸出，避免 selected-scope artifact
引用未生成的 CLR type。

## 3. Official R5 batch result

| Metadata category | Count |
|---|---:|
| Concrete Resource factories | 158 |
| Generated Resource declarations | 160 |
| Concrete datatype inventory | 41 |
| Generated complex datatypes | 39 |
| External concrete datatypes (`Meta`, `Narrative`) | 2 |
| Declared datatype mappings (`Meta.security`, `Meta.tag`) | 2 |
| Extension `value[x]` alternatives | 54 |
| General open-type mappings | 486 |
| Validation rules | 1,103 |
| C5 model sources | 829 |
| C6 metadata sources | 2 |
| Combined compilation sources | 831 |

160 個 generated Resource declarations 中有 2 個 abstract Resources，因此只有 158 個 factory。
Abstract Resource 不可被 parser 依 `resourceType` 直接具現化。

Concrete datatype inventory 除了 C4 的 39 個 generated datatypes，也包含 ownership policy 保留的
external `Meta` 與 `Narrative`。`Meta.security` 與 `Meta.tag` 另外產生 `Coding` declared datatype
mapping，讓 Parser 不需猜測抽象 `DataType` property 的實際型別。

486 筆 general open-type mappings 來自 9 個 generated open-type members 與 54 個允許的 runtime
value types。這些 entries 讓 Runtime 依 metadata 處理 exact FHIR JSON suffix，例如
`Task.input.value[x]` 的 `FhirString` 使用 `valueString`，不需要增加 concrete model type 分支。

## 4. Generated artifacts

C6 產生兩個固定 artifact：

- `Generated/R5/ModelMetadata/R5ModelMetadata.g.cs`：Resource factories、datatype、Extension
  與 general open-type mappings；
- `Generated/R5/ModelMetadata/R5ValidationRules.g.cs`：以既有 typed rule API 建立 validation
  registry。

所有 factory 都是明確的 strongly typed construction delegate。Default provider 的 transitional
handwritten surface 也改為 explicit entries，不再以 `Assembly.GetTypes()` 建立 inventory；C7/C8
切換 generated artifacts 時可直接替換 provider composition。

## 5. Validation composition

C6 僅生成 C0 capability matrix 核准為 executable 的規則：

| Rule kind | Count |
|---|---:|
| Required scalar | 794 |
| Required collection | 59 |
| Choice at-most-one | 194 |
| Choice exactly-one | 56 |

Required scalar 的 794 筆包含 786 個 generated ordinary scalar、5 個 required open-type presence
rules，以及 external bootstrap 的 `Extension.url`、`Narrative.status`、`Narrative.div` 3 筆規則。
C6 reconnaissance 同時修正 C0 統計分類：9 個 generated open-type members 應分為 5 required
與 4 optional，而不是全部列為 optional；ordinary choices 則為 56 required 與 194 optional。

`ResourceRuleRegistry` 會由 base type 到 derived type 組合 rules，使 abstract/base Resource 上宣告的
限制也能套用到 concrete runtime instance。FHIRPath invariants、terminology binding、slicing、
fixed/pattern 等 preserve-only metadata 仍保留於 IR，但本階段不宣稱可執行。

## 6. Runtime integration evidence

完整 831-source batch 會以 Roslyn 共同編譯，並以 generated registries 驗證：

- factory 能由 FHIR resource type name 建立 concrete Resource；
- general open type 能 serialize/parse/serialize 並維持 exact JSON property name；
- required ordinary/open members 產生包含 collection index 與 FHIR choice name 的錯誤路徑；
- empty `Meta.tag` object 透過 declared mapping 明確解析為 `Coding`；
- external `Extension` 與 `Narrative` required rules 由 generated registry 執行；
- metadata output 在 reversed IR input 下保持 deterministic；
- selected Resource scope 不引用 scope 外的 generated datatypes；
- existing handwritten Runtime、primitive、JSON 與 validation regression 保持相容。

## 7. C7 handoff

C7 應以 C6 `ModelMetadataGenerationBatch` 的 artifact inventory 建立 manifest、provenance、hash 與
reproducibility gate。Generated metadata 的 coverage 與 executable capability set 必須明確寫入
manifest，不得把 preserve-only validation metadata 宣稱為完整 R5 validation。
