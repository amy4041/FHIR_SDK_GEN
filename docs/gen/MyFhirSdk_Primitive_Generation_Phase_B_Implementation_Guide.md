# MyFhirSdk Primitive Generation Phase B 實作指引

Version 1.0

- 文件狀態：In implementation（B0 completed；B1 local completed，cross-platform CI pending）
- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9
- Phase A 基準：Completed（A0-A6，A6 merge commit `7cb4159`）
- 上位架構文件：
  `docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`
- Phase B 交接契約：
  `docs/gen/MyFhirSdk_Primitive_Generation_Phase_B_Handoff.md`
- 前階段實作指引：
  `docs/gen/MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md`

## 1. 文件目的

本文件將 Phase B handoff 中的 primitive generation contract 拆解為可實作、可測試、
可分批合併且可回復的工作項目，定義每個 Work Package 的目標、方法、完成標準與驗收
方式。

三層文件的責任如下：

1. 上位架構文件決定 Runtime、R5 Models 與 CodeGen 的責任及依賴方向。
2. Phase B handoff 是 primitive matrix、policy boundary、wrapper contract 與 Definition of
   Done 的權威來源。
3. 本文件只決定如何在目前 repository 中依序實作及驗收，不得改寫 handoff 的契約。

若本文件與 handoff 對 primitive shape、codec/validator key、可見性或 literal preservation
的描述不一致，以 handoff 為準；應先修正文檔差異，再繼續 production implementation，
不得由實作者自行選擇其中一種行為。

## 2. Phase B 目標

Phase B 完成後應達成：

1. 從固定版本的官方 FHIR R5 StructureDefinitions 建立完整 primitive inventory。
2. 使用單一、版本化且可驗證的 generation policy，為每個官方 primitive 做出
   `supported` 或附理由的 `unsupported` 決策。
3. 生成 handoff matrix 中 17 個薄 primitive wrapper declarations。
4. 生成與 17 個 wrappers 對應的 internal primitive registry composition source。
5. 生成 manifest，記錄 FHIR、policy、CodeGen 與 Runtime contract versions。
6. 讓 `CSharpTypeMapper` 消費 validated policy，不再持有第二份 primitive dictionary。
7. generated source 經 golden、Roslyn、SDK build 與 Runtime contract tests 驗證。
8. generated output 連續執行兩次 byte-for-byte 相同。
9. 完成可回復的切換，以 generated wrappers 與 registry composition 取代手寫版本。
10. 保持 Runtime codec、validator、definition 與 registry 為 `internal`，不新增 public
    `IsValid()`。

## 3. 非目標

Phase B 不包含：

- 生成 complex datatype、Resource、Backbone 或 Profile declarations。
- 生成 Parser、Serializer、Validator engine 或 validation algorithm。
- 重新實作 primitive codec、format validator 或 FHIR JSON primitive metadata 流程。
- 拆分 `MyFhirSdk.Runtime` 與 `MyFhirSdk.R5.Models` 實體 assemblies。
- 使用 `InternalsVisibleTo` 將 Runtime internals 暴露給 generated Models assembly。
- 將 CodeGen 正式封裝或發布為 .NET local tool。
- 移除 handoff bootstrap debt register 中尚未達成 exit criterion 的 public members。
- 擴充 `CSharpTypeMapper` 的 complex type whitelist；該工作屬於 Phase C。
- 實作 HTTP Client、IG/Profile validation 或 transport。

Phase B 可以為 primitive generation 擴充共用 loader、diagnostics、renderer、compilation
validation 與 writer，但不得順帶開始 Phase C 的完整 model generation。

## 4. 目前基準與已知差距

### 4.1 現有實作基準

Phase B 開始時 repository 具有：

- `Primitives/Fhir*.cs`：17 個手寫 primitive wrappers。
- `Primitives/Runtime/PrimitiveRegistry.cs`：手寫 default registry composition。
- `Primitives/Runtime/PrimitiveCodecs.cs`：internal codec 與 literal codec。
- `Primitives/Runtime/PrimitiveValidators.cs`：internal format validators。
- `CodeGen/Loading`、`Parsing`、`Mapping`、`Rendering`、`Compilation`、`Writing`：MVP
  generation pipeline。
- `Tests/Architecture/PhaseBPrimitiveHandoffTests.cs`：generated-style 薄 wrapper 與 Runtime
  seam 的 characterization tests。
- `Tests/Architecture/ApprovedPublicApi.txt`：Phase A 核准的 public API baseline。
- Parser/Serializer primitive fixtures：metadata-only、array alignment、decimal 與 integer64
  literal round-trip cases。

`GeneratedFileWriter` 已提供 UTF-8 without BOM、LF newline、檔名排序、安全路徑、staging、
backup 與 rollback，但 Phase B 仍需驗證它能處理 source 與 manifest 的完整 batch，且不會
覆寫手寫 Runtime source。

### 4.2 已知差距

開始 B1 前必須確認下列差距仍成立，並以測試固定：

1. `CSharpTypeMapper.PrimitiveTypeNames` 是獨立的過渡 mapping，目前未包含
   `integer64`，且不是完整官方 R5 primitive inventory。
2. `StructureDefinitionLoader` 目前只接受 `kind = complex-type`；primitive inventory
   需要支援 `kind = primitive-type`，但不能降低既有 complex datatype validation。
3. `FhirSdkGenerator` 目前只有 datatype selection、parse、render、compile、write pipeline，
   尚未建立 primitive batch 與 generation mode。
