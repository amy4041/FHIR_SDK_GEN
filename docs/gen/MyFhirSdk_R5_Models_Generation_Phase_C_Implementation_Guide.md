# MyFhirSdk R5 Models Generation Phase C 實作指引

Version 0.1

- 文件狀態：Proposed，待 C0 baseline 與決策閘門核准
- 適用範圍：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、MyFhirSdk、.NET 9
- Phase B 基準：Completed
- 上位架構文件：`docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`
- Phase C 交接契約：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Handoff.md`
- 前階段指引：`docs/gen/MyFhirSdk_Primitive_Generation_Phase_B_Implementation_Guide.md`

## 1. 文件目的

本文件把 Phase C 的完整 R5 model generation 拆成可實作、可測試、可分批合併且可回復的
Work Packages，並定義各階段的輸入、輸出、完成標準與驗收方式。

上位架構文件決定 Runtime、R5 Models 與 CodeGen 的責任；Phase C handoff 固定 Phase B
交付的 primitive contract；本文件只決定如何實作完整 datatype、Resource、Backbone 與
model metadata generation。若描述衝突，以 handoff 為準，應先修正文檔或建立 ADR/API
decision，不能由實作者自行改變契約。

## 2. Phase C 目標

Phase C 完成後應達成：

1. 從固定版本的官方 R5 package 建立完整且可驗證的 StructureDefinition inventory。
2. 建立 inheritance、type reference、target profile 與 `contentReference` graph，移除手寫
   complex type whitelist。
3. primitive reference 只透過 Phase B validated policy 與 `PrimitiveTypeMappingView` 映射。
4. 生成核准範圍內的 complex datatypes、Resources 與 resource-owned Backbones。
5. 表達 inheritance、cardinality、collection、choice、abstract/open type 與
   `contentReference` model shape。
6. 生成 Parser、Serializer、Validator 所需的 model metadata、Resource factory 與 validation
   rule composition。
7. 生成含 package、policy、CodeGen、Runtime contract 與 artifact identity 的 manifest。
8. 完整 batch 在重排輸入及連續兩次執行時 byte-for-byte 相同。
9. 以可回復的原子 change set 取代核准範圍內的手寫 model 與 metadata entries。
10. 移除 MVP whitelist、preview path、assembly scan 與已完成任務的 transitional adapters。

## 3. 非目標

Phase C 不包含：

- 改變 Phase B primitive wrapper、codec、validator 或 registry contract。
- 為 `oid`、`time`、`uuid`、`xhtml` 自行推導 CLR mapping。
- 實作 Parser、Serializer、Validator 的通用執行 engine。
- 生成 constraint Profile、IG、slice-specific class、ValueSet、CodeSystem 或 SearchParameter。
- 實作通用 Snapshot Generator；正式輸入必須包含已驗證 snapshot。
- 未經 ADR 拆分 Runtime 與 R5 Models assemblies。
- 封裝或發布 .NET local tool；這屬於 Phase D。
- 將現有手寫 `Types/` 或 `Resources/` 當作 inventory 或 mapping 真相來源。

## 4. 目前基準與差距

### 4.1 可重用能力

- Phase B 的 official primitive coverage、17 個 generated wrappers、registry、manifest 與
  deterministic pipeline。
- `PrimitiveTypeMappingView` 與必要注入該 view 的 `CSharpTypeMapper`。
- datatype MVP 的 DTO、loader、selector、parser、IR、mapper、renderer、Roslyn validator、
  transactional writer，以及 5 個 datatype golden fixtures。
- generated datatype 的 Parser、Serializer、Validator runtime contract tests。
- Runtime 的 `IModelMetadataProvider`、`IValidationRuleProvider` 與 immutable registry seam。
- public API、dependency、accessibility 與 generated integration architecture gates。

### 4.2 尚未完成

1. Loader 尚未支援完整 package 與 `kind = resource` inventory。
2. DTO 未完整承載 choice、binding、fixed、pattern、constraint 等 Phase C metadata。
3. selector/parser 只支援直接 child、單一 type，並拒絕 choice、Backbone 與
   `contentReference`。
4. 現有 IR 無 definition category、choice group、Backbone node 或 metadata model。
5. `CSharpTypeMapper.DefaultComplexTypeNames` 仍是 17 筆 MVP whitelist。
6. renderer 固定生成 `DataType`，沒有 Resource、Backbone、choice 或 metadata rendering。
7. `FhirSdkGenerator` 仍是 selected datatype preview pipeline。
8. `R5ModelMetadataProvider` 仍掃描 assembly；`R5ValidationRuleEntries` 仍手寫 concrete rules。
9. base/bootstrap ownership 與 CodeGen 對完整 SDK project 的 reference 尚待處理。

MVP 成功只代表少量 datatype 能接入 Runtime，不代表完整 inventory、model shape 或 metadata
generation 已完成。

## 5. 固定實作原則

### 5.1 單一 primitive 決策來源

```text
official primitive definitions + primitive policy
                    ↓ validate/join
 PrimitiveInventoryPolicyCoverage.Policy
                    ↓
       PrimitiveTypeMappingView
                    ↓ inject
            CSharpTypeMapper
