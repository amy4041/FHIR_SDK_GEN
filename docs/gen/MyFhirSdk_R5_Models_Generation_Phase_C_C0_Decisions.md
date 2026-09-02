# MyFhirSdk R5 Models Generation Phase C0 Baseline 與決策

Version 0.8

- 文件狀態：Complete
- 適用範圍：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、MyFhirSdk、.NET 9
- 工作分支：`feat/phase-c0-model-generation-baseline`
- 起始 commit：`d0cbf44425b5a8a5dc4180258aee6bf1613e2554`
- 實作指引：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md`
- Phase C 交接契約：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Handoff.md`

## 1. 文件目的

本文件記錄 Phase C0 的 baseline、已核准決策、待決事項、驗證方式與退出條件。C0 不改變
production behavior；後續 C1-C9 必須以本文件中 Accepted 的決策為實作輸入。

## 2. Baseline 狀態

| Gate | 狀態 | 證據 |
|---|---|---|
| Phase B primitive regeneration | Passed | `Generated/R5/Primitives` regeneration 無 git diff |
| Official R5 package identity | Passed | C0-001 lock 與離線 identity tests |
| Release solution tests | Passed | 533 passed、0 failed、1 skipped |
| R5 model public API snapshot | Passed | 獨立 approved snapshot、完整範圍與 determinism tests |
| Deterministic inventory reconnaissance | Passed | Approved JSON snapshot、reordered-input 與 two-run tests |

## 3. 決策摘要

| ID | 決策 | 狀態 |
|---|---|---|
| C0-001 | Official R5 package input、identity 與 offline CI policy | Accepted |
| C0-002 | R5 model public API baseline 與 disposition | Accepted |
| C0-003 | Assembly、base 與 bootstrap ownership | Accepted |
| C0-004 | Namespace、filename 與 collision naming | Accepted |
| C0-005 | Backbone naming 與 public placement | Accepted |
| C0-006 | Choice/open type public representation | Accepted |
| C0-007 | Validation capability matrix | Accepted |

## 4. C0-001：Official R5 package input

- 狀態：Accepted
- Package ID：`hl7.fhir.r5.core`
- Package version：`5.0.0`
- FHIR version：`5.0.0`
- Package type：`Core`
- License：`CC0-1.0`
- 官方來源：`https://www.hl7.org/fhir/hl7.fhir.r5.core.tgz`
- Archive size：`17057450` bytes
- SHA-256：`74b27cd1bfce9e80eaceac431edf230b0945a443564fbf5512f82e5fa50a80d4`
- Repository path：
  `Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz`
- Lock path：`CodeGen/Policy/r5-package-lock.json`
- Archive root：`package`
- CI input policy：offline

### 4.1 決策

1. Phase C 的正式 full-batch 輸入固定為上述 package bytes，不以 URL 或檔名單獨判定身分。
2. Package archive 納入 repository test fixtures，讓 CI 與本機測試不需網路即可使用相同
   bytes。
3. Package ID、package version、FHIR version、package type、license 與 SHA-256 由 versioned
   lock 固定。
4. Hash 或 `package/package.json` identity 不符時，必須在 inventory construction 前失敗。
5. Production generation 不得在執行期間自動下載或改用最新版 package。
6. 若未來更換 package bytes、來源或保存政策，必須在同一個 change set 更新 lock、測試與
   本決策紀錄。

### 4.2 理由與影響

- 固定 package bytes 可避免遠端內容、快取或列舉差異破壞 deterministic generation。
- Repository fixture 提供可重現的 offline CI input，但會增加約 17 MB repository 大小。
- URL 保留作 provenance；SHA-256 才是正式 artifact identity。
- C1 package loader 與 C7 manifest 必須消費相同 identity，不能建立另一份 package
  version/hash 常數。

### 4.3 驗證與退出條件

`R5CorePackageFixtureTests` 必須離線驗證：

- archive bytes 符合 lock 中的 SHA-256；
- archive 含 `package/package.json`；
- package name、version、type、license 與 FHIR versions 符合 lock；
- test project 從 committed fixture 複製 archive 與 lock，不進行網路存取。

C0-001 在上述測試通過且完整 solution tests 無回歸後完成。

## 5. Deterministic inventory reconnaissance

- 狀態：Completed
- Approved snapshot：
  `Tests/CodeGen/Fixtures/FhirPackages/R5/structuredefinition-reconnaissance.approved.json`
- Analyzer：`Tests/CodeGen/Reconnaissance/R5PackageReconnaissance.cs`
- Tests：`Tests/CodeGen/Reconnaissance/R5PackageReconnaissanceTests.cs`

Reconnaissance 直接串流讀取 C0-001 固定的 `.tgz`，只納入 JSON 內容中
`resourceType == StructureDefinition` 的 entry。它不以檔名或 `.index.json` 作為 definition
分類真相，且不把結果接入 production generator。

### 5.1 Definition inventory 結果