4. `PrimitiveRegistry.CreateDefinitions()` 目前直接列出 17 筆手寫 entries。
5. 主 SDK project 會依預設 glob 編譯 repository 下的 `.cs`；若將 generated wrappers
   寫入正式輸出目錄但尚未排除手寫 wrappers，會產生 duplicate type compilation failure。
6. 現有 public wrappers 除 handoff 的最小 constructor shape 外，部分型別仍有 public
   constants 或 `ToString()` overrides。這些 member 已進入 public API snapshot，不能在
   Phase B 切換時無聲消失。

### 4.3 B0 public API 決策閘門

handoff 同時要求「薄 wrapper」及「generated declarations 與 public API snapshot 相容」。
因此 B0 必須逐一分類現有 declared public members：

- 無參數與 CLR value constructors：必須保留。
- `decimal`、`integer64` 的 string constructor 與 `Literal`：必須依 handoff 保留。
- public constants：預設保留相容性；若決定移除，必須有明確 API 相容性決策與核准的
  snapshot 差異。
- `ToString()` overrides：預設保持既有 observable behavior；不得把 JSON codec 或 format
  validation algorithm 搬入 wrapper。若 handoff 的薄 wrapper boundary 無法容納既有行為，
  應先更新 handoff 或建立簡短 ADR/API decision。

在此分類核准前，可以建立 policy/parser/renderer 的測試骨架，但不得切換正式 wrappers
或更新 approved public API baseline。

## 5. 實作原則與固定決策

### 5.1 單一 primitive policy

Phase B 採 repository-managed JSON policy，建議正式路徑為：

```text
CodeGen/Policy/primitive-generation-policy.json
```

選擇 JSON 是為了沿用 `System.Text.Json` 與現有 diagnostics，不增加 YAML runtime
dependency。若 review 決定改用 YAML，必須在 B1 開始前修改本節；同一版本不得同時維護
JSON 與 YAML 兩份權威 policy。

建議的 C# model 與服務放置方式：

```text
CodeGen/Policy/
├─ PrimitiveGenerationPolicy.cs
├─ PrimitiveGenerationPolicyEntry.cs
├─ PrimitiveGenerationPolicyLoader.cs
├─ PrimitiveGenerationPolicyValidator.cs
└─ primitive-generation-policy.json
```

policy 是 wrapper name、CLR type、JSON token、codec key、validator key、literal shape 與
support status 的唯一真相來源。`CSharpTypeMapper`、wrapper renderer 與 registry composition
renderer 必須消費同一個 validated in-memory policy；不得各自建立 dictionary。

codec/validator key 到 Runtime symbol 的轉換可以有一個封閉 resolver，例如將
`decimal-literal` 轉為 `PrimitiveCodecs.Decimal`，但 resolver 只能負責翻譯已驗證的 key，
不能擁有另一份 primitive-to-key 決策。resolver 的 key set 必須與 policy validator 共用，
且由 exhaustive tests 證明沒有未知或未使用 key。

### 5.2 官方 primitive inventory

inventory 必須從使用者指定且版本驗證通過的官方 definitions input 建立，不得從
17 筆 handoff matrix、檔名或 `CSharpTypeMapper` 反推。

primitive selection 至少應驗證：

- `resourceType` 為 `StructureDefinition`。
- `kind` 為官方 primitive definition 所使用的 `primitive-type`。
- `version` 與 requested FHIR version 使用 ordinal equality。
- `type`、`url` canonical 與 source file 均存在。
- FHIR type name 與 canonical 在 inventory 中唯一。
- inventory 使用 ordinal sort，結果不依 filesystem enumeration order。

Loader 應引入明確的 definition kind/profile，或先做共同反序列化再分流驗證；不得把現有
complex datatype loader 的 `kind` 檢查直接放寬為「任何值都接受」。

每個 inventory item 必須與 policy 以 FHIR type name 及 canonical 對應。inventory 多一筆、
policy 多一筆、canonical 不同或 FHIR version 不同，都必須在 render/write 前失敗。

### 5.3 正式 generated output

Phase B 正式輸出目錄固定為：

```text
Generated/R5/Primitives/
```

建議輸出：

```text
Generated/R5/Primitives/
├─ FhirBase64Binary.g.cs
├─ ...
├─ FhirUrl.g.cs
├─ PrimitiveRegistryComposition.g.cs
└─ primitive-generation-manifest.json
```

規則如下：

- 每個 supported wrapper 一個 `.g.cs`，檔名由 policy wrapper name 決定。
- registry composition 使用固定檔名，不依執行順序或機器環境。
- manifest 不是 C# source，不送入 Roslyn source batch。
- output 只包含本次完整 generation batch；不保留 stale files。
- source 與 manifest 使用 UTF-8 without BOM、LF newline，檔案尾端保留單一 newline。
- 不寫入時間戳、絕對路徑、使用者名稱、隨機值或不穩定 assembly location。
- generated header 記錄版本 provenance，但不得包含每次執行會變動的資料。
- `GeneratedFileWriter` 的 protected path tests 必須涵蓋 `Primitives` 與其他手寫 source
  roots，正式命令只允許寫入核准的 generated root 或明確的測試暫存目錄。

是否將正式 generated output 提交 source control，應在 B0 review 時明確核准。此 repository
的 Phase B 預設採提交 generated source 與 manifest，使 golden review、package source 與
可重現性可稽核；CI 重新生成後必須驗證 working tree 無差異。

### 5.4 Registry composition seam

Phase B 在目前單一 SDK assembly 內採 partial composition seam：

1. 手寫 `PrimitiveRegistry` 保留 immutable registry、duplicate detection、required lookup
   與 `Define<TPrimitive,TValue>` helper。