```

不得把 17 筆 mapping 複製到 dictionary、switch、graph、renderer 或 metadata。遇到四個
unsupported primitive 必須回報 diagnostic，不能降級為 `string`、`object` 或略過 property。

### 5.2 Inventory 是 complex/resource mapping 的真相來源

complex datatype、Resource、abstract base 與 Backbone ownership 必須由 validated inventory
及核准的 model-shape decision 決定。手寫 source、assembly scan、fixture 名稱與
`DefaultComplexTypeNames` 都不能成為正式 whitelist。

### 5.3 分離 inheritance graph 與 reference graph

FHIR reference graph 合法包含 self/mutual reference，因此不能對整張 graph 強制
topological sort：

- inheritance graph 必須無環且 base canonical 可解析；
- property reference graph 允許循環，但 edge 必須可解析或指向核准的 Runtime-owned node；
- dependency closure 使用 cycle-safe traversal；
- 互相引用的 source 以完整 batch 一次編譯；
- output 依 ordinal identity 排序，不依賴 DFS 或 filesystem 順序。

### 5.4 分層資料流

```text
package bytes
  → DTOs
  → validated inventory + dependency graph
  → renderer-ready internal models
  → in-memory artifacts + manifest
  → Roslyn/Runtime contract validation
  → transactional write
