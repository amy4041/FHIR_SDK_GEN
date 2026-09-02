# MyFhirSdk R5 Models Generation Phase C2 Dependency Graph

Version 1.0

- 狀態：Complete
- FHIR baseline：R5 `5.0.0`
- Package：`hl7.fhir.r5.core#5.0.0`
- C1 input：`DefinitionInventory`
- C0 external policy：`CodeGen/Policy/r5-model-ownership-policy.json`
- Phase B primitive policy：`CodeGen/Policy/primitive-generation-policy.json`

## 1. Scope

C2 將 C1 的完整 definition inventory 轉成 validated、deterministic dependency graph，並提供
cycle-safe selected generation scope。C2 不重新掃描 package、不從 filename 猜 type/canonical，
也不建立 C3 renderer-ready IR。

正式資料流：

```text
DefinitionInventory
  + ModelOwnershipPolicyDocument
  + PrimitiveTypeMappingView
        ↓
DefinitionDependencyGraphBuilder
        ↓
DefinitionDependencyGraph
        ↓ selected canonical seeds
GenerationScopeSelector
        ↓
GenerationScope + deterministic GenerationPlan
```

## 2. Node registry 與 ownership

Graph 以 canonical ordinal identity 建立 307 個 immutable nodes。Official package disposition：

| Disposition | Count | Generation behavior |
|---|---:|---|
| `GeneratedModel` | 199 | Phase C declaration candidates |
| `ExternalHandwritten` | 11 | 可被引用，但不產生 declaration |
| `SupportedPrimitive` | 17 | Phase B terminal，由 `PrimitiveTypeMappingView` 證明可映射 |
| `UnsupportedPrimitive` | 4 | terminal；被 selected scope 觸及時直接診斷 |
| `ConstraintProfile` | 66 | reference target，不產生 Phase C declaration |
| `LogicalModel` | 10 | retained metadata node，不產生 Phase C declaration |

External nodes 必須由 C0 ownership policy 明確核准，且 policy 的 canonical、FHIR type、kind、
abstract、base canonical 必須與 inventory 完全相符。External handwritten nodes 是 traversal
terminal：其 runtime declaration 已存在，因此 selected closure 不會把它的內部欄位重新納入
generated declaration scope。

## 3. Edge model

每條 edge 保存 source canonical、source element id、edge kind、target canonical、可選的 target
element id，以及原始 reference identity。這讓 C3 不必重新解析 DTO 字串，且每個 reference
都能回溯 provenance。

Official R5 formal graph 共 10,299 條 edges：

| Edge kind | Count | Resolution |
|---|---:|---|
| `Inheritance` | 209 | `baseDefinition` canonical |
| `ElementType` | 6,926 | inventory specialization type identity |
| `Profile` | 63 | canonical，允許 version suffix |
| `TargetProfile` | 2,410 | canonical，允許 version suffix |
| `ContentReference` | 78 | canonical + snapshot element id |
| `BackboneOwner` | 613 | inline Backbone element → owning definition |

Snapshot 只為 generated model nodes 建立 property/reference edges；11 個 external nodes 保留
inheritance identity 與 inbound references，但視為 runtime-owned terminal。Primitive nodes同樣不
展開其 FHIRPath system implementation detail。

## 4. Validation

Graph build 在交付 graph 前驗證：

- ownership policy schema/FHIR version 與 external node identity
- missing inheritance base、element type、profile/targetProfile canonical
- missing `contentReference` snapshot element
- model inheritance kind compatibility
- inheritance cycle

Inheritance graph 不允許 cycle；一般 reference graph允許 self-reference 與 mutual-reference。
Diagnostics 依 code、canonical、element id、source、message ordinal 排序。任何 build error 都不
回傳 partial graph。

## 5. Generation scope

`GenerationScopeSelector.Select` 接受 canonical seeds，沿 graph 取得完整、cycle-safe closure：

- generated model target 進入 closure 與 generation plan
- approved external、supported primitive 成為 terminal dependency
- constraint profile、logical model可被追蹤，但不產生 declaration
- unsupported primitive 產生 `FSG0035`，scope 不回傳 partial plan
- missing/non-generated seed 產生 `FSG0036`

Generated models、terminal dependencies、traversed edges 與 plan ordinals全部固定排序。Official
full scope 目前會明確產生 41 筆 unsupported primitive references：`time` 23、`oid` 9、`uuid`
9；`xhtml` 僅存在於 external handwritten `Narrative`，不會被重複生成。這是 Phase B/C0 已知
capability boundary，不是 graph resolution failure。

## 6. Mapper migration

`CSharpTypeMapper.DefaultComplexTypeNames` 已移除。Mapper constructor 現在要求
`DefinitionTypeMappingView`，不再有隱藏 fallback whitelist。`DefinitionTypeMappingView.FromGraph`
由 graph disposition 推導 generated datatype、resource 與 external handwritten CLR mapping；因此
例如 `Period` 對應 `MyFhirSdk.Types`、`Patient` 對應 `MyFhirSdk.Resources`、`DomainResource`
對應 C0 核准的 `MyFhirSdk.Core`。既有 MVP generator 暫由其 loaded definitions 建立 mapping view；
C3/C7 model pipeline 將直接使用 C2 graph/scope。

Architecture test 驗證 static fallback 欄位不存在，且 complex type input 是 required
constructor argument。

## 7. Tests

Tests 覆蓋：

- official 307-node/10,299-edge formal graph 與 disposition/edge counts
- every edge target resolves to a graph node
- canonical ordering 與 reversed-input determinism
- missing type、missing contentReference
- inheritance kind mismatch 與 cycle
- self/mutual references、targetProfile、contentReference
- selected closure、external terminal 與 deterministic generation plan
- full-scope unsupported primitive diagnostics
- mapper fallback architecture guard

Release verification：

- CodeGen：303 passed、0 failed
- Solution：559 passed、0 failed、1 skipped

## 8. C3 handoff

C3 應直接消費 `DefinitionDependencyGraph` 與 `GenerationScope`：

- declaration candidates 只來自 `GenerationScope.GeneratedModels`
- base/type/profile/contentReference/Backbone owner 只讀 resolved edges
- external/primitive target 不可重新生成 declaration
- renderer 不得讀 raw DTO、inventory 或自行解析 canonical
- unsupported primitive diagnostics 必須在建立 partial IR 前終止