2. generated `PrimitiveRegistryComposition.g.cs` 在同一 namespace 與同一 partial type 中
   提供 deterministic definitions composition。
3. `PrimitiveRegistry.Default` 仍是唯一 immutable default composition root。
4. generated composition 可以使用 internal Runtime codec/validator symbols，因為它與
   Runtime 編譯於同一 assembly。
5. 不新增 production `InternalsVisibleTo`，也不將 codec、validator、definition 或 registry
   改成 public。

實作可以使用 generated private method 或 partial method，但最後必須只有一個 default
entry source。不得讓手寫與 generated definitions 合併後同時生效，也不得使用 runtime
reflection 掃描 wrappers 取代 deterministic composition。

### 5.5 先隔離驗證，再原子切換

B1-B5 不得直接把同名 generated wrappers 加入主 SDK compilation。切換前的驗證方式為：

- wrapper source：golden tests 與 Roslyn external contract compilation。
- registry composition：golden/structural tests、key resolution tests，以及使用同 assembly
  compilation context 的 integration harness。
- 完整 batch：determinism、manifest 與 writer tests。

B6 才在同一個可回復的 change set 中：

1. 產生並加入正式 generated output。
2. 排除或移除 17 個手寫 wrapper declarations。
3. 移除手寫 default registry entries，接上 generated composition。
4. build 主 SDK，執行完整 regression suite。

不得先提交會使主分支產生 duplicate types 的 generated wrappers，也不得先刪除手寫
wrappers 再等待下一個 PR 補上 generated versions。

### 5.6 Deterministic 與 fail-fast

所有集合、diagnostics、source 與 manifest entries 使用 `StringComparer.Ordinal` 排序。
下列情況必須在寫檔前失敗，且既有 output 保持不變：

- input/version/policy schema 無效。
- inventory 或 policy 重複、缺漏或多餘。
- supported entry 缺少 wrapper/CLR/codec/validator/literal shape。
- unsupported entry 沒有非空白理由。
- unknown codec/validator key。
- literal codec 與 JSON token、CLR type 或 wrapper shape 不相容。
- 產生重複 type/file name。
- Roslyn compilation failure。
- manifest 無法建立。

diagnostics 應使用穩定 code、severity、source、FHIR canonical/version，並依 code、source、
FHIR type name 與 message 做 deterministic sort。Phase B 應新增專用 diagnostic codes，
不得把所有 policy/inventory 錯誤都包成 `CompilationFailure`。

## 6. 目標資料流與結構

```text
Official R5 StructureDefinitions
              │
              ▼
    Primitive inventory loader ─────┐
                                     │ join + validate
Versioned primitive policy ─────────┘
              │
              ▼
   ValidatedPrimitiveGenerationModel
       │              │             │
       ▼              ▼             ▼
Wrapper renderer  Registry renderer  Manifest renderer
       │              │             │
       └──────────────┴─────────────┘
                      │
             compilation/contract tests
                      │
                      ▼
          transactional generated output
```

建議新增或擴充：

```text
CodeGen/
├─ Inventory/
│  └─ PrimitiveDefinitionInventoryBuilder.cs
├─ Policy/
│  ├─ PrimitiveGenerationPolicy*.cs
│  └─ primitive-generation-policy.json
├─ Models/
│  └─ PrimitiveGenerationModel.cs
├─ Rendering/
│  ├─ PrimitiveWrapperRenderer.cs
│  ├─ PrimitiveRegistryCompositionRenderer.cs
│  └─ PrimitiveManifestRenderer.cs
└─ Generation/
   └─ PrimitiveGenerationPipeline.cs

Tests/CodeGen/
├─ Inventory/
├─ Policy/
├─ Rendering/
├─ GoldenFiles/R5/Primitives/
├─ Compilation/
└─ Generation/
```

名稱可以配合現有命名慣例調整，但 inventory、policy validation、rendering、compilation
與 writing 的責任必須可獨立測試。不得把所有 Phase B 邏輯塞入 `Program.cs` 或現有
`CSharpClassRenderer` 的條件分支。

## 7. Work Package B0：固定 Phase B baseline 與決策閘門

### 7.1 目標

在改變 production behavior 前固定 17 個手寫 wrappers、default registry、public API、
JSON round-trip 與 CodeGen MVP 的基準，並核准 Phase B 的輸入、輸出與切換策略。

### 7.2 方法

1. 記錄目前 Release build/test 結果與 SDK public API snapshot。
2. 由 reflection 或集中測試產生 17 筆現況 inventory：FHIR type、wrapper type、value type、
   constructors、public fields/properties/methods、codec、validator 與排序。
3. 將現況逐筆與 handoff matrix 比較；差異必須明確列出，不得只做 source text 比較。
4. 建立 `decimal`、`integer64` literal constructor、`Literal` 與 `ToString()` observable
   behavior 的 characterization tests。
5. 確認 `FhirString`、`FhirMarkdown`、`FhirDecimal` 等 public constants 的相容處置。
6. 核准 JSON policy path、正式 generated root、source-control strategy、partial registry seam
   與 B6 原子切換方式。
7. 為 Phase B 新增測試 fixture 目錄；fixture 應是固定、最小且不依網路下載。

### 7.3 完成標準

- 17 筆 Runtime matrix 與 handoff 差異為零，或每個差異都有核准 disposition。
- public API compatibility exceptions 已記錄。
- baseline tests 在 Windows/Linux 可重現。
- B1-B7 不再需要臨時決定輸入、輸出或切換策略。
- production behavior 尚未改變。

### 7.4 驗收方式

