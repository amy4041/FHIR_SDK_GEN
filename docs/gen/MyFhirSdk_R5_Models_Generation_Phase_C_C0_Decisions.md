# MyFhirSdk R5 Models Generation Phase C0 Baseline 與決策

Version 0.4

- 文件狀態：In Progress
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
| Release solution tests | Passed | 511 passed、0 failed、1 skipped |
| R5 model public API snapshot | Passed | 獨立 approved snapshot、完整範圍與 determinism tests |
| Deterministic inventory reconnaissance | Passed | Approved JSON snapshot、reordered-input 與 two-run tests |

## 3. 決策摘要

| ID | 決策 | 狀態 |
|---|---|---|
| C0-001 | Official R5 package input、identity 與 offline CI policy | Accepted |
| C0-002 | R5 model public API baseline 與 disposition | Accepted |
| C0-003 | Assembly、base 與 bootstrap ownership | Accepted |
| C0-004 | Namespace、filename 與 collision naming | Pending |
| C0-005 | Backbone naming 與 public placement | Pending |
| C0-006 | Choice/open type public representation | Pending |
| C0-007 | Validation capability matrix | Pending |

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

下列型別沒有對應的 R5 model declaration，持續由 Runtime 手寫擁有：

| CLR contract | Role |
|---|---|
| `MyFhirSdk.Core.FhirObject` | SDK model root |
| `MyFhirSdk.Core.IFhirExtensionValue` | Extension value dispatch marker |
| `MyFhirSdk.Core.PrimitiveType<T>` | generated primitive wrapper base |

CodeGen 可以引用及映射這些 contracts，但不得生成同名 declaration。

### 7.3 Official definition external nodes

下列 10 個 official StructureDefinitions 必須進入 C1 inventory 與 C2 dependency graph，但
其 class declaration 在 Phase C 標記為 `external-handwritten`：

| FHIR type | Kind | Abstract | Declaration owner |
|---|---|---:|---|
| `Base` | `complex-type` | yes | Runtime foundation bootstrap |
| `Element` | `complex-type` | yes | Runtime foundation bootstrap |
| `BackboneElement` | `complex-type` | yes | Runtime foundation bootstrap |
| `BackboneType` | `complex-type` | yes | Runtime foundation bootstrap |
| `DataType` | `complex-type` | yes | Runtime foundation bootstrap |
| `Resource` | `resource` | yes | Runtime foundation bootstrap |
| `DomainResource` | `resource` | yes | Runtime foundation bootstrap |
| `Extension` | `complex-type` | no | R5 versioned bootstrap |
| `Meta` | `complex-type` | no | R5 versioned bootstrap |
| `Narrative` | `complex-type` | no | R5 versioned bootstrap |

前七個型別同時承擔 generated model 的繼承 contract。後三個型別雖是 concrete R5
datatypes，但目前被 base properties 直接引用，且 `Extension` 參與 Parser/Serializer 的
choice dispatch，因此本階段保留為版本化 bootstrap，可避免同名 declaration、bootstrap
reference cycle 與未核准的 C0-002 API 差異。

### 7.4 Generation 與 graph 規則

1. Inventory 必須保留並驗證上述 10 個 official definitions；不得因其 declaration 不生成
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
- 10 個 external definition nodes 的 canonical 與 CLR identity 不重複；
- policy identity、kind、abstract 與 base canonical 符合 C0-001 固定的 official package；
- 所有 policy CLR types 存在於目前 `MyFhirSdk` assembly；
- migration rules 禁止 Phase C 產生重複 bootstrap declarations，並要求 inventory/graph
  保留及解析這些 nodes。

## 8. 待決事項

C0-004 至 C0-007 必須根據 model API snapshot、official package reconnaissance 與 Runtime
capability evidence 逐項核准。未核准前，C1-C7 不得自行推導 namespace/filename、Backbone
placement、choice/open type 或 validation disposition。