| 分類 | 數量 |
|---|---:|
| 全部 StructureDefinitions | 307 |
| `complex-type` | 51 |
| `logical` | 10 |
| `primitive-type` | 21 |
| `resource` | 225 |
| `specialization` | 230 |
| `constraint` | 66 |
| derivation missing | 11 |
| abstract | 11 |
| concrete | 296 |

`specialization` 由 47 個 complex types、21 個 primitives 與 162 個 Resources 組成。這 230
個 definitions 的 type、canonical 與 source identity 均完整唯一，且全部包含 snapshot。

完整 package 中有兩個缺少 version 與 snapshot 的 definitions：

- `package/StructureDefinition-example-composition.json`
- `package/StructureDefinition-example-section-library.json`

兩者皆為 `kind = resource`、`derivation = constraint`，不屬於 Phase C specialization model
generation candidates。C1 仍須先明確分類，不能因缺少 snapshot 而靜默略過。

### 5.2 Model shape 結果

| Shape | 全部 definitions | Specializations |
|---|---:|---:|
| Snapshot elements | 15464 | 9554 |
| Choice elements | 486 | 261 |
| Choice type alternatives | 2518 | 1359 |
| `contentReference` elements | 153 | 78 |
| Slicing elements | 255 | 66 |
| Constraints | 17843 | 10971 |
| Binding elements | 2574 | 1650 |
| Fixed elements | 87 | 0 |
| Pattern elements | 33 | 0 |

Approved snapshot 另保存上述 shape 依 kind 的分布、完整 base canonical 分布、missing identity
source 與 duplicate type source 明細。

### 5.3 對 C1-C3 的約束

1. 完整 package 的 `type` 只有 241 個唯一值；constraint Profiles 合法重用 base model
   type，因此 C1 不得在分類前以 `type` 作為全 package 唯一 key。
2. 全部 307 個 canonical 與 source identity 均唯一；C1 應保存並驗證這兩種 identity。
3. Specialization definitions 的 230 個 type 均唯一；constraint、logical、primitive 與
   model specialization 必須在建立 generation scope 前分流。
4. `Base` 是唯一缺少 `baseDefinition` 的 root definition；其他 missing derivation 包含
   `Base` 與 10 個 logical definitions，C1 必須明確分類而非推測。
5. Choice、`contentReference`、slicing 與 constraints 的實際數量證明 C3 IR 必須直接表達
   這些 shape，不能延續 MVP 的 single-type/direct-child 假設。
6. Reconnaissance snapshot 只作為 regression evidence，不是 C1 production inventory 或
   mapping 輸入。

### 5.4 Determinism gate

Tests 必須證明：

- 正式 package 重新分析後與 approved JSON snapshot byte-for-byte 相同；
- definitions 反轉輸入順序後輸出相同；
- 同一 package 連續兩次分析後輸出相同；
- JSON 採 ordinal ordering、UTF-8/LF 語意，且不包含時間戳、絕對路徑或隨機值。

## 6. C0-002：R5 model public API baseline

- 狀態：Accepted
- Approved snapshot：`Tests/Architecture/ApprovedR5ModelApi.txt`
- Formatter：`Tests/Architecture/R5ModelPublicApiSnapshot.cs`
- Tests：`Tests/Architecture/R5ModelPublicApiSnapshotTests.cs`

### 6.1 Baseline 範圍

Snapshot 固定目前 68 個公開 model types：

| Namespace / scope | 型別數 |
|---|---:|
| `MyFhirSdk.Types` | 17 |
| `MyFhirSdk.Resources` | 39 |
| 明確選取的 `MyFhirSdk.Core` model/bootstrap contracts | 12 |

Core 範圍為 `BackboneElement`、`BackboneType`、`Base`、`DataType`、
`DomainResource`、`Element`、`Extension`、`FhirObject`、
`IFhirExtensionValue`、`Meta`、`Narrative` 與 `Resource`。這些型別雖與既有
Runtime public API snapshot 部分重疊，仍刻意納入本 baseline，以保護 model inheritance
與 bootstrap 邊界。

Approved snapshot 目前固定 67 個 public/protected constructors 與 420 個 properties，並
記錄：

- 型別的 accessibility、class/interface/enum/abstract/sealed、base type 與公開 interface；
- constructor 與 property 的 public/protected accessibility；
- property type、nullable annotation、集合泛型形狀與 getter/setter accessibility；
- property 的 abstract/virtual/override dispatch；
- `JsonPropertyName` 所指定的 wire name。

### 6.2 決策與 disposition

1. 此 snapshot 是後續 Phase C generation 的相容性 regression oracle。C1-C7 的變更若造成
   public model API 差異，必須在同一 change set 中說明差異、更新對應 C0 決策並明確審核
   snapshot，不能以自動重產方式直接接受。
2. `Types`、`Resources` 與上述 Core contracts 是「目前需保護的公開面」，不代表 C0-002
   已決定其永久 assembly、source ownership 或生成責任；這些由 C0-003 至 C0-006 決定。
