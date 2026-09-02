# MyFhirSdk R5 Models Generation Phase C1 Definition Inventory

Version 1.0

- 狀態：Complete
- FHIR baseline：R5 `5.0.0`
- Package：`hl7.fhir.r5.core#5.0.0`
- C0 decisions：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_C0_Decisions.md`
- Implementation guide：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md`

## 1. Scope

C1 從 C0 核准的 official package bytes 建立完整、validated、deterministic definition
inventory。這個階段負責 package identity、raw DTO preservation、definition classification、
identity validation 與 provenance；不建立 dependency edges、不選 generation closure，也不產生
C# artifacts。

正式資料流：

```text
IDefinitionPackageInput
        ↓
DefinitionPackageLoader
        ↓ package identity + raw StructureDefinition DTOs
DefinitionInventoryBuilder
        ↓
DefinitionInventory
```

`DefinitionInventoryPipeline` 是正式 composition seam。Inventory item 只能來自 package stream
中的 JSON bytes；entry filename 只作 provenance，是否為 StructureDefinition 由 JSON
`resourceType` 判斷。

## 2. Package input 與 identity

`IDefinitionPackageInput` 將 archive source 與 loader 分離；目前 production adapter 是
`FileDefinitionPackageInput`，測試可使用 memory-backed input。Loader 直接讀取 `.tgz`，不需要
先解壓至 repository 或暫存目錄。

Loader 驗證：

- package id：`hl7.fhir.r5.core`
- package version：`5.0.0`
- package type：`Core`
- `fhirVersions` 包含 `5.0.0`
- `package/package.json` 唯一且存在
- package 至少包含一筆 `StructureDefinition`
- JSON entry 必須可反序列化

Archive SHA-256 與 offline fixture tracking 繼續由 C0-001 package lock tests 負責；C1 不建立
第二份 package identity 常數或下載路徑。

## 3. DTO expansion

`StructureDefinitionDto` 新增 `fhirVersion`。`ElementDefinitionDto` 現在保存：

- `base.path/min/max`
- type `profile` 與 `targetProfile`
- `contentReference`、`sliceName`、raw `slicing`
- constraint `key/severity/human/expression/source`
- binding `strength/description/valueSet`
- `mustSupport`、`isModifier`、`isModifierReason`、`isSummary`、`condition`
- fixed/pattern 等 polymorphic fields 的 raw `JsonElement`

Fixed/pattern 不在 DTO layer 解成 CLR model value；原始 property name 與 JSON value 會完整保留，
由 C3 IR 根據 C0-007 capability matrix 決定 disposition。

## 4. Inventory classification

Official package 的 307 筆 definitions 全部進入 inventory：

| Category | Count | C1 disposition |
|---|---:|---|
| `ModelRoot` | 1 | `Base`，missing derivation/base 合法 |
| `ModelSpecialization` | 209 | C2 model graph candidates |
| `PrimitiveSpecialization` | 21 | Phase B-owned terminal candidates |
| `ConstraintProfile` | 66 | 明確分類，不產生 Phase C model declaration |
| `LogicalModel` | 10 | 明確分類，不產生 Phase C model declaration |

Constraint Profile 合法重用 base FHIR type，因此 type uniqueness 只套用於 model root 與
specializations。Canonical 與 package entry source identity 則在完整 inventory 中必須唯一。

每個 item 保存：

- package entry source identity
- StructureDefinition `id`、`type`、canonical、definition version、FHIR version
- kind、abstract、baseDefinition、derivation、category
- 完整 raw `StructureDefinitionDto`

Items 依 canonical、source identity ordinal 排序，且 exposed collection 為 read-only。

## 5. Validation gates

所有 definitions 都必須具備 `resourceType`、`id`、`type`、canonical、kind 與 abstract flag。
Model/primitive specializations 另須具備：

- definition `version` 與 `fhirVersion`，且皆符合 package FHIR version
- `baseDefinition`
- non-empty `snapshot.element`
- non-empty `differential.element`

`ModelRoot` 同樣驗證 version、FHIR version、snapshot 與 differential，但允許缺少
baseDefinition/derivation。Constraint Profiles 可保留 official package 中缺少 version 或
snapshot 的兩筆 definitions，因為它們不在 model declaration scope。

Identity、version、category、snapshot 或 duplicate error 都會在 C2 graph 建立前使 inventory
失敗。Diagnostics 依 code、source、canonical、message ordinal 排序。

## 6. Tests

Tests 覆蓋：

- DTO metadata 與 raw fixed/pattern preservation
- official package 307 筆 formal inventory 與 category counts
- unconventional archive filename 的 `resourceType` discovery
- package id/version/type/FHIR mismatch
- malformed JSON、missing package document、empty package
- mixed approved kinds 與 legal constraint type reuse
- duplicate specialization type、canonical、source identity
- wrong definition version、missing snapshot、unsupported category
- reversed archive entries 與 reversed loaded definitions determinism
- item provenance、ordering 與 collection immutability

Release verification：

- CodeGen：296 passed、0 failed
- Solution：552 passed、0 failed、1 skipped

## 7. C2 handoff

C2 必須直接使用 `DefinitionInventory`，不得重新掃描 package、從 filename 推導 canonical/type，
或以 `DefaultComplexTypeNames` 建立 whitelist。C2 將負責：

- inheritance 與 property reference edges
- profile/targetProfile、contentReference 與 Backbone ownership edges
- C0 external nodes 與 Phase B primitive terminals
- inheritance cycle/kind compatibility/missing edge validation
- cycle-safe selected generation closure