```

DTO 不做 C# 決策；inventory 解決 identity/dependency；IR 保存 namespace、base、properties、
choice 與 metadata 決策；renderer 不重新解析 StructureDefinition。

### 5.5 Base/bootstrap 與 public API

C0 的預設建議是保持單一 SDK assembly，將目前 Runtime base contracts 視為 external graph
nodes，不生成同名 declaration。`Extension`、`Meta`、`Narrative` 則逐一決定生成或保留為
versioned bootstrap contract。其他 assembly/base design 必須先有 ADR。

C0 還必須固定 namespace、base type、sealed/abstract、property/nullability/collection、
`ResourceType`、choice 命名、`Reference.ReferenceValue` 等 collision naming，以及 Backbone
public placement。未核准的 public API 差異不能因改用 generated source 而無聲發生。

### 5.6 Deterministic 與 fail-fast

missing/duplicate definition、unresolved edge、名稱衝突、unsupported shape、metadata conflict
或 compilation failure 都必須在 write 前失敗。Diagnostics/artifacts 以 ordinal 排序；source
採 UTF-8 without BOM 與 LF；manifest 不含時間戳、絕對路徑、隨機值或列舉順序。

## 6. 目標 artifacts 與 core models

```text
Generated/R5/
├─ Primitives/                  # Phase B owner
├─ Types/
├─ Resources/                  # Resources 與 resource-owned Backbones
├─ ModelMetadata/
└─ model-generation-manifest.json
```

至少需要以下 model families：

| Model | 責任 |
|---|---|
| `DefinitionInventoryItem` | type、canonical、version、kind、abstract、base、source identity |
| `DefinitionDependencyGraph` | inheritance、type、profile、contentReference edges |
| `ModelTypeModel` | category、C# identity、base、properties、provenance |
| `ModelPropertyModel` | element identity、FHIR/JSON name、cardinality、types、order |
| `ChoiceGroupModel` | `[x]` identity、alternatives、min/max、member names |
| `BackboneTypeModel` | owner Resource、path、C# identity、children |
| `ModelMetadataBatch` | Resource factories、datatypes、choice/extension mappings |
| `ValidationMetadataBatch` | required、cardinality、choice 與核准的其他 rules |
| `ModelGenerationBatch` | sources、metadata、manifest 與 diagnostics context |

## 7. C0：固定 baseline 與決策閘門

### 目標與方法

1. 執行 Phase C handoff entry gates，確認 primitive regeneration 無 diff。
2. 固定 `Types/`、`Resources/`、bootstrap 與 metadata 的 API/runtime baseline。
3. 以可重現方式盤點 official package 的 kind、數量、base、choice、contentReference、
   slicing 與 constraint 分布。
4. 核准 package source、URL、SHA-256、CI offline 與 git tracking policy。
5. 核准 namespace、filename、Backbone naming/placement、base/bootstrap ownership、choice/open
   type mapping 與 API disposition。
6. 建立 validation capability matrix：哪些 metadata 由現有 Runtime 執行、哪些需先擴充
   model-agnostic Runtime contract、哪些不在 Phase C scope。

### 完成與驗收

- 無未決的 output、API、base/bootstrap 或 assembly decision。
- baseline Release tests、API snapshot 與 Phase B two-run regeneration 全綠。
- inventory reconnaissance 可由 test/script 重現，不是人工計數。
- hand-written model 只作 regression oracle，不是 generator input。

## 8. C1：載入 official R5 definition inventory

### 方法

1. 建立 package/input abstraction，驗證 package id/version 與 FHIR version。
2. 保留 raw deserialize 與 kind-specific validation，不將 loader 放寬為接受任意 definition。
3. 支援 Phase C scope 的 `complex-type`、`resource` specialization；primitive 繼續走 Phase B。
4. inventory 保存 source、id、type、canonical、version、kind、abstract、base、derivation。
5. 驗證 duplicate type/canonical/source identity；constraint/logical model 明確分類或診斷。
6. unit tests 用小 fixtures；formal batch 使用 C0 核准且有 checksum 的完整 package。

### 完成與驗收

- inventory 只由 package bytes 建立，identity error 在 graph 前失敗。
- reordered files 產生相同 inventory/diagnostics。
- 覆蓋 valid/mixed kind、wrong version、duplicate、malformed、missing snapshot 與 provenance tests。

## 9. C2：建立 dependency graph 與 generation scope

### 方法

1. 建立 inheritance、type、profile/targetProfile、contentReference、Backbone owner、Runtime
   external 與 primitive terminal edges。
2. canonical 採固定 ordinal resolution，不從檔名猜測。
3. inheritance graph 驗證 missing base、kind compatibility 與 cycle。
4. reference graph 允許 cycle；selected scope 必須取得完整 cycle-safe closure。
5. supported primitive 由 `PrimitiveTypeMappingView` 解決；unsupported primitive 直接診斷。
6. graph、closure、generation plan 與 diagnostics 全部 deterministic。

### 完成與驗收

- mapper 不再需要 static complex whitelist。
- 每個 reference 可追蹤到 inventory、primitive policy 或核准 external node。
- 覆蓋 inheritance cycle、self/mutual reference、targetProfile、contentReference、missing edge、
  selected closure 與 reordered-input tests。
- architecture test 禁止 `DefaultComplexTypeNames` 或等價 fallback 回歸。

## 10. C3：擴充 DTO 與 renderer-ready IR

### 方法

1. 依 C0 matrix 擴充 DTO，必要欄位缺失必須診斷。
2. IR 表達 category、base、abstract/sealed、FHIR/JSON name、type alternatives、profiles、
   choice、Backbone、contentReference 與 validation metadata。
3. choice 保存原始 `[x]` identity 與每個 alternative；Backbone 保存 owner canonical/path。
4. contentReference 在 graph layer 解成 target，不交給 renderer 解析字串。
5. 名稱轉換集中在 `CSharpNameConverter`，檢查 type/property/choice/Backbone collision。

### 完成與驗收

- renderer 不讀 DTO 或 inventory；每個 IR member 可回溯 canonical/element id/path。
- unsupported shape 不產生 partial class。
- IR tests 覆蓋 inheritance、choice、abstract target、Backbone、contentReference、cardinality 與
  collision；既有五個 MVP golden 持續通過。

## 11. C4：生成完整 complex datatypes

### 方法

1. 由 graph 決定 base 與 referenced namespace，不重複 inherited properties。
2. 生成一般、abstract、derived datatype，以及核准的 choice/contentReference shape。
3. 對 `Extension`、`Meta`、`Narrative` 套用 C0 disposition。
4. 建立涵蓋簡單、繼承、choice、自我引用、abstract/open target 與 collision 的 golden matrix。
5. 全部 datatype source 一次交給 Roslyn，不能靠手寫同名 concrete type 補 dependency。

### 完成與驗收

- full datatype batch 不依賴 whitelist/handwritten inventory。
- 既有五個 MVP types 的 API/runtime behavior 相容。
- all-datatype Roslyn、golden、round-trip、Validator traversal、API diff 與 determinism tests 通過。

## 12. C5：生成 Resources 與 Backbones

### 方法

1. 建立 `kind = resource` parser/renderer 與 Resource inheritance mapping。
2. concrete Resource 生成固定 `ResourceType`；abstract Resource 不生成 factory。
3. 將 Backbone paths 建成有 owner identity 的 models，再依 C0 naming/placement 生成。
4. 支援 nested Backbone、choice、contained Resource、Resource reference 與 contentReference。
5. selected Resource generation 包含完整 datatype/Backbone closure。

### 完成與驗收

- ResourceType 唯一且正確，Backbone 名稱不受 input order 影響。
- Resource/datatype batch 可共同編譯，未支援 inline shape不被略過。
- 覆蓋 Resource/Backbone golden、Parser concrete/contained/choice round-trip、Validator path、API
  diff 與 existing Resource regression tests。

## 13. C6：生成 model metadata、factory 與 validation composition

### 目標資料流

```text
generated model/choice/validation entries
                 ↓
 same-assembly partial composition
                 ↓
 immutable Runtime metadata/rule registries
                 ↓
       Parser / Serializer / Validator