3. Primitive public API 已由 Phase B baseline 保護，因此不重複納入。Runtime services、
   serialization、validation 與非公開 metadata 仍由既有
   `Tests/Architecture/ApprovedPublicApi.txt` 保護。
4. Snapshot 保存現況，不宣告所有手寫 shape 都是最終正確設計。例如 `SimpleQuantity`
   在官方 package 中屬 constraint Profile，bootstrap/core 型別亦可能維持手寫；C1
   inventory 與後續 ownership 決策必須先明確處置，不能由 baseline 反推生成範圍。

### 6.3 驗證與更新規則

Tests 必須證明：

- 實際公開面與 approved snapshot 完全相同；
- scope 恰為 68 個型別，且三個 namespace/scope 的數量固定；
- 反轉型別輸入順序後輸出不變；
- nullable、集合、JSON wire name 與 abstract/override 等相容性關鍵 shape 被 formatter
  明確記錄。

Snapshot 採 ordinal ordering 與 LF，且不包含時間戳、絕對路徑或隨機值。

## 7. C0-003：Assembly、base 與 bootstrap ownership

- 狀態：Accepted
- Decision source：`CodeGen/Policy/r5-model-ownership-policy.json`
- Tests：`Tests/CodeGen/Policy/R5ModelOwnershipPolicyTests.cs`

### 7.1 Assembly 決策

Phase C 維持目前的單一 SDK assembly：

| Artifact / responsibility | Phase C assembly |
|---|---|
| Runtime contracts 與 engines | `MyFhirSdk` |
| Generated R5 primitives、Types、Resources、Backbones 與 metadata | `MyFhirSdk` |
| Generator executable | `MyFhirSdk.CodeGen` |

Runtime 與 R5 Models 在 Phase C 是邏輯責任邊界，不是實體 assembly 邊界。不得在 C1-C9
自行新增 `MyFhirSdk.Runtime` 或 `MyFhirSdk.R5.Models` project，也不得移動 public types
造成 assembly-qualified identity 改變。拆分 assembly 必須另立 ADR，說明 package/versioning、
dependency direction、metadata composition、migration 與 API compatibility。

`MyFhirSdk.CodeGen` 在 Phase C 可繼續以 ProjectReference 參考 `MyFhirSdk.csproj`，供
Roslyn compilation 與 Runtime contract validation 使用。這不授權 CodeGen 以現有手寫
`Types` 或 `Resources` 作 inventory、mapping 或 dependency 真相。是否改為較小的明確
Runtime reference 延至 Phase D local-tool packaging 前重新評估。

### 7.2 Runtime-only contracts

下列 CLR 型別不由一般 R5 model class generator 宣告，持續由 Runtime 手寫擁有：

| CLR contract | Role |
|---|---|
| `MyFhirSdk.Core.FhirObject` | SDK model root |
| `MyFhirSdk.Core.IFhirExtensionValue` | Extension value dispatch marker |
| `MyFhirSdk.Core.PrimitiveType<T>` | generated primitive wrapper base |

CodeGen 可以引用及映射這些 contracts，但不得生成同名 declaration。

### 7.3 Official definition external nodes

下列 11 個 official StructureDefinitions 必須進入 C1 inventory 與 C2 dependency graph，但
其 class declaration 在 Phase C 標記為 `external-handwritten`：

| FHIR type | Kind | Abstract | Declaration owner |
|---|---|---:|---|
| `Base` | `complex-type` | yes | Runtime foundation bootstrap |
| `Element` | `complex-type` | yes | Runtime foundation bootstrap |
| `BackboneElement` | `complex-type` | yes | Runtime foundation bootstrap |
| `BackboneType` | `complex-type` | yes | Runtime foundation bootstrap |
| `DataType` | `complex-type` | yes | Runtime foundation bootstrap |
| `PrimitiveType` | `complex-type` | yes | Runtime primitive bootstrap |
| `Resource` | `resource` | yes | Runtime foundation bootstrap |
| `DomainResource` | `resource` | yes | Runtime foundation bootstrap |
| `Extension` | `complex-type` | no | R5 versioned bootstrap |
| `Meta` | `complex-type` | no | R5 versioned bootstrap |
| `Narrative` | `complex-type` | no | R5 versioned bootstrap |

七個 foundation 型別承擔 generated model 的繼承 contract；official `PrimitiveType`
映射至 Runtime 的 `PrimitiveType<T>`，其 closed generic base 由 Phase B primitive policy
決定。後三個型別雖是 concrete R5
datatypes，但目前被 base properties 直接引用，且 `Extension` 參與 Parser/Serializer 的
choice dispatch，因此本階段保留為版本化 bootstrap，可避免同名 declaration、bootstrap
reference cycle 與未核准的 C0-002 API 差異。

### 7.4 Generation 與 graph 規則

1. Inventory 必須保留並驗證上述 11 個 official definitions；不得因其 declaration 不生成
   而在載入階段略過。