- `PublicApiSnapshotTests` 通過且 approved file 未被無理由更新。
- `PrimitiveRuntimeContractTests`、Parser/Serializer primitive fixtures 通過。
- `PhaseBPrimitiveHandoffTests` 通過。
- CodeGen MVP golden、Roslyn、CLI tests 通過。
- Release solution build/test 為 baseline green。

### 7.5 B0 核准決策與實作結果

B0 以 commit `59d1793` 作為 production baseline，採用下列決策：

| Decision | B0 disposition |
|---|---|
| Policy format/path | JSON；`CodeGen/Policy/primitive-generation-policy.json` |
| Formal generated root | `Generated/R5/Primitives/` |
| Generated artifact ownership | source 與 manifest 提交 Git；B5/B6 啟用正式輸出時調整 `.gitignore` |
| Registry composition | 現有單一 SDK assembly 內的 partial `PrimitiveRegistry` seam |
| Wrapper/registry migration | B6 以單一可回復 change set 原子切換，不允許 duplicate 或 missing type 的中間狀態 |
| Official input | `hl7.fhir.r5.core#5.0.0`；21 個 `kind = primitive-type` fixtures 固定於 repository |
| Public API compatibility | 保留既有 constants 與 presentation-only `ToString()` observable behavior |

Public compatibility policy 使用受限資料，不接受任意 C# snippet：

- `toStringBehavior` 只允許 `inherited`、`boolean-lowercase`、`invariant-value`、
  `literal-or-invariant-value`。
- `publicConstants` 只允許結構化的 name、CLR integral type 與 constant value，並驗證 C#
  identifier、唯一性與 numeric range。
- compatibility behavior 不得執行 JSON codec、format validation、registry lookup 或建立
  validation issue。

現有 public compatibility members 已同步固定於 Phase B handoff 3.3。完整官方 inventory
比 Runtime handoff matrix 多出 `oid`、`time`、`uuid`、`xhtml`；B1 policy 必須將它們標示
為 unsupported 並記錄「Phase A Runtime 尚無核准的 CLR/codec/validator contract」，除非先
另行擴充並核准 Runtime contract，不得由 CodeGen 猜測 mapping。

B0 新增的 automated gates：

- `PrimitiveWrapperBaselineTests`：固定 17 個 public sealed wrapper、base/value type、
  constructors、`Literal`、public constants、declared `ToString()` 與 invariant/literal 行為。
- `PrimitiveStructureDefinitionFixtureTests`：固定 21 個官方 R5 primitive identities、
  canonical、version、file name 與 SHA-256 bytes。
- `ApprovedPublicApi.txt` 未變更；production source 未變更。

Windows local baseline（.NET 9、Release）：

- B0 修改前：build 0 warnings、0 errors；380 passed、0 failed、1 skipped。
- B0 targeted tests：Architecture 92 passed；CodeGen 139 passed。
- B0 修改後：build 0 warnings、0 errors；389 passed、0 failed、1 skipped。
- PR #9 的 Windows/Linux CI 均通過；B0 cross-platform gate 完成。

## 8. Work Package B1：建立 versioned primitive policy

### 8.1 目標

建立單一、可反序列化、可版本化且可完整驗證的 primitive generation policy。

### 8.2 Policy schema

最小 top-level fields：

```json
{
  "schemaVersion": 1,
  "policyVersion": "1.0.0",
  "fhirVersion": "5.0.0",
  "runtimeContractVersion": "phase-a-v1",
  "primitiveNamespace": "MyFhirSdk.Primitives",
  "primitives": []
}
```

每筆 entry 至少包含 handoff 要求的：

- `fhirTypeName`
- `canonical`
- `fhirVersion`
- `wrapperName`
- `clrValueType`
- `jsonToken`
- `codecKey`
- `validatorKey`
- `preserveLiteral`
- `literalConstructor`
- `literalPropertyName`
- `supportStatus`
- `unsupportedReason`（只在 unsupported 時要求）

若 B0 決定保留額外 public constants 或 presentation members，schema 必須以受限、可驗證
的 compatibility shape 表達，不能接受任意 C# snippet。policy 不得包含 regex、delegate、
codec implementation 或 validation algorithm。

### 8.3 Validation rules

除 handoff 4.1 外，至少加入：

- schema version 必須為 CodeGen 支援值。
- semantic version fields 必須可解析且不得空白。
- namespace、wrapper identifier 與 CLR type token 必須來自允許的 C# shape。
- type name、canonical、wrapper name 及 output file name ordinal unique。
- supported entry 的所有 generation fields 必須完整。
- unsupported entry 必須有理由且不得產生 wrapper/registry entry。
- policy entries 依 FHIR type name ordinal 正規化；原始檔順序不影響 output。
- 17 個 supported entries 必須與 handoff matrix 完全一致。

### 8.4 完成標準

- policy loader 不依 static global state。
- invalid schema、duplicate、missing field、unknown key、literal mismatch 與 unsupported reason
  均有精確 diagnostics。
- policy validator 回傳 immutable validated model；renderer 不接受未驗證 DTO。
- policy 已包含 handoff 的 17 個 supported decisions。
- 尚未移除 `CSharpTypeMapper.PrimitiveTypeNames`，避免在本 PR 同時改變 production mapping。

### 8.5 驗收方式

- policy serialization/deserialization tests。
- handoff 17 筆 matrix data-driven tests。
- 每條 validation rule 至少一個 invalid case。
- ordinal/case-sensitive duplicate tests。
- shuffled policy input 產生相同 validated order。

### 8.6 B1 實作結果

B1 建立 `CodeGen/Policy/primitive-generation-policy.json` 作為唯一 versioned primitive
policy，並新增：

