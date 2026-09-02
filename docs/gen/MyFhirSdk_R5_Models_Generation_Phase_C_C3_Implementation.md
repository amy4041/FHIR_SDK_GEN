# MyFhirSdk R5 Models Generation Phase C3 Renderer-ready IR

Version 1.0

- 狀態：Complete
- FHIR baseline：R5 `5.0.0`
- C2 input：`DefinitionDependencyGraph` + `GenerationScope`
- C0 policy inputs：model naming、Backbone、choice/open type
- Output：immutable `ModelIrBatch`

## 1. Scope

C3 將 C2 已解析的 graph/scope 轉成 renderer-ready intermediate representation。這是最後一層
可以讀取 `StructureDefinitionDto`、inventory provenance 與 graph edges 的 model transformation；
C4/C5 renderer 只應讀 IR，不應重新判斷 canonical、choice、Backbone 或 contentReference。

```text
DefinitionDependencyGraph + GenerationScope
  + PrimitiveTypeMappingView
  + ModelIrGenerationPolicy
        ↓
ModelIrBuilder
        ├─ declaration/base/placement resolution
        ├─ direct element ownership
        ├─ type alternative/profile resolution
        ├─ choice/open type expansion
        ├─ Backbone declaration and ownership
        ├─ contentReference target resolution
        ├─ validation metadata preservation
        └─ symbol/member/path collision validation
        ↓
ModelIrBatch or diagnostics (never a partial batch)
```

## 2. C0 policy composition

`ModelIrGenerationPolicyLoader` 讀取：

- `r5-model-naming-policy.json`
- `r5-backbone-policy.json`
- `r5-choice-open-type-policy.json`

Loader 驗證三份 policy 的 FHIR version 一致，且 public representation 仍是 C0 核准的：

- ordinary choice：每個 alternative 一個 nullable property
- open type：單一 nullable polymorphic property
- Backbone：public top-level placement

Composite policy 保存 datatype/resource/Backbone namespace、Backbone base CLR type、open type CLR
type、explicit member/Backbone renames、open type element ids，以及 concrete Resource synthetic
member names。Policy 缺失、malformed 或 decision drift 產生 `FSG0037`。

## 3. IR contracts

`ModelDeclarationIr` 表達：

- datatype、resource 或 Backbone category
- FHIR/C# identity、namespace、deterministic artifact path
- abstract/sealed shape
- resolved base type與 abstract/external flags
- Resource owner canonical與 Backbone element id
- direct members與完整 definition provenance

`ModelMemberIr` 表達：

- original element id/path、FHIR/JSON name及 declaration provenance
- standard、ordinary choice、open type、Backbone或 contentReference representation
- original cardinality、choice stem及 source order
- resolved type alternatives與 generated public properties
- resolved contentReference target，不把 reference string交給 renderer
- validation及說明 metadata

`ModelTypeReferenceIr` 保存 target canonical/element id、FHIR type code、CLR type、abstract、external、
primitive/support flags，以及 `profile`/`targetProfile`。因此 abstract/open targets 不需由 renderer
反查 inventory。

## 4. DTO expansion與 metadata preservation

`ElementDefinitionDto` 新增強型別欄位：

- `label`
- `alias`
- `representation`
- `comment`
- `requirements`
- `meaningWhenMissing`
- `orderMeaning`

IR 另保存 constraints、binding、mustSupport、modifier/summary flags、conditions、slicing raw JSON，
以及 fixed/pattern raw values。C0 核准 specialization scope 的 fixed/pattern 數量為零；若未來出現，
會保留 raw identity並以 `FSG0039` 在 render 前失敗。

## 5. Member representations

### Standard

一般 element 必須有一個 resolved type alternative。Cardinality 保存 `0|1 .. 1|*`，property
另外表達 nullable與 collection shape。

### Ordinary choice

IR 保留原始 `[x]` member與 alternatives順序，並為每個 alternative建立 nullable property。例如：

```text
choice[x] : string | Payload
  → ChoiceString  : FhirString?
  → ChoicePayload : Payload?
```

每個 alternative 保存 canonical、CLR type、profile與 targetProfile；原始 choice min/max留在
member供 C6 建立 at-most-one/exactly-one rule。

### Open type

IR 保留全部 official alternatives，但 public shape只有一個由 C0 policy指定的 polymorphic
property，例如 `MyFhirSdk.Core.DataType? Value`。不使用 CLR type name heuristic，也不丟棄
alternative metadata。

### Backbone

每個 resource-owned Backbone element建立獨立 public top-level declaration：

- namespace `MyFhirSdk.Resources`
- `sealed`
- base `MyFhirSdk.Core.BackboneElement`
- identity來自完整 element id或 explicit rename
- artifact置於 Resource owner folder

Element 會配置給最深的 owning Backbone；nested Backbone不繼承 containing Backbone。

### contentReference

C3 只讀 C2 resolved `ContentReference` edge，產生 target element provenance與 CLR reference。
若 target是 Backbone，property直接引用已建立的 top-level Backbone IR declaration。

## 6. Naming、placement與collision gates

所有 type/property/choice suffix/Backbone segment都透過 `CSharpNameConverter`。Builder套用：

- `Expression.expression → ExpressionValue`
- `Reference.reference → ReferenceValue`
- C0 Backbone explicit renames

Render前會驗證：

- fully qualified type identity（ordinal）
- artifact path（ordinal-ignore-case）
- member與 declaring type名稱
- direct、inherited及 synthetic member名稱
- JSON member名稱

未核准 collision 產生 `FSG0040`，任何 error 都使 `ModelIrBatch` 為 null，不留下 partial class。

## 7. Determinism與tests

Declarations依 namespace/type ordinal排序，members保存 snapshot order，policy sets及 metadata
collections使用固定 ordinal順序，diagnostics依 code/canonical/element/message排序。

Tests涵蓋：

- official `Period` inheritance、abstract external target、primitive mapping與 cardinality
- official `Reference.reference` explicit rename
- ordinary choice與 open type完整 alternatives
- profile/targetProfile preservation
- Resource-owned Backbone naming、placement及 member ownership
- resolved contentReference與 validation metadata
- required choice、collection及 nullable public shape
- direct/synthetic與 inherited member collision
- unsupported cardinality fail-before-render
- reordered definition determinism
- DTO metadata與 C0 composite policy loading
- 既有 MVP parser/renderer/golden regression

Release verification：

- CodeGen：312 passed、0 failed
- Solution：568 passed、0 failed、1 skipped
- `dotnet format --verify-no-changes`：passed
- `git diff --check`：passed

## 8. Known capability boundary

C2 full generation scope仍會在 C3 前阻擋 `time`、`oid`、`uuid` unsupported primitive references；
C3 不建立 fallback CLR types，也不繞過 C2 scope gate。這符合 C0 choice policy 的
`unresolvedAlternativeDisposition = diagnostic-before-render`。

## 9. C4/C5 handoff

C4/C5 renderer應只消費：

- `ModelDeclarationIr` 決定 class、namespace、base與 artifact path
- `ModelMemberIr.Properties` 決定公開 members
- `TypeAlternatives` 決定 choice/open type metadata
- `Validation` 交給 C6 metadata generation

Renderer不得讀 `StructureDefinitionDto`、`DefinitionInventoryItem`、raw canonical字串或 C0 JSON
policy，也不得自行重新命名或解析 contentReference。