2. Dependency graph 必須以 canonical 將這些 definitions 建成可解析的 external nodes，
   並映射至 policy 指定的 CLR type。
3. Phase C 不得為 external definition node render class declaration；若 full batch 出現
   同一 CLR identity 的 generated source，必須在 write 前失敗。
4. Generated datatypes、Resources 與 Backbones 可以繼承或引用 external nodes。
5. Declaration ownership 不等於 metadata ownership。C6 仍可為 external bootstrap types
   生成 parser、serializer 或 validation metadata，但不得重新宣告 class。
6. C7 manifest 必須記錄 ownership policy identity，並區分 generated declarations 與
   external definition dependencies。
7. C8 不刪除上述手寫 declarations。未來若要生成其中任一型別，必須先核准 migration
   decision，更新 policy、C0-002 API snapshot、Runtime contract tests 與原子切換計畫。

### 7.5 驗證與退出條件

Policy tests 必須證明：

- assembly mode、Runtime contracts 與拆分 ADR gate 均明確且唯一；
- 11 個 external definition nodes 的 canonical 唯一；除 official `PrimitiveType` 刻意映射
  至既有 Runtime generic contract 外，CLR ownership 必須明確；
- policy identity、kind、abstract 與 base canonical 符合 C0-001 固定的 official package；
- 所有 policy CLR types 存在於目前 `MyFhirSdk` assembly；
- migration rules 禁止 Phase C 產生重複 bootstrap declarations，並要求 inventory/graph
  保留及解析這些 nodes。

## 8. C0-004：Namespace、filename 與 collision naming

- 狀態：Accepted
- Decision source：`CodeGen/Policy/r5-model-naming-policy.json`
- Tests：`Tests/CodeGen/Policy/R5ModelNamingPolicyTests.cs`

### 8.1 Namespace 與 output directory

| Category | Namespace | Repository output |
|---|---|---|
| Phase B supported primitive wrappers | `MyFhirSdk.Primitives` | `Generated/R5/Primitives` |
| Complex datatype specialization，排除 external nodes | `MyFhirSdk.Types` | `Generated/R5/Types` |
| Resource specialization，排除 external nodes | `MyFhirSdk.Resources` | `Generated/R5/Resources/{ResourceOwner}` |
| Generated R5 model metadata | `MyFhirSdk.ModelMetadata.R5` | `Generated/R5/ModelMetadata` |
| C0-003 external definitions | 由 ownership policy 指定 | 不輸出 declaration |
| Resource-owned Backbone | `MyFhirSdk.Resources` | `Generated/R5/Resources/{ResourceOwner}` |

Phase C 不為 datatype 或 Resource 建立每型別子 namespace，也不依 package entry path、base
type 或 reference target 改變 namespace。Constraint Profiles 與 logical models 不在 Phase C
model declaration scope，因此不取得 fallback namespace。

### 8.2 Top-level type 與 filename 規則

1. Complex datatype 與 Resource 的 C# type name 由官方 `StructureDefinition.type` 經
   `CSharpNameConverter.ConvertTypeName` 取得。
2. Primitive wrapper name 只由 Phase B primitive generation policy 決定，不重新推導。
3. 每個 top-level model file 只含一個 public top-level model declaration，檔名固定為
   `{CSharpTypeName}.g.cs`。
4. Resource 及其所有 Backbone artifacts 依 owner 放在
   `Generated/R5/Resources/{ResourceOwner}/`；top-level Resource 的 owner 是自己。
   `ResourceOwner` 使用核准的 C# Resource type name。
5. Model metadata 檔名採 `{ArtifactIdentity}.g.cs`；manifest 固定為
   `model-generation-manifest.json`。
6. Artifact path 使用 repository-relative `/` 語意，必須同時符合 Windows/Linux portable
   filename 規則；禁止 rooted path、`..`、Windows device name 與其他 unsafe segment。
7. Symbol identity 使用 ordinal comparison；output path uniqueness 使用
   ordinal-ignore-case comparison；輸出與 diagnostics 使用 ordinal ordering。

Official package reconnaissance 在排除 C0-003 的 11 個 external definitions 後得到：

| Generation category | Top-level candidates |
|---|---:|
| Complex datatypes | 39 |
| Resources，包含 abstract Resource specializations | 160 |
| 合計 | 199 |

199 個候選經核准 namespace 與 converter 映射後，沒有 fully-qualified type identity collision，
也沒有 case-insensitive output path collision。

### 8.3 Property 與 wire name

- 一般 property name 由完整 element id 的最後一段經
  `CSharpNameConverter.ConvertPropertyName` 取得。
- FHIR JSON wire name 永遠保存官方 element name，不能從衝突後的 CLR 名稱反推。
- 當核准的 CLR rename 無法還原 wire name 時，generated property 必須明確使用
  `JsonPropertyName` 或等價 generated metadata。
- 不允許依輸入順序加入 `2`、`3` 等自動 suffix，也不允許靜默遮蔽 inherited member。