- strict JSON DTO：未知 property、malformed JSON、missing file 與 cancellation 都有明確
  loader contract。
- `PrimitiveGenerationPolicyLoader`：回傳 `GenerationResult`，不使用 mutable static state。
- `PrimitiveGenerationPolicyValidator`：在 renderer 前驗證 schema、semantic version、
  required fields、C# namespace/identifier、ordinal uniqueness、cross-platform output filename、
  supported/unsupported shape、封閉 key、literal contract、codec/token/CLR、validator/backing、
  `ToString()` behavior 與 public constants。
- immutable `ValidatedPrimitiveGenerationPolicy`：entries 與 constants defensive copy，並將
  JSON string keys 轉成封閉 enums；後續 renderer 不接收未驗證 DTO。
- 專用 diagnostics `FSG0013`–`FSG0018`，錯誤排序 deterministic。

正式 policy 共 21 筆，依 FHIR type name ordinal 排序：

- 17 筆 supported entries 與 Phase B handoff matrix 完全一致。
- `oid`、`time`、`uuid`、`xhtml` 明確標示 unsupported 並記錄缺少核准 Runtime contract。
- `FhirString`、`FhirMarkdown`、`FhirDecimal` constants 與六個 wrappers 的 B0
  `ToString()` compatibility behavior 使用受限結構化 policy 表達。

B1 保留 `CSharpTypeMapper.PrimitiveTypeNames`，未修改 Runtime、Parser、Serializer、Validator
或 wrappers；mapper 切換仍屬 B7。

Windows local Release 驗收：build 0 warnings、0 errors；426 passed、0 failed、1 skipped；
其中 CodeGen tests 176 passed。Push 後由 branch CI 補齊 Windows/Linux cross-platform gate。

## 9. Work Package B2：載入官方 R5 primitive inventory

### 9.1 目標

從固定的官方 R5 5.0.0 definitions 建立完整、deterministic inventory，並與 policy 做
一對一 coverage validation。

### 9.2 方法

1. 將 StructureDefinition 的共同 JSON loading 與 kind-specific validation 分離。
2. 新增 primitive inventory builder，只接受符合 primitive selection contract 的 definitions。
3. 保存 source file、FHIR type、canonical、version、base definition 及文件 provenance。
4. 以 type name 與 canonical 做 ordinal duplicate detection。
5. 將 inventory 與 policy join；兩側 unmatched entries 都產生 error。
6. 對 handoff matrix 未列出的官方 primitive，要求 policy 明確標示 unsupported 或在 Runtime
   policy review 後新增 supported contract；不得猜測 CLR type、codec 或 validator。
7. 測試使用 repository fixture；正式 pipeline 不在 generation 過程隱式連網。

### 9.3 完成標準

- 完整官方 inventory 每筆都有 policy decision。
- 錯誤 version、kind、canonical、duplicate 與 unmatched policy 會在 render 前失敗。
- 既有 complex datatype loader tests 保持通過。
- inventory order 不依檔案系統或 JSON 輸入順序。

### 9.4 驗收方式

- valid primitive fixture batch test。
- wrong kind/version/resourceType tests。
- duplicate type/canonical tests。
- missing/extra policy coverage tests。
- shuffled file creation/enumeration order determinism test。
- 既有五種 MVP datatype loader/generation regression tests。

## 10. Work Package B3：生成 primitive wrapper declarations

### 10.1 目標

從 validated inventory + policy model 生成 17 個與 Runtime public contract 相容的 wrappers，
不在 wrapper 內複製 Runtime codec 或 validation algorithm。

### 10.2 一般 wrapper contract

每個一般 wrapper 至少生成：

```csharp
public sealed class FhirString : PrimitiveType<string>
{
    public FhirString()
    {
    }

    public FhirString(string? value)
        : base(value)
    {
    }
}
```

namespace 來自 validated policy，目前必須為 `MyFhirSdk.Primitives`。文件註解的來源與
正規化規則必須固定；不得把不穩定的 source path 寫入 generated documentation。

### 10.3 Literal-preserving wrapper contract

`decimal` 與 `integer64` 必須生成 handoff 3.2 固定的 string constructor 與 public
`Literal` property，並保持：

- JSON parse 後 CLR `Value` 與 `Literal` 的既有 observable contract。
- `decimal` trailing zero、exponent raw text 不被正規化。
- `integer64` JSON string literal 不轉成 JSON number。
- 由 CLR value constructor 建立時仍可由 Runtime codec 以 invariant representation 輸出。

literal constructor 所需的 parse/assignment shape 應由受限 renderer template 產生，不得
從 policy 注入任意程式碼。format validity 仍由 internal validator 決定。

### 10.4 完成標準

- 17 個 wrapper filenames 與 type names deterministic。
- generated wrappers 不引用 `IPrimitiveCodec`、`IPrimitiveValidator`、`PrimitiveRegistry`、
  Parser、Serializer 或 Validator。
- 沒有 public `IsValid()`。
- B0 核准保留的 public API 與 observable behavior 均存在。
- generated wrappers 尚未與手寫同名 declarations 一起加入主 SDK compilation。

### 10.5 驗收方式

- 每個 wrapper 的 golden test。
- Roslyn external contract compilation，只引用 approved public Core contract。
- reflection-based shape tests：sealed、base type、constructors、Literal 及禁止成員。
- XML documentation escaping tests。
- shuffled inventory/policy determinism tests。
- decimal/integer64 constructor 與 literal round-trip tests。

## 11. Work Package B4：生成 registry composition

### 11.1 目標