```

### 方法

1. 生成 concrete Resource name/type/factory 與 concrete datatype inventory。
2. 生成 declared abstract/open datatype、choice 與 Extension `value[x]` resolution metadata。
3. 生成 required、cardinality、choice，以及 capability matrix 核准的其他 validation metadata。
4. 缺少 generic Runtime rule 時，先小幅擴充 model-agnostic internal contract；規則演算法不能
   放入 generated class。
5. composition 驗證 duplicate、conflict、wrong factory target 與 missing entry，並 immutable、
   ordinal deterministic。
6. 保留 provider injection/fake tests，不重新引入 Runtime concrete model branch。

### 完成與驗收

- default provider 不以 assembly scan 決定 R5 inventory，Runtime engine 不列 concrete R5 types。
- metadata 與 model batch 一一 coverage。
- 覆蓋 metadata negative/determinism、abstract Resource/DataType、Extension、choice dispatch、
  required/cardinality/choice validation integration 與 architecture tests。

## 14. C7：manifest、pipeline 與 deterministic output

### Pipeline

1. 驗證 options、package identity 與安全 output root。
2. 驗證 primitive policy coverage。
3. 建立 inventory、graph 與 selected/full plan。
4. 建立全部 model/Backbone/metadata IR。
5. 在記憶體 render 全部 artifacts，驗證 path/type identity 唯一。
6. 生成 manifest，Roslyn compile完整 batch並驗證 Runtime contracts。
7. 全部成功才 transactional commit；取消/失敗保留舊 output。

Manifest 至少記錄 schema、FHIR/package identity與 hash、primitive policy version/hash、CodeGen、
Runtime contract、scope/model policy、artifact inventory/count/path/hash，以及 deferred capability
summary；不得記錄不穩定環境資料。

### 完成與驗收

- CLI 有明確 Phase C mode且不破壞 primitive mode。
- official full-batch、reordered-input、two-run、committed-output、path safety、rollback、
  cancellation、CLI diagnostics/exit code 與 Windows/Linux LF tests 通過。

## 15. C8：整合主 SDK 並原子切換

可依 datatype、Resource/Backbone、metadata 拆 PR，但每個 artifact family 必須在同一個可回復
change set 完成：

1. 加入正式 generated artifacts。
2. 移除或排除同名手寫 declarations/entries。
3. 接上 generated composition，更新 project/architecture gates。
4. build/test 完整 solution。
5. 重新生成並確認 committed output 無 diff。

不得提交「先刪手寫」或「generated 與手寫同名 source 同時編譯」的中間狀態。完成時主 SDK
每個 type/entry 只有一個 owner，public API 只有 C0 核准差異，Runtime behavior 保持相容。

## 16. C9：cleanup 與 Phase D handoff

1. 移除 `DefaultComplexTypeNames`、production preview set 與 fallback mapping。
2. 移除 generated provider 已取代的 assembly scan、手寫 R5 entry list與 temporary adapters。
3. 確認 CodeGen 不以手寫 concrete Types/Resources 完成 generation。
4. 更新 boundaries、handoff status、README 與 operation instructions。
5. 登錄保留 bootstrap types 的 owner/reason/exit criterion。
6. 交接 CodeGen ProjectReference、Roslyn Runtime reference、tool packaging、repository-root
   assumptions 與版本相容性給 Phase D。
7. 加入 architecture tests 防止 decision source 回歸。

## 17. 建議 PR 拆分

| WP | PR 範圍 | 必要成果 |
|---|---|---|
| C0 | baseline、reconnaissance、API/base decisions | 無 production behavior change |
| C1 | package input、loader、inventory | official inventory |
| C2 | dependency graph、closure、derived mapper | 移除 whitelist 的基礎 |
| C3 | DTO 與 IR | 可表達完整 model shape |
| C4 | datatype builders/renderers | full datatype batch |
| C5 | Resource/Backbone builders/renderers | full Resource batch |
| C6 | generated metadata/factory/rules | provider composition |
| C7 | manifest、pipeline、CLI、determinism | safe full batch output |
| C8 | SDK integration與原子切換 | generated 取代手寫 |
| C9 | cleanup、gates、Phase D handoff | Phase C DoD 完成 |

建議 branches：

```text
feat/phase-c0-model-generation-baseline
feat/phase-c1-r5-definition-inventory
feat/phase-c2-model-dependency-graph
feat/phase-c3-model-generation-ir
feat/phase-c4-datatype-generation
feat/phase-c5-resource-backbone-generation
feat/phase-c6-generated-model-metadata
feat/phase-c7-model-generation-pipeline
feat/phase-c8-integrate-generated-models
feat/phase-c9-model-generation-cleanup
```

每個 PR 必須可獨立 build/test，說明 API/artifact/manifest/Runtime contract 影響、temporary
adapter 與 rollback，並提供 negative、determinism 與 failure-before-write evidence。不得混入
Phase D local-tool packaging或 Profile generation。

## 18. Phase C Definition of Done

- official package source/version/hash 可驗證，inventory 完整唯一且 deterministic。
- inheritance 無環；reference/content edges 全部解析或為核准 external target。
- mapper 無 static primitive dictionary或 complex whitelist，並重用 Phase B coverage。
- 核准 datatypes、Resources、Backbones、factories 與 metadata 全部 generated。
- public API 與 C0 disposition 相容；Runtime engines 無 concrete R5 list/branch。
- manifest 記錄完整 input/policy/CodeGen/Runtime/artifact identity。
- output two-run byte-identical、跨平台 LF，失敗或取消不破壞舊 output。
- full-batch Roslyn、SDK、API、architecture、Parser、Serializer、Validator、Client、IG 與 solution
  tests 通過。
- 無同名 handwritten/generated declaration 或重複 metadata entry。
- bootstrap 與 Phase D debt 皆有 owner、理由及 exit criterion。

## 19. 驗收流程

### 19.1 Entry baseline

```powershell
dotnet restore MyFhirSdk.sln
dotnet test MyFhirSdk.sln -c Release --no-restore
dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -c Release --no-restore -- `
  --mode primitive `
  --input Tests/CodeGen/Fixtures/StructureDefinitions/Primitives/R5 `
  --policy CodeGen/Policy/primitive-generation-policy.json `
  --output Generated/R5/Primitives `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
git diff --exit-code -- Generated/R5/Primitives
```