Official specialization definitions 的 direct、non-choice members 只有兩個名稱與 declaring
type 相撞，核准 mapping 如下：

| Element id | CLR property | JSON name | 理由 |
|---|---|---|---|
| `Expression.expression` | `ExpressionValue` | `expression` | 避免 `Expression.Expression` |
| `Reference.reference` | `ReferenceValue` | `reference` | 避免 `Reference.Reference`，維持 C0-002 API |

Concrete Resource 另保留 synthetic `ResourceType` member：public read-only override，JSON
name 為 `resourceType`。若官方或 generated member 與 `ResourceType` 相撞，必須失敗，不
得改名或覆蓋。

### 8.4 Collision gate

在 render/write 前必須完成以下 collision checks：

1. fully-qualified top-level CLR identity；
2. portable、case-insensitive output path；
3. property 與 declaring type name；
4. 同一 declaring type 的 declared members；
5. 完整 inheritance closure 的 inherited members；
6. `ResourceType` 等 synthetic/reserved members；
7. C0-005/C0-006 決定後的 Backbone 與 choice member identities。

只有 policy 中列出的 explicit rename 可以解決已知 collision。其他 collision 必須產生依
element identity ordinal 排序的 diagnostic，並在 render/write 前失敗。

### 8.5 後續但不可自行推導的項目

- Resource-owned Backbone 的 namespace、public identity、filename 與 placement 已由
  C0-005 決定。
- Choice member 與 open type 的 property naming/representation 由 C0-006 決定。
- `Extension.value[x]` 維持 C0-003 bootstrap declaration；其 open-type representation
  仍屬 C0-006，不由本 naming policy 改變。

### 8.6 C0-003 reconnaissance correction

C0-004 全 package naming reconnaissance 發現 official
`http://hl7.org/fhir/StructureDefinition/PrimitiveType`。C0-003 ownership policy 已補為第
11 個 external definition node，映射至 Runtime `PrimitiveType<T>` bootstrap；否則它會被
錯誤規劃為 `MyFhirSdk.Types.PrimitiveType.g.cs`。Ownership tests 會直接對 official
package 驗證此 identity、kind、abstract 與 base canonical。

## 9. C0-005：Backbone naming 與 public placement

- 狀態：Accepted
- Decision source：`CodeGen/Policy/r5-backbone-policy.json`
- Tests：`Tests/CodeGen/Policy/R5BackbonePolicyTests.cs`

### 9.1 Official R5 Backbone inventory

C0-001 固定的 official package 在 Phase C specialization scope 中包含：

| Shape | 數量 |
|---|---:|
| Resource-owned `BackboneElement` nodes | 613 |
| 含 Backbone nodes 的 Resource owners | 141 |
| Inline `BackboneType` nodes | 0 |
| 深度 1 | 384 |
| 深度 2 | 170 |
| 深度 3 | 47 |
| 深度 4 | 12 |

Backbone node identity 取自完整、唯一的 snapshot element id。Constraint Profiles 與 logical
models 不在 Phase C model declaration scope，不能將其中的 inline shape 混入上述 inventory。
目前 package 若在核准 scope 出現 `BackboneType` inline node，須視為 inventory/policy
mismatch 失敗，不能自行套用 `BackboneElement` placement。

### 9.2 Public placement 與 class shape

每個核准的 Resource-owned Backbone：

- 生成為 public top-level sealed class；
- namespace 為 `MyFhirSdk.Resources`；
- 直接繼承 `MyFhirSdk.Core.BackboneElement`；
- 輸出至
  `Generated/R5/Resources/{ResourceOwner}/{CSharpTypeName}.g.cs`；
- 每個檔案只含一個 public top-level model declaration。

Top-level Resource 與其全部 descendant Backbones 共用 owner folder，例如：

```text
Generated/R5/Resources/Patient/
├─ Patient.g.cs
└─ PatientContact.g.cs

Generated/R5/Resources/Claim/
├─ Claim.g.cs
├─ ClaimItem.g.cs
├─ ClaimBodySite.g.cs
├─ ClaimDetail.g.cs
└─ ClaimSubDetail.g.cs
```

Owner folder 只屬於 repository artifact placement，不建立子 namespace。上述型別的 public
CLR identity 仍是 `MyFhirSdk.Resources.PatientContact`、
`MyFhirSdk.Resources.ClaimDetail`，不是
`MyFhirSdk.Resources.Patient.PatientContact`。

巢狀 Backbone 是 containment/ownership 關係，不是 CLR inheritance。例：
`Claim.item.detail.subDetail` 的 class 仍直接繼承 `BackboneElement`，不繼承表示
`Claim.item.detail` 的 class。

選擇 public top-level placement 是為了維持 C0-002 現有 API、避免 deeply nested CLR type
identity，並讓 Parser、Serializer、Validator metadata 使用穩定的完整型別名稱。

### 9.3 一般命名規則