產生與 handoff matrix 完全一致、ordinal sorted 的 internal primitive definitions，取代
`PrimitiveRegistry.CreateDefinitions()` 中的手寫 entry list。

### 11.2 方法

1. 建立唯一的 codec/validator key resolver，輸出 internal Runtime symbols。
2. 對每個 supported policy entry 生成 wrapper/value generic types、FHIR type name、codec
   與 validator composition。
3. 讓手寫 `PrimitiveRegistry` 與 generated composition 透過同 assembly partial seam 連接。
4. unsupported entries 只進 manifest，不進 registry。
5. 保留 `PrimitiveRegistry.Create` 的 duplicate detection 與 ordinal materialization，不能
   因 generated input 已驗證而移除 runtime defensive checks。
6. `PrimitiveRegistry.Default` 仍不可由 SDK 使用者替換或修改。

### 11.3 完成標準

- generated composition 恰好 17 筆，順序與 handoff matrix 一致。
- unknown/missing key 在 generation 階段失敗。
- registry 沒有 reflection assembly scan、wrapper name switch 或 public mutation seam。
- Parser、Serializer、Validator 不新增 concrete wrapper references。
- composition 可以在主 SDK 的 same-assembly context 編譯。

### 11.4 驗收方式

- registry composition golden test。
- exhaustive codec/validator key resolver tests。
- `PrimitiveRuntimeContractTests` 的 type/value/token/round-trip/validator matrix。
- duplicate FHIR name、duplicate wrapper type、missing registration tests。
- architecture tests 確認 internal accessibility 與無 concrete wrapper engine branch。

## 12. Work Package B5：manifest、pipeline 與 deterministic output

### 12.1 目標

將 inventory、policy、wrapper、registry、manifest、compilation validation 與 transactional
writer 串成獨立的 primitive generation pipeline。

### 12.2 Manifest contract

manifest 至少包含：

- manifest schema version。
- FHIR specification/package identity 與 version。
- policy version。
- CodeGen version。
- Runtime contract version。
- primitive namespace。
- supported/unsupported inventory decisions及 unsupported reason。
- generated source file list；如包含 hashes，hash 只涵蓋其他 artifacts，避免 self-reference。

manifest properties 與 arrays 必須固定排序，不含 timestamp、absolute path 或環境資料。

### 12.3 Pipeline 順序

```text
load definitions
→ build primitive inventory
→ load/validate policy
→ join inventory and policy
→ build immutable generation model
→ render wrapper/registry/manifest artifacts
→ validate source batch
→ transactional write complete batch
```

任一步失敗都不得寫入 partial output。CLI/generator mode 應明確區分 primitive batch 與現有
datatype preview，不能用 type name 是否剛好為 primitive 來隱式切換模式。

### 12.4 完成標準

- 連續執行兩次 output byte-for-byte 相同。
- 交換 input file 與 policy entry 順序不改變 output。
- compilation failure 或 cancellation 保留前一版完整 output。
- stale files 會在成功 transaction 中移除。
- manifest 與 source versions 一致。
- 正式 output 不覆寫 `Primitives/Runtime`、`CodeGen` 或其他手寫 source roots。

### 12.5 驗收方式

- end-to-end primitive pipeline fixture test。
- wrapper Roslyn compilation 與 same-assembly composition compilation test。
- two-run byte comparison。
- shuffled input comparison。
- writer rollback/cancellation/stale-file tests。
- manifest schema、ordering、unsupported reason 與 version tests。
- CLI diagnostics/exit code tests。

## 13. Work Package B6：整合主 SDK 並切換 generated source

### 13.1 目標

以單一可回復變更將正式 generated wrappers 與 registry composition 接入主 SDK，移除對應
手寫 declarations/entries，且 public/runtime behavior 保持相容。

### 13.2 切換前 gate

全部符合才能切換：

- B0-B5 tests 全綠。
- 17 個 wrapper golden/Roslyn tests 通過。
- generated registry matrix 與 handoff 完全一致。
- public API diff 已預先產生並核准。
- 正式 generated output 已在暫存位置連續生成兩次且 byte-identical。
- rollback change set 已明確：可恢復手寫 wrappers 與手寫 registry entries。

### 13.3 原子切換步驟

1. 以核准的 official definitions 與 policy 生成正式 output。
2. 確認主 SDK project 會編譯 `.g.cs`，但不把 manifest 當 source。
3. 在同一 change set 移除或明確排除 17 個手寫 wrapper `.cs`。
4. 將 `PrimitiveRegistry` 接到 generated partial composition，移除手寫 entry list。
5. build 主 SDK，先執行 architecture/public API/primitive tests，再執行全部 solution tests。
6. 再執行一次 generation，確認 repository 無 generated diff。

### 13.4 完成標準

- 主 assembly 中每個 wrapper 只有一個 declaration。
- `PrimitiveRegistry.Default` 每個 supported primitive 恰好一筆。
- public API snapshot 相容，或只包含 B0 核准的差異。
- Parser/Serializer/Validator behavior 與 Phase A baseline 相容。
- generated output 可由固定 input + policy 完整重現。
- 沒有新增 production `InternalsVisibleTo` 或公開 internal primitive contract。

### 13.5 驗收方式

- Release restore/build/test 全部通過。
- `PublicApiSnapshotTests`、`RuntimeContractCompilationTests`、
  `RuntimeContractAccessibilityTests`、`RuntimeModelDependencyTests` 通過。
- `PrimitiveRuntimeContractTests`、`PhaseBPrimitiveHandoffTests` 通過。
- Parser/Serializer/Validation primitive fixtures 通過。
- CodeGen golden/Roslyn/determinism tests 通過。
- generation 後 `git diff --exit-code` 不出現未提交 generated changes。

