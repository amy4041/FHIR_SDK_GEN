# MyFhirSdk CodeGen Phase D D1 實作紀錄

Version 1.0

- 狀態：Completed
- 基準：D0 decisions Version 1.0
- Runtime contract：`phase-a-v1+c4-primitives-v1`
- Descriptor schema：`1`
- Descriptor SHA-256：`4923075ae0eb4ac88fefe6292b68a893b2e55d25e79c1746dfffa0bc266ce210`

## 1. 交付內容

D1 新增 `CodeGen/Policy/runtime-contract.json` 作為 CodeGen 唯一可消費的 Runtime shape
descriptor，並新增：

- strongly typed descriptor DTO；
- 嚴格 loader；
- schema/identity/role/graph validator；
- deep read-only `RuntimeContractView`；
- descriptor exact-byte SHA-256；
- SDK reflection architecture gate。

D1 尚未把 `ModelMetadataIrBuilder` 或 Roslyn compilation 接到 contract view；production
pipeline 仍維持 D0 baseline，相關切換分別由 D2、D3 負責。

`compilerReference.sha256` 目前固定 D0 entry baseline 的 Release SDK bytes。D3 materialize
package-owned compiler asset 時必須以實際 staged asset 更新並驗證此值；不得從當次任意
`bin/obj` 輸出推測或 fallback。

## 2. Descriptor scope

Descriptor 包含 13 個手寫 Runtime/foundation/bootstrap symbols：

- model root 與 extension-value marker；
- Base、Element、BackboneElement、BackboneType、DataType；
- PrimitiveType、Resource、DomainResource；
- Extension、Meta、Narrative。

它只包含 CodeGen 需要特殊 composition 的三個 declared slots：

- `Extension.Value`；
- `Meta.Security`；
- `Meta.Tag`。

Descriptor 不包含 `SimpleQuantity`、generated datatypes、Resources 或完整 FHIR inventory。
FHIR canonical/kind/ownership 仍由 official package 與 model ownership policy 負責。

## 3. Validation contract

Loader 接受 UTF-8 no-BOM、LF JSON，並在建立 view 前完成：

- duplicate JSON property 與 unknown member rejection；
- supported schema、required field、version/hash format validation；
- symbol/slot identity uniqueness與ordinal ordering；
- known role、role cardinality、kind/modifier/generic arity validation；
- Runtime inheritance relation與cycle validation；
- declared slot owner/element cross-reference validation；
- compiler reference與Runtime assembly/target framework cross-link validation。

成功後的 collections 都是 read-only，consumer 不接觸 mutable JSON DTO。相同 bytes 會得到
相同 descriptor hash 與 ordinal-identical view。

## 4. Diagnostics

| Code | Meaning |
| --- | --- |
| `FSG0100` | descriptor path/read failure |
| `FSG0101` | invalid UTF-8 or JSON |
| `FSG0102` | unsupported descriptor schema |
| `FSG0103` | invalid schema field、identity、ordering、shape或cross-link |
| `FSG0104` | duplicate JSON property、symbol或slot identity |
| `FSG0105` | unknown symbol/slot role |

Diagnostics 依 code、message ordinal 排序；validator 不回傳 partial view。

## 5. Runtime shape gate

Architecture test 從 descriptor 產生 expectations，再以 reflection 比對目前
`MyFhirSdk.dll`：

- assembly name/version/public key token；
- CLR full name、class/interface、base type；
- abstract/sealed、generic arity與descriptor-declared interfaces；
- declared property name/type、collection與nullable shape。

因此 Runtime declaration 改變但 descriptor 未同步時會直接使 architecture test 失敗，
不需要在測試中複製第二份 CLR shape 清單。

## 6. D1 exit criteria

- repository descriptor 可重複載入並產生相同 view/hash；
- missing required role、duplicate、wrong base、wrong arity、unknown role與unsupported schema
  都 fail-fast；
- descriptor 與手寫 Runtime shape 一致；
- descriptor 無 generated concrete model inventory；
- Phase C generated manifest、831 artifacts與production behavior不變。