一般 CLR name 由完整 element id 的所有 segments 依序經
`CSharpNameConverter.ConvertTypeName` 後串接：

| Element id | 一般 CLR name |
|---|---|
| `Patient.contact` | `PatientContact` |
| `Bundle.entry.request` | `BundleEntryRequest` |
| `Coverage.costToBeneficiary.exception` | `CoverageCostToBeneficiaryException` |
| `ValueSet.expansion.contains.property.subProperty` | `ValueSetExpansionContainsPropertySubProperty` |

完整 owner/path 必須保留；不得只使用 leaf name、不得依 traversal order 縮短，也不得自動
加入數字 suffix。

### 9.4 C0-002 compatibility overrides

目前 32 個公開手寫 Backbone classes 中有 29 個直接符合完整 path 規則。下列三個名稱以
explicit policy override 保留：

| Element id | 一般名稱 | 核准 CLR name |
|---|---|---|
| `Claim.item.bodySite` | `ClaimItemBodySite` | `ClaimBodySite` |
| `Claim.item.detail` | `ClaimItemDetail` | `ClaimDetail` |
| `Claim.item.detail.subDetail` | `ClaimItemDetailSubDetail` | `ClaimSubDetail` |

Override 只適用於完整 element id，不建立模糊的 prefix/leaf 規則。若未來發現新 collision，
必須更新 policy、測試與 C0 decision，不能仿照 Claim 名稱自行縮短。

### 9.5 Reference、collision 與 determinism

1. `contentReference` 必須解析至既有 element identity，不得因此建立第二個 Backbone
   declaration。
2. 613 個 Backbone names 必須彼此唯一，並與 160 個 top-level Resource names 在
   `MyFhirSdk.Resources` 中共同檢查。
3. Resource 與 Backbone output path 都必須包含相同的 canonical Resource owner folder。
4. CLR identity 使用 ordinal comparison；output path 使用 ordinal-ignore-case comparison。
5. 套用三筆 overrides 與 owner folders 後，目前不存在 CLR identity 或 portable output
   path collision。
6. Unapproved collision 必須在 render/write 前失敗，diagnostics 依 element id ordinal
   排序。
7. 反轉 Backbone inventory 輸入順序後，element-to-CLR identity snapshot 必須相同。

### 9.6 對後續階段的約束

- C1 inventory 必須保存 snapshot element id 與 element type code。
- C2 graph 必須建立 owner、nested Backbone 與 `contentReference` edges。
- C3 IR 必須保存 owner canonical、完整 path、核准 CLR identity 與 provenance。
- C5 renderer 不得重新從 leaf name 推導 class identity，且必須用 IR 中的 canonical owner
  決定 Resource family directory。
- C7 manifest 必須把 Backbone artifact 與 owner/element identity 對應納入 deterministic
  inventory。
- C8 原子切換時，32 個現有 public Backbone API 必須由同名 generated declaration 接手，
  不得先移除手寫 class。

## 10. C0-006：Choice/open type public representation

- 狀態：Accepted
- Decision source：`CodeGen/Policy/r5-choice-open-type-policy.json`
- Tests：`Tests/CodeGen/Policy/R5ChoiceOpenTypePolicyTests.cs`

### 10.1 Official R5 direct choice inventory

以 C0-003 generation scope 的 specialization definitions 為範圍，並使用 snapshot
`element.base.path` 判斷 declaration owner、排除 derived definition 的 inherited member 後，結果如下：

| Shape | 數量 |
|---|---:|
| Direct `[x]` elements | 259 |
| Type alternatives | 1303 |
| Ordinary closed choices | 250 elements / 817 alternatives |
| Generated open-type choices | 9 elements / 486 alternatives |
| External bootstrap open type | `Extension.value[x]` 1 element |
| Optional / required choices | 198 / 61 |

259 個 choice 的 `max` 全部是 `1`，每個 choice 都有至少兩個 alternatives。R5 package 的
complete datatype set 由 `Extension.value[x]` snapshot 的 54 個 alternatives 固定；generation
scope 中有 9 個 choice 使用完全相同的 set：`ElementDefinition` 的 `defaultValue[x]`、
`example.value[x]`、`fixed[x]`、`pattern[x]`，以及 `Parameters.parameter.value[x]`、
`Task.input.value[x]`、`Task.output.value[x]`、`Transport.input.value[x]`、
`Transport.output.value[x]`。這 9 個 element 核准為 open-type choice，其他 250 個為
ordinary closed choice。若未來 package 出現新的完整-set match，必須先更新 policy，不能
自動改變 public API shape。

### 10.2 Ordinary closed choice public shape

一般 choice 生成「每個 alternative 一個 nullable public property」，不另外生成 aggregate
property。例如：

| Element | Public properties | JSON names |
|---|---|---|
| `Patient.deceased[x]` | `DeceasedBoolean`、`DeceasedDateTime` | `deceasedBoolean`、`deceasedDateTime` |
| `Claim.item.location[x]` | `LocationCodeableConcept`、`LocationAddress`、`LocationReference` | `locationCodeableConcept`、`locationAddress`、`locationReference` |