## 14. Work Package B7：移除過渡 mapping、清理與 Phase C handoff

### 14.1 目標

移除 Phase B 已取代的 primitive decision sources，更新文件與 debt ownership，並確保 Phase C
可以直接消費同一 validated policy。

### 14.2 方法

1. 將 `CSharpTypeMapper` 改為注入或消費 validated primitive mapping view。
2. 移除 `CSharpTypeMapper.PrimitiveTypeNames`，不能留下 fallback dictionary。
3. 搜尋並移除其他重複 primitive mapping、手寫 registry entries 與 temporary adapters。
4. 保留 complex whitelist，並清楚標示 Phase C owner。
5. 更新 architecture boundaries、Phase B handoff status、README/operation instructions 與
   generated provenance。
6. 記錄未完成但有 owner 的 debt；不得因 Phase B cleanup 提前拆 assembly。
7. 為 Phase C 說明如何讀取 policy 的 supported primitive mapping，而不是複製資料。

### 14.3 完成標準

- repository 只有一個 primitive policy decision source。
- mapper、wrapper renderer、registry renderer 與 manifest 使用同一 validated model。
- 無手寫 17 筆 registry/wrapper fallback。
- 無 ownerless Phase B transitional adapter。
- handoff Definition of Done 與 review checklist 全部完成並留有驗證紀錄。

### 14.4 驗收方式

- architecture test 禁止 `PrimitiveTypeNames` 或等價 static primitive dictionary 回歸。
- static search 確認 Runtime engine 沒有 concrete wrapper name branch。
- 完整 Release build/test 與 deterministic regeneration 通過。
- 文件連結、版本與 generated manifest 一致。

### 14.5 B7 實作結果

B7 將 `CSharpTypeMapper` 的 primitive mapping 改為必要注入的
`PrimitiveTypeMappingView`。該 view 只可由
`ValidatedPrimitiveGenerationPolicy` 建立，僅包含 supported entries，並保留 policy 的
primitive namespace 與 wrapper name。舊的 static `PrimitiveTypeNames` dictionary 已移除，
`integer64` 因此不再遺漏，`oid`、`time`、`uuid`、`xhtml` 仍依 policy 明確不映射。

Datatype preview pipeline 會在 parse 前載入並驗證同一份 primitive policy；CLI 可使用
`--policy` 指定版本，未指定時使用隨 CodeGen 發布的 repository policy。Policy read、schema、
identity 或 FHIR version 錯誤均在 render/write 前失敗。

保留的 `DefaultComplexTypeNames` 不是 primitive decision source，而是 MVP complex datatype
scope gate，其移除 owner 為 Phase C。完整 consumer contract、bootstrap debt owner 與 entry
gate 記錄於 `MyFhirSdk_R5_Models_Generation_Phase_C_Handoff.md`。Architecture test 禁止
`PrimitiveTypeNames` 或等價 static string dictionary 回歸。

## 15. 建議實作順序與 PR 拆分

PR、branch 與 commit 說明使用本文件 Work Package 編號，避免建立另一套編號：

| Work Package | 建議 PR 範圍 | 必要成果 |
|---|---|---|
| B0 | Baseline、API disposition、fixture 與決策閘門 | 無 production behavior change |
| B1 | Policy DTO、loader、validator 與 policy file | 單一 validated policy |
| B2 | Primitive inventory 與 policy coverage | 官方 inventory 一一決策 |
| B3 | Wrapper model/renderer/golden/Roslyn | 17 個 wrappers 可重現 |
| B4 | Registry renderer 與 composition seam | 17 筆 deterministic entries |
| B5 | Manifest、pipeline、writer、CLI/determinism | 完整 batch 可安全輸出 |
| B6 | Generated source integration 與原子切換 | generated 取代手寫版本 |
| B7 | 移除 mapper fallback、清理與文件 | Phase B DoD 完成 |

每個 PR 必須：

- 可獨立 build/test；B6 前不得讓主 SDK 出現 duplicate type。
- 說明 public API 是否變更。
- 列出新增/移除的 temporary adapter 與 owner。
- 不混入 complex datatype/Resource generation。
- 提供自動化測試與 failure/rollback 說明。
- 保持 diagnostics 與 output deterministic。

B3 與 B4 可以在同一 feature branch 平行準備，但 registry renderer 必須建立在 B1 validated
policy contract 上；B6 不得拆成「先刪手寫、後加 generated」兩個可觀察到紅燈的 PR。

## 16. Phase B 完成標準

全部符合才算 Phase B 完成：

- 官方 R5 primitive inventory 與 versioned policy 一一對應。
- unsupported entries 有具體、可稽核理由。
- handoff 的 17 個 wrappers 由 CodeGen 產生並編譯於主 SDK。
- wrapper public API 與 B0 核准 baseline 相容。
- `decimal`、`integer64` literal representation 可 round-trip。
- generated registry entries 與 handoff matrix 完全一致且 ordinal sorted。
- `PrimitiveRegistry.Default` 保持 internal、immutable 且不可由 SDK 使用者替換。
- `CSharpTypeMapper` 已消費 validated policy，舊 primitive dictionary 已移除。
- manifest 記錄 FHIR、policy、CodeGen 與 Runtime contract versions。
- output 連續生成兩次 byte-for-byte 相同。
- generated source、manifest 與 golden files 沒有不穩定環境資料。
- Phase A contract、Parser、Serializer、Validation、CodeGen 與 architecture tests 全部通過。
- 沒有公開 codec、validator、definition、registry 或 primitive `IsValid()`。
- 沒有同時保留同名手寫與 generated wrappers。
- Phase C owners 與剩餘 bootstrap debt 已更新。