### 19.2 每個 WP gate

```powershell
dotnet format MyFhirSdk.sln --verify-no-changes --no-restore
dotnet test MyFhirSdk.sln -c Release --no-restore
git diff --check
```

C7 後另執行正式 `--mode model` full-batch command，並確認：

```powershell
git diff --exit-code -- Generated/R5
```

### 19.3 靜態輔助檢查

```powershell
rg 'DefaultComplexTypeNames|PrimitiveTypeNames' CodeGen
rg 'GetTypes\(|typeof\((Patient|Bundle|Claim|HumanName|Coding)' ModelMetadata Serialization Validation
rg 'DateTime.Now|UtcNow|Guid.NewGuid|GetFullPath' Generated/R5
rg 'using MyFhirSdk.Resources|using MyFhirSdk.Types' Serialization Validation
```

預期 production code 無 fallback mapping、assembly-scan inventory、不穩定 provenance 或 Runtime
engine 對 concrete R5 namespace 的依賴。靜態搜尋只輔助 review，不取代 architecture tests。

## 20. 主要風險與對策

| 風險 | 對策 |
|---|---|
| 手寫 model 變成 inventory | official package tests；禁止 production whitelist |
| 重建 primitive mapping | mapper 只接受 `PrimitiveTypeMappingView` |
| 合法 reference cycle 被拒絕 | inheritance/reference graph 分離；cycle-safe tests |
| unresolved type 降級成 object | fail-fast，禁止 partial output |
| choice/Backbone 改變 API | C0 disposition、golden/API diff/collision tests |
| base 拆分形成 assembly cycle | 預設 Runtime-owned nodes；其他方案先 ADR |
| renderer 解讀規格 | DTO/inventory/IR/renderer 分層 |
| assembly scan 隱藏 missing metadata | generated coverage join與 architecture gate |
| validation 超過 Runtime 能力 | C0 matrix；只擴充 model-agnostic contract |
| generated/handwritten 重複 | C8 artifact-family 原子切換 |
| 大批 output 中途失敗 | in-memory validation + transactional rollback |
| OS/列舉順序造成 diff | ordinal、UTF-8 no BOM、LF、cross-platform hashes |
| 測試依賴網路 | fixed source/hash與 offline CI input |
| scope 膨脹到 Profile/tool | PR gate；Profile 後續 phase、tool Phase D |

## 21. ADR 與文件邊界

assembly 拆分、base/bootstrap永久 ownership、Backbone public placement、choice/open type public
representation、Runtime public surface、generated namespace/artifact ownership，以及 package
redistribution policy等長期決策應另建 ADR/API decision。private class命名、test位置、builder
拆分等局部細節不需 ADR，但應在 PR 說明。

Phase C 進行時，本文件記錄 WP decisions/results；handoff 只記長期契約與狀態；boundaries 只在
責任或 dependency direction 改變時更新；README 只放正式 command/artifact/status。Phase C
完成後另建 Phase D handoff，交接 local-tool packaging、Roslyn Runtime reference、版本相容性與
pack/install/upgrade smoke tests。