Choice stem 移除 `[x]` 後使用 `CSharpNameConverter.ConvertPropertyName`；alternative suffix 從
exact FHIR type code 使用 `CSharpNameConverter.ConvertTypeName`，因此 `dateTime` 的 suffix 是
`DateTime`，不是 CLR wrapper name `FhirDateTime`。CLR property type 仍由 validated type mapper
決定；primitive mapping 不得複製到 choice policy。

每個 property 的 JSON name 是 exact FHIR stem 加 FHIR type suffix；primitive metadata partner
使用相同名稱加 `_` prefix。所有 alternatives 即使 choice `min=1` 仍保持 nullable：`min=0`
由 validation metadata 執行 at-most-one，`min=1` 執行 exactly-one。Setter 不得暗中清除其他
alternatives，避免 assignment order 隱藏無效輸入。驗證路徑保留原始 `name[x]` identity。

### 10.3 Open-type public shape

Open type 不展開成 54 個 public properties，而是生成單一 nullable polymorphic property：

- 一般 generated open type 使用 `MyFhirSdk.Core.DataType?`；
- C0-003 external bootstrap `Extension.value[x]` 維持 C0-002 API：
  `MyFhirSdk.Core.IFhirExtensionValue? Value`；
- property name 使用移除 `[x]` 後的 choice stem；
- JSON name 不能從 CLR type name 猜測，必須由 declaring type、element id 與 concrete FHIR
  type 的 generated metadata 解析成 `{stem}{FHIR type suffix}`；
- official 54 個 alternatives 必須全部保留在 IR。

目前 Runtime 已證明 `Extension.Value` 可將 `FhirString`、`HumanName` 與 `SimpleQuantity`
分派為 `valueString`、`valueHumanName` 與 `valueQuantity`。其中 `SimpleQuantity` 的 wire type
是 `Quantity`，也證明不能依 CLR class name heuristic 產生 JSON name。C6 必須把這個 dispatch
擴充為 model-agnostic generated metadata，而不是在 Serializer/Parser 增加 concrete type
branch。

### 10.4 Unsupported primitive 與 fail-fast disposition

259 個 direct choices 中，Phase B unsupported primitive alternatives 的 occurrence 為：
`oid=9`、`time=20`、`uuid=9`、`xhtml=0`。C0-006 不替這些 primitive 新增 mapping，也不允許
改成 `string`/`object`、從 IR 移除或略過 public alternative。C3 必須保存 official
alternative；type resolution 無法由 `primitive-generation-policy.json` 解決時，必須在 render
前回報 deterministic diagnostic。要讓這些 alternatives 可生成與 round-trip，仍需獨立核准
Runtime CLR/codec/validator contract 及 Phase B policy update。

### 10.5 Collision、determinism 與後續約束

1. Choice members 必須與同 declaring type 的 direct、inherited、synthetic members 一起做
   ordinal collision check，JSON names 也必須唯一。
2. 不允許 numeric suffix；未核准 collision 在 render/write 前失敗，diagnostics 依 element
   identity、FHIR type code ordinal 排序。
3. C1 必須保存 snapshot element id、`base.path`、min/max 與 alternatives；C3 必須明確建立
   `ChoiceGroupModel`，renderer 不得重新分類 open type。
4. C5 依 IR 產生 ordinary split properties 或 open polymorphic property；C6 產生 choice
   validation 與 concrete-type JSON dispatch metadata。
5. C8 API migration 必須維持現有 `Patient`、`Practitioner`、`Claim` choice properties 與
   `Extension.Value` identity。

## 11. C0-007：Validation capability matrix

- 狀態：Accepted
- Decision source：`CodeGen/Policy/r5-validation-capability-policy.json`
- Tests：`Tests/CodeGen/Policy/R5ValidationCapabilityPolicyTests.cs`

### 11.1 Validation inventory scope

Matrix 以 209 個 `complex-type`/`resource` specialization definitions 為 model validation
scope。其中 199 個由 Phase C 生成 declaration，10 個是 C0-003 的
`external-handwritten` specialization nodes；缺少 `derivation` 的 external root `Base` 另外保留
為 graph/runtime node。Primitive validation 繼續由 Phase B policy 與 Runtime registry 負責，
constraint Profiles 與 IG-specific profiles 不納入 Phase C core model validation generation。

使用 snapshot `element.base.path` 判斷 direct declaration owner 後，generated scope 如下：

| Shape | 數量 |
|---|---:|
| Direct elements | 5960 |
| Effective elements，不含 definition roots | 9221 |
| `min=0` / `min=1` | 5054 / 906 |
| `max=1` / `max=*` | 4026 / 1934 |
| 有限 collection upper bound | 0 |
| Required scalar，包含 required choice | 847 |
| Required non-choice scalar / required collection | 786 / 59 |
| Direct choice | 259 |
| Direct constraints | 6144，分布於 5956 個 elements |
| Direct bindings | 1475 |
| Direct fixed / pattern / slicing | 0 / 0 / 0 |