## 17. 最終驗收流程

### 17.1 自動化驗收

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln --configuration Release --no-restore
dotnet test MyFhirSdk.sln --configuration Release --no-build --no-restore
```

另執行：

- Phase B primitive generation end-to-end command。
- 同一 input/policy 連續生成兩次並做 byte-level directory comparison。
- public API approval tests。
- architecture/dependency/accessibility tests。
- primitive definition/codec/validator matrix tests。
- Parser/Serializer primitive JSON fixture tests。
- generated wrapper golden 與 Roslyn compilation tests。
- manifest schema/version/provenance tests。
- CLI exit code 與 diagnostics ordering tests。

若 CI 以 committed generated output 為基準，應在乾淨 checkout 重新生成並確認：

```powershell
git diff --exit-code -- Generated/R5/Primitives
```

### 17.2 靜態快速檢查

以下只作為 review 輔助，不取代 automated architecture tests：

```powershell
rg 'PrimitiveTypeNames' CodeGen
rg 'FhirDecimal|FhirInteger64' Serialization Validation
rg 'IPrimitiveCodec|IPrimitiveValidator|PrimitiveRegistry|IsValid\(' Generated/R5/Primitives
rg 'DateTime.Now|UtcNow|Guid.NewGuid|GetFullPath' Generated/R5/Primitives
```

完成狀態預期：

- 第一項無舊 static primitive dictionary 命中。
- 第二項不在 Parser/Serializer/Validator production engine 出現 concrete wrapper branch。
- 第三項只允許 registry composition 使用 internal contracts；wrapper files 不得命中。
- 第四項 generated artifacts 不得命中不穩定 provenance。

### 17.3 人工驗收情境

至少人工確認：

1. 從固定 R5 definitions 與 policy 生成完整 primitive batch。
2. 任意移除一筆 policy，generation 在寫檔前以明確 diagnostic 失敗。
3. 將 unknown codec key 放入測試 policy，generation 明確失敗且舊 output 不變。
4. 建立含 metadata-only primitive 的 Resource，可 serialize-parse-serialize。
5. primitive array raw/metadata alignment 保持正確。
6. `decimal` trailing zero/exponent 與 `integer64` JSON string representation 保持原樣。
7. 使用者只能由 `FhirValidator.Validate(Resource)` 取得 primitive issue。
8. SDK 使用者不能存取或替換 primitive codec、validator 或 default registry。

## 18. 風險與對策

| 風險 | 對策 |
|---|---|
| 官方 inventory 與 17 筆 matrix 混為一談 | inventory 從 definitions 建立；policy 對每筆 supported/unsupported |
| mapper、renderer、registry 再形成多套 mapping | 所有 consumer 只接受同一 validated policy model |
| primitive loader 放寬後破壞 datatype validation | 共同反序列化、kind-specific validation profiles |
| generated 與手寫 wrappers 同時編譯 | B1-B5 隔離驗證；B6 單一 change set 原子切換 |
| public constants/`ToString()` 無聲消失 | B0 reflection/API disposition gate，snapshot 差異需核准 |
| literal constructor 正規化 decimal/integer64 | raw literal fixtures 與 serialize-parse-serialize byte/semantic tests |
| registry composition 迫使 internals public | same-assembly partial seam，不新增 production friend assembly |
| generated registry 移除 runtime defensive checks | 保留 duplicate/missing detection 與 immutable default root |
| output 含時間戳或機器路徑 | manifest/header 禁止環境資料，two-run byte comparison |
| writer 失敗留下 partial batch | render/compile 全部成功後 transactional commit 與 rollback tests |
| unsupported primitive 被靜默忽略 | coverage validation 要求每筆有明確理由 |
| Phase B 順帶擴張成完整 Models generation | PR scope gate，complex whitelist 與 base-shape debt 留給 Phase C |

## 19. ADR 與決策紀錄邊界

本指引記錄「如何實作與驗收」。只有長期影響 public API、assembly dependency 或 artifact
ownership 的選擇才另建 ADR，例如：

- 未來 Runtime/Models 拆 assembly 後的 registry composition seam。
- generated artifacts 是否長期提交 source control。
- public primitive compatibility members 的移除或 ownership 改變。

Phase B 不需要為一般類別命名、測試檔案位置或 renderer 私有實作建立 ADR。若 Phase C/D
決定拆 assembly，必須另以 ADR 解決跨 assembly composition，且不得形成
Runtime → R5 Models 的反向依賴。

## 20. 文件與後續階段

建議維持：

```text
MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md
└─ 架構原則、責任與依賴方向

MyFhirSdk_Runtime_Phase_A_Implementation_Guide.md
└─ Runtime contract refactor 的實作與驗收

MyFhirSdk_Primitive_Generation_Phase_B_Handoff.md
└─ primitive matrix、policy contract、bootstrap debt 與 Phase B DoD

MyFhirSdk_Primitive_Generation_Phase_B_Implementation_Guide.md
└─ 本文件：B0-B7 實作順序、測試、切換與回復

MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md（後續）
└─ full datatype/Resource/dependency graph generation

MyFhirSdk_CodeGen_Local_Tool_Release_Guide.md（後續）
└─ tool packaging、manifest versioning 與 publish/upgrade smoke tests
```

Phase B 完成後，Phase C 必須重用 validated primitive policy 與 generated wrapper identity，
不得在完整 model generation 中重新建立 primitive mapping。