1475 個 bindings 依 strength 分為 `example=733`、`extensible=214`、`preferred=97`、
`required=431`。10 個 external specialization nodes 另有 22 個 direct elements、3 個 required
scalars、1 個 open choice、25 筆 constraints、4 筆 bindings 與 3 個 slicing declarations；C6
可替 external declarations 生成 metadata，但不得重新生成 class。

### 11.2 C6 核准的 executable baseline

| Capability | Runtime evidence | C6 disposition |
|---|---|---|
| Primitive format | `PrimitiveFormatRule`、Phase B `PrimitiveRegistry` | graph-wide reuse，不產生 per-property rule |
| Collection integrity | `FhirObjectGraphWalker`、`CardinalityRule` | graph-wide檢查 null list 與 null item |
| Maximum cardinality | single property 或 `IList<T>` public shape | 保存 cardinality；本 baseline 不需額外 upper-bound rule |
| Required scalar | `RequiredFieldRule.For` | 生成 direct required non-choice entries |
| Required collection | `RequiredFieldRule.ForList` | 生成 direct `1..*` entries |
| Ordinary choice | `ChoiceElementRule.AtMostOne/ExactlyOne` | 依 C0-006 生成 choice entries |
| Open-type presence | polymorphic property、`RequiredFieldRule.For` | 僅在 `min=1` 時生成 required entry |

Generated scope 的 ordinary choice 共 250 個，其中 61 個產生 exactly-one、189 個產生
at-most-one。9 個 generated open types 全為 optional，因此不產生 choice group rule；
`Extension.value[x]` 也是 optional single property。Required rules 與 choice rules 必須分開，
不得對 required choice 的每個 nullable alternative 各自生成 required rule。

現有 `ResourceRuleRegistry` 以 concrete type 取得 rules；C6 必須為每個 concrete runtime type
組成 deterministic effective rule set，使 base/Backbone rules 不會因 CLR inheritance 遺失。
Rule algorithm 屬於 model-agnostic Runtime，R5 type/element entries 屬於 generated metadata；
generated classes 不加入 validation methods，Runtime 也不加入 concrete R5 type branches。

### 11.3 Preserve-only capabilities

下列 official metadata 必須由 C1 DTO 與 C3 IR 保存，但目前不能宣稱已由 Validator 執行：

| Capability | 缺少的 Runtime seam | Phase C disposition |
|---|---|---|
| FHIRPath invariants | FHIRPath evaluator contract | 保存 key、severity、expression、source；C6 不生成 executable rule |
| Terminology bindings | terminology validation provider | 保存 strength 與 ValueSet identity；C6 不生成 executable rule |
| Reference target/profile | reference resolver/profile conformance contract | 保存 dependency/profile metadata；不解析或驗證遠端 target |
| Slicing | slice matching contract | core declaration只保存 provenance；profile validation 不在本階段展開 |

啟用上述能力前，必須先核准 model-agnostic Runtime contract 與 policy update。不得把演算法放進
generated model、不得丟棄 official metadata，也不得把目前 baseline 描述為完整 R5
conformance validation。

### 11.4 Zero-occurrence 與 profile boundary

R5 specialization snapshot 中 `fixed[x]` 與 `pattern[x]` occurrence 都是 0；完整 package 中的
87 個 fixed 與 33 個 pattern 全在目前排除的非-specialization scope。C6 不需為 baseline
實作 generic fixed/pattern comparison；若未來核准的 specialization scope 出現任何 occurrence，
必須在 render 前診斷並更新 capability policy，不能略過。

現有 `ProfileValidator` framework 仍可執行明確註冊的 IG package rules，但這不等同自動翻譯
StructureDefinition constraint Profile、FHIRPath、binding 或 slicing。Phase C 不生成
constraint Profile/IG-specific rules。

### 11.5 對後續階段的約束

1. C1 必須載入 cardinality、constraint、binding、fixed、pattern、slicing 與 target profile
   metadata，即使 capability 是 preserve-only。
2. C3 IR 必須區分 executable、preserve-only 與 unsupported/unapproved validation metadata，
   並保存 rule provenance。
3. C6 只生成 matrix 核准的 executable entries；缺少 Runtime seam 的 metadata 不得偷偷轉成
   no-op success。
4. Metadata composition 必須 immutable、duplicate/conflict fail-fast，並依 type、element、rule
   identity ordinal 排序。
5. C7 manifest 必須揭露 executable capability set 與 preserve-only metadata，不得宣稱 full
   R5 validation。

## 12. C0 退出狀態

C0-001 至 C0-007 全部 Accepted。Official package identity、public API、ownership、naming、
Backbone、choice/open type 與 validation disposition 都有 machine-readable policy 或 approved
snapshot、離線測試及 decision record。Phase C 可進入 C1；後續階段不得自行擴張或重新推導
上述決策。
