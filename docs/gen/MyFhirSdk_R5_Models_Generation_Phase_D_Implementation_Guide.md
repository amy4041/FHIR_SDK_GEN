# MyFhirSdk CodeGen Phase D 實作指引

Version 0.1

- 文件狀態：Accepted for implementation；D0 baseline 與 dependency/package 決策已於
  `docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_D0_Decisions.md` 固定
- 適用範圍：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、MyFhirSdk、.NET 9
- Phase C 基準：Completed
- 上位架構文件：`docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`
- Phase D 交接契約：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_Handoff.md`
- 前階段指引：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md`

## 1. 文件目的

本文件將 Phase D 的 .NET local tool 包裝與 dependency seam 拆成可實作、可測試、可分批
合併且可回復的 Work Packages。Phase D 的工作不是增加 FHIR model coverage，而是讓
Phase C 完成的 deterministic generator：

1. 不再以完整 `MyFhirSdk.csproj` 作為 CodeGen 編譯期依賴；
2. 以版本化 Runtime contract 及明確 Roslyn reference 驗證 generated source；
3. 不依賴 repository clone、目前工作目錄或 repository build output；
4. 可透過 .NET local tool manifest 安裝、還原、執行及升級；
5. 對 CodeGen、Runtime、policy、FHIR package 與 target framework 的不相容組合明確失敗。

若本文件與 Phase D handoff 或上位 boundaries 衝突，以 handoff 與 boundaries 為準，先修正
文件或建立 ADR，不得由實作者在 renderer、CLI 或 package target 內形成隱藏決策。

## 2. Phase D 目標

Phase D 完成後應具備：

1. `myfhir-codegen` 可由 repository local tool manifest 固定版本並執行。
2. 已安裝的 tool 不需 clone repository，也不需先 build `MyFhirSdk.csproj`。
3. CodeGen production assembly 不參考 `MyFhirSdk` implementation assembly。
4. Runtime foundation declaration 維持手寫，CodeGen 只消費版本化 contract descriptor。
5. Roslyn compilation 使用明確 reference set，不使用 `typeof(DataType).Assembly.Location`。
6. tool package 內含預設 policies、Runtime contract descriptor 與必要 compilation assets。
7. CLI input、asset resolution、output safety 與 diagnostics 在 Windows/Linux 一致。
8. manifest 記錄 Runtime contract、reference identity 及 tool package version。
9. pack、install、restore、generation、upgrade 與 clean-environment smoke tests 自動化。
10. Phase C 的 831 個 generated source artifacts、public API、Runtime behavior 與
    deterministic output 不變；manifest 只允許 D5 核准的 schema/provenance 擴充。

## 3. 非目標

Phase D 不包含：

- 重新設計或重新生成 Phase C 已完成的 datatype、Resource、Backbone 與 metadata shape。
- 將 `FhirObject`、`Base`、`Element`、`DataType`、`Resource` 等 Runtime foundation 改為
  generated declarations。
- 為了 local tool 包裝而強制拆成公開的 Runtime、Models、SDK 多套件。
- 改變既有 public CLR type 的 assembly identity；若需要，必須另立 ADR 與 migration phase。
- 生成 constraint Profile；`SimpleQuantity` 仍依 Phase D handoff 保持手寫。
- 新增 FHIRPath、terminology、fixed/pattern value 等 Phase C deferred capabilities。
- 發布到公開 NuGet feed、簽章、SBOM 或正式 release promotion；可建立 pack-ready artifact，
  正式發佈流程另由 release phase 決定。
- 以 assembly scan、現存 generated output 或 handwritten concrete model 反推 inventory。

## 4. 目前基準與已知差距

### 4.1 Phase C 交付基準

| 項目 | 基準 |
|---|---|
| FHIR package | `hl7.fhir.r5.core#5.0.0` |
| Package SHA-256 | `74b27cd1bfce9e80eaceac431edf230b0945a443564fbf5512f82e5fa50a80d4` |
| Primitive policy | `1.1.0` |
| CodeGen contract | `1.0.0` |
| Runtime contract | `phase-a-v1+c4-primitives-v1` |
| Target framework | `net9.0` |
| Generated model artifacts | 831 |
| CLI modes | explicit `primitive`、`model` |

`Generated/R5/model-generation-manifest.json`、C0 public API snapshots、Release solution tests 與
`CommittedModelGenerationTests` 是 Phase D 不得改壞的 baseline。

### 4.2 現有耦合

目前 production CodeGen 對 SDK 的編譯期耦合集中於：

- `CodeGen/MyFhirSdk.CodeGen.csproj` 直接 `ProjectReference` 完整 `MyFhirSdk.csproj`；
- `RoslynCompilationValidator` 以 `typeof(DataType).Assembly.Location` 找 reference；
- `ModelMetadataIrBuilder` 以 `typeof(FhirObject).Assembly` 查找 external bootstrap CLR type，
  並以 reflection 判斷少數 abstract datatype property；
- `Program` 從目前目錄尋找 repository root；
- model policies 由 `AppContext.BaseDirectory/Policy` 隱含解析；
- `GeneratedFileWriter` 以 repository root 判斷 SDK protected source directories。

Phase D 應移除上述「環境與 assembly discovery」耦合，但保留 output transaction、path
validation、policy validation、full-batch Roslyn 與 Runtime regression。

## 5. 固定實作原則

### 5.1 Runtime foundation 保持手寫

Phase D 接受下列 declaration ownership：

| 分類 | 型別 | Phase D 決策 |
|---|---|---|
| 永久 Runtime contract | `FhirObject`、`IFhirExtensionValue`、`PrimitiveType<T>` | 手寫，不生成 |
| Runtime foundation | `Base`、`Element`、`BackboneElement`、`BackboneType`、`DataType`、`Resource` | 手寫，不生成 |
| 需保留相容性的 foundation | `DomainResource` | Phase D 手寫；未經 ADR 不搬移 |
| R5 versioned bootstrap | `Extension`、`Meta`、`Narrative` | Phase D 手寫；assembly boundary 完成後再評估 |
| Profile declaration | `SimpleQuantity` | 手寫，交由後續 Profile generation phase |

官方 StructureDefinition 可用來驗證這些型別的 shape、生成 metadata 或建立 compatibility
report，但不得因「全部生成」而產生同名 source。判斷原則是：SDK 執行契約手寫，規格模型
大量生成；declaration ownership 與 metadata ownership 可以不同。

### 5.2 Local tool 發布不要求實體 assembly 拆分

Phase D 必須建立清楚的邏輯依賴：

```text
MyFhirSdk.CodeGen.Tool
  ├─ consumes Runtime contract descriptor
  ├─ consumes explicit compiler reference assets
  └─ generates source

Generated Models
  └─ compile against compatible MyFhirSdk Runtime/SDK
```

不要求在本階段將主 SDK 立即拆成 `MyFhirSdk.Runtime` 與 `MyFhirSdk.R5.Models` 公開 assembly。
工具可攜性應透過 contract descriptor 與 compiler-only reference assets 達成，不應靠改變
既有 SDK type identity 達成。

### 5.3 Runtime contract descriptor 是 CodeGen 的唯一 Runtime shape 輸入

建立 versioned、machine-readable descriptor，至少記錄：

- schema version、contract version、target framework；
- Runtime assembly/reference identity；
- CodeGen 允許引用的 foundation/bootstrap CLR symbols；
- symbol role、abstract/sealed/generic arity 等 CodeGen 真正需要的 facts；
- external bootstrap 中需要特殊 metadata composition 的 declared slots；
- descriptor 與 reference asset 的 SHA-256；
- compatible CodeGen/policy/FHIR version constraints。

descriptor 不複製 complete FHIR inventory，也不列 generated concrete Types/Resources。
`r5-model-ownership-policy.json` 仍決定 external definition ownership；descriptor 只證明對應
Runtime contract 是否存在及相容。SDK architecture tests 必須用 reflection 驗證 descriptor
與實際手寫 Runtime declarations 一致，CodeGen production 不得自行 reflection discovery。

### 5.4 決策 contract 與 Roslyn reference 分離

兩者用途不同：

```text
Runtime contract descriptor
  → CodeGen mapping、bootstrap metadata、compatibility decision

Runtime compiler reference set
  → Roslyn 對 generated batch 做 compilation validation
```

Roslyn reference set 必須由 caller 或 package asset provider 明確提供，不能使用
`typeof(...)`、搜尋 `bin/obj`、載入目前 SDK implementation 或掃描已載入 assemblies。Phase D
可先封裝與 SDK baseline 相容的 reference assembly 作 compiler-only asset；不得將其中的
concrete model 當成 inventory 或 generation decision source。

### 5.5 Package assets 與 override precedence

tool package 應自行攜帶預設：

- primitive generation policy；
- model ownership、naming、backbone、choice/open type、validation policies；
- Runtime contract descriptor；
- Roslyn compiler reference assets；
- package/version metadata。

解析 precedence 必須固定並測試：

1. CLI 明確指定的 asset/path；
2. tool package 內建 asset；
3. 不得以目前目錄或向上搜尋 repository 作 production fallback。

開發測試可顯式注入 repository assets，但該 adapter 不得成為 packaged default。

### 5.6 Compatibility 必須 fail-fast

在讀取完整 definitions、建立 IR 或寫檔之前驗證：

- descriptor schema 與 contract version；
- tool/CodeGen version range；
- FHIR package id/version/FHIR version；
- primitive/model policy versions與hash；
- Runtime reference identity、target framework 及 descriptor hash；
- requested generation mode 與 asset completeness。

不相容必須產生排序穩定、可測試且含實際/預期 identity 的 diagnostic。不得警告後繼續、
自動降級、忽略未知欄位或改用環境中的另一份 assembly。

### 5.7 Deterministic、離線與最小權限

- 正式 CI 使用 repository 已鎖定的 offline FHIR fixture，不在 generation 中下載。
- package contents、manifest 與 generated text 採穩定排序、UTF-8 no BOM、LF。
- 不將 absolute repository、temp、user-profile 或 package-cache path 寫入 generated output。
- tool 不執行 generated source、不載入使用者 assembly 到 default load context。
- output 保持 staging、atomic swap、rollback 與 path traversal 防護。
- secret、NuGet credential、machine-specific cache path 不進入 logs 或 manifest。

## 6. 目標元件與資料流

建議新增或重構的邏輯元件：

```text
CLI arguments
   ↓
ToolAssetResolver
   ├─ policy set
   ├─ RuntimeContractDescriptor
   └─ RuntimeReferenceSet
   ↓
CompatibilityValidator
   ↓
PrimitiveGenerationPipeline / ModelGenerationPipeline
   ├─ inventory / graph / IR
   ├─ RuntimeContractView
   ├─ render
   └─ RoslynCompilationValidator(referenceSet)
   ↓
manifest
   ↓
GeneratedFileWriter(OutputSafetyContext)
```

建議 artifacts：

```text
CodeGen/
├─ Contracts/
│  ├─ RuntimeContractDescriptor*.cs
│  ├─ RuntimeContractLoader.cs
│  ├─ RuntimeContractValidator.cs
│  └─ RuntimeContractView.cs
├─ Assets/
│  ├─ ToolAssetResolver.cs
│  └─ RuntimeReferenceSet.cs
├─ Compatibility/
│  └─ ToolCompatibilityValidator.cs
├─ Policy/
│  └─ runtime-contract.json
└─ Packaging/
   └─ compiler references and package metadata

.config/
└─ dotnet-tools.json
```

實際命名可於 D0 核准，但 contract/asset resolution、generation pipeline、writer 與 CLI host
應保持可獨立測試，不得全部放入 `Program.Main`。

## 7. D0：固定 baseline 與架構決策

### 7.1 目標

在更動 ProjectReference 或 package layout 前，固定 Phase C 可回歸基準與 Phase D seam。

### 7.2 必須決定

1. Runtime foundation 保持手寫；Phase D 不生成 foundation declaration。
2. 主 SDK physical assembly 本階段不強制拆分。
3. Runtime contract descriptor schema、owner、versioning 與 validation 規則。
4. Roslyn compiler reference asset 的來源、package layout、identity/hash contract。
5. tool `PackageId`、`ToolCommandName`、初始 package version。
6. CLI asset override options 與 precedence。
7. repository-aware development host 與 packaged host 的邊界。
8. compatibility matrix 與新增 diagnostics code range。

建議初始 identity：

```text
PackageId:       MyFhirSdk.CodeGen.Tool
ToolCommandName: myfhir-codegen
ToolVersion:     1.0.0
```

### 7.3 Baseline

- 保存 `dotnet list reference`、tool project package layout 與現有 CLI help snapshot。
- 執行 Phase C committed-generation drift、public API snapshot 與 Release solution tests。
- 盤點 CodeGen 中所有 `MyFhirSdk.Core` compile-time references、`typeof(...)` assembly lookup、
  repository root、`AppContext.BaseDirectory` 與 `bin/obj` assumptions。
- 建立 D0 decision record，所有項目標記 Accepted/Deferred/Rejected。

### 7.4 完成與驗收

- 無 production behavior change。
- baseline output hash 與 831-artifact manifest 不變。
- 所有 D1-D8 所需決策已有 owner 與 exit criterion。
- 未決的 reference/package identity decision 會阻擋 D1，不以 temporary fallback 繞過。

## 8. D1：建立 versioned Runtime contract

### 8.1 方法

1. 定義 `runtime-contract.json` schema 與 strongly typed DTO。
2. 實作 loader，區分 read、JSON、schema、duplicate、unknown role 與 identity diagnostics。
3. 實作 validator，產生 immutable `RuntimeContractView`。
4. 將 external bootstrap abstract datatype slots 明確放入 contract，不以 runtime reflection
   推論。
5. 在 SDK architecture test 中反射實際 Runtime declarations，逐項比對：
   - CLR full name、generic arity；
   - base type；
   - abstract/sealed；
   - CodeGen 需要的 public property name/type/cardinality shape。
6. 產生穩定的 descriptor hash 與 contract identity model，於 D5 統一納入
   model/primitive manifest。

### 8.2 防止雙重真相

- FHIR canonical/kind/ownership 仍由 official inventory 與 ownership policy決定。
- descriptor 只描述 handwritten Runtime contract。
- renderer 不得直接讀 JSON DTO；只能讀 validated view/IR。
- 同一 contract fact 不可同時硬編碼在 descriptor、switch 與 tests；tests 應從 descriptor
  產生 expectations，再與 Runtime reflection 比對。

### 8.3 完成與驗收

- 缺少 `DataType`、`Resource`、`PrimitiveType<T>` 或核准 bootstrap symbol 時 fail-fast。
- duplicate、錯誤 base、錯誤 generic arity 與 unsupported schema 有 stable diagnostics。
- descriptor 與目前 SDK Runtime shape 完全一致。
- descriptor 不含任何 generated concrete Resource/datatype inventory。
- 相同 descriptor 兩次載入產生 ordinal-identical view/hash。

## 9. D2：移除 CodeGen 對完整 SDK 的編譯期依賴

### 9.1 方法

1. 讓 `ModelMetadataIrBuilder` 接受 `RuntimeContractView`，移除：
   - `using MyFhirSdk.Core`；
   - `typeof(FhirObject).Assembly`；
   - `typeof(DataType)` assignability/property comparison；
   - production `Assembly.GetType`。
2. 以 descriptor 的 roles/declared slots 完成 external metadata composition。
3. 讓 compilation service 僅接受抽象 `RuntimeReferenceSet`，不直接認識 SDK CLR types。
4. 從 `MyFhirSdk.CodeGen.csproj` 移除 `ProjectReference` 至 `MyFhirSdk.csproj`。
5. 若 tests 需要 SDK Runtime behavior，放在 integration/architecture test project，不反向加入
   production ProjectReference。

### 9.2 Architecture gates

至少檢查：

```powershell
rg 'ProjectReference.*MyFhirSdk.csproj' CodeGen
rg 'using MyFhirSdk.Core|typeof\(FhirObject|typeof\(DataType' CodeGen
rg 'MyFhirSdk\.(Types|Resources)' CodeGen
```

允許 renderer 由 IR 輸出 namespace/type name 字串；禁止 production CodeGen 載入或引用
concrete SDK models 做決策。

### 9.3 完成與驗收

- CodeGen project 可在不 build SDK project 的情況下 restore/build。
- production dependency graph 不含 MyFhirSdk implementation assembly。
- full model metadata entries 與 Phase C baseline byte-identical。
- external bootstrap metadata coverage 與 conflict diagnostics 不退化。
- SDK architecture/API/Parser/Serializer/Validator tests仍通過。

## 10. D3：建立 explicit Roslyn Runtime reference service

### 10.1 方法

1. 定義 immutable `RuntimeReferenceSet`，包含：
   - target framework；
   - ordered reference paths；
   - logical assembly identity；
   - contract/reference hash。
2. `RoslynCompilationValidator` constructor 必須要求 reference set/provider。
3. Trusted Platform Assemblies 與 Runtime contract references 分開處理及去重。
4. reference path 正規化後以 assembly identity 排序，不依 filesystem enumeration。
5. reference asset 缺少、不可讀、identity 不符、重複 identity 或 target framework 不符時，
   在 Roslyn emit 前失敗。
6. package reference asset 只能作 compilation metadata；不得作 inventory scan。

### 10.2 測試

- valid package-owned reference 可編譯 full model batch。
- missing/corrupt/wrong-version reference 產生 stable diagnostic。
- shuffled input reference order 產生相同 compilation result。
- diagnostics 不包含 machine-specific absolute path；必要時只顯示 logical asset identity。
- Windows/Linux reference resolution 結果一致。

### 10.3 完成標準

- production 無 `typeof(DataType).Assembly.Location`。
- production 不搜尋 repository `bin/obj`。
- tool package 解壓後的 reference assets 足以執行 full-batch Roslyn validation。
- generated source與manifest不因reference的實體安裝路徑而改變。

## 11. D4：移除 repository-root 與 asset-location assumptions

### 11.1 Tool asset resolution

建立 `ToolAssetResolver`，由 host 顯式組合：

- package-owned default policy root；
- optional CLI policy/runtime-contract/reference overrides；
- input package；
- output root；
- optional development repository safety context。

model mode 建議增加 `--policy-root`、`--runtime-contract`、`--runtime-reference` 等明確選項；
最終名稱由 D0 決策固定。primitive mode 現有 explicit `--policy` contract 不可默默改義。

### 11.2 Output safety

將 writer 的 transaction 能力與 repository-specific protected directory 規則分離：

```text
GeneratedFileWriter
  └─ atomic write、path validation、rollback

OutputSafetyContext
  ├─ universal protected paths
  └─ optional repository protected paths
```

packaged tool 即使沒有 repository root，仍必須拒絕 filesystem root、tool installation
directory、input archive/reference asset、rooted artifact、`..` traversal 與 collision。只有
明確偵測/指定 repository 時才套用 SDK source directory protection。

### 11.3 完成與驗收

- `Program` 不向上搜尋 `MyFhirSdk.sln` 作必要啟動條件。
- 從任意空白工作目錄執行 `--help` 與 generation 成功。
- policy resolution 不依目前工作目錄。
- output 不在 repository 時仍保有 atomic swap/cancellation rollback。
- development repository protected paths仍不可被直接覆寫。

## 12. D5：版本相容性與 manifest contract

### 12.1 Compatibility matrix

至少固定：

| Dimension | Phase D baseline |
|---|---|
| Tool/CodeGen | `1.0.0` |
| Runtime contract | `phase-a-v1+c4-primitives-v1` |
| Primitive policy | `1.1.0` |
| FHIR/package | R5 `5.0.0` / `hl7.fhir.r5.core#5.0.0` |
| Target framework | `net9.0` |

version range 規則必須 machine-readable。不能只在 README 寫「應相容」。

### 12.2 Diagnostics

新增專用 codes，不得把所有錯誤塞入 `InvalidInput`：

- Runtime contract read/schema/validation failure；
- Runtime reference missing/identity/hash mismatch；
- unsupported target framework；
- incompatible tool/Runtime/policy/FHIR version；
- packaged asset missing/corrupt。

錯誤排序以 logical asset identity、dimension、actual/expected ordinal 排序。

### 12.3 Manifest

在不破壞 schema evolution 的前提下加入：

- tool package id/version；
- Runtime descriptor version/hash；
- compiler reference logical identity/hash；
- target framework；
- compatibility matrix/schema version。

manifest 不記錄 absolute path、NuGet cache path 或 OS-specific separator。

### 12.4 完成與驗收

- baseline 組合生成成功。
- 每一個不相容 dimension 有正/負向測試。
- selected/full scope 使用同一 compatibility gate。
- manifest two-run/cross-platform byte-identical。
- 舊 manifest reader若需相容，必須有明確 schema migration test；否則 fail-fast。

## 13. D6：封裝 .NET local tool

### 13.1 Project/package 設定

依 D0 identity 設定至少：

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>myfhir-codegen</ToolCommandName>
<PackageId>MyFhirSdk.CodeGen.Tool</PackageId>
<PackageVersion>1.0.0</PackageVersion>
```

並補齊 authors、description、license、repository、readme、deterministic build、
`ContinuousIntegrationBuild` 等 package metadata。package 不得意外包含：

- FHIR test fixtures；
- repository absolute paths；
- SDK source；
- test assemblies；
- `bin/obj` 重複內容；
- 未使用的 implementation assemblies。

### 13.2 Local manifest

在 `.config/dotnet-tools.json` 固定 repository 開發版本。驗證：

```powershell
dotnet tool restore
dotnet myfhir-codegen --help
```

測試 pack 應輸出到暫存 artifacts directory，再以自訂 `--add-source` 安裝；不得依賴已存在的
global tool 或使用者 NuGet cache 命中。

### 13.3 Package content tests

- `.nupkg` entry path採 ordinal snapshot。
- required policies、descriptor、references 全部存在且 hash 符合。
- package 不含 forbidden files。
- `--help`、invalid arguments 與 diagnostics 不需 repository 即可執行。

### 13.4 完成標準

- pack 兩次的內容集合與重要 payload hash 相同；NuGet container metadata若有不可控欄位，
  應比較 normalized package contents並記錄原因。
- local manifest restore 後可執行 primitive/model mode。
- tool command、package id/version 在 CLI、manifest、NuGet metadata與文件一致。

## 14. D7：clean-environment、upgrade 與 CI

### 14.1 Smoke matrix

Windows與Ubuntu至少驗證：

1. clean temporary NuGet packages directory；
2. pack tool；
3. install/restore指定版本；
4. 從 repository 外工作目錄執行 `--help`；
5. 使用 locked offline R5 package執行 full model generation；
6. 執行 primitive generation；
7. 比對 committed/staged artifact hashes；
8. 卸載或由舊版本升級至目前版本；
9. 再次生成並比對 deterministic output。

### 14.2 CI 分層

```text
build/test
  → existing solution regression

pack inspection
  → nupkg contents and metadata

tool smoke
  → install/restore/run outside repository

cross-platform drift
  → compare normalized artifact hashes
```

CI 不依賴網路下載 FHIR package；NuGet restore 仍依標準 lock/cache policy。tool smoke 使用
本次 build 的 local package source，不使用公開 feed 上可能同名版本。

### 14.3 完成與驗收

- Windows/Linux均通過 clean-environment full batch。
- 沒有 current-directory、path separator、case sensitivity 或 file enumeration drift。
- failed generation不破壞既有 output。
- upgrade後 manifest/tool version正確更新；若 generation contract 未改，831 個 model
  source artifacts仍保持一致。
- CI 上傳 `.nupkg`、normalized package inventory 與 smoke logs作 artifacts。

## 15. D8：cleanup、操作文件與後續 handoff

### 15.1 Cleanup

移除：

- `RepositoryRootLocator` production default；
- CodeGen→SDK ProjectReference；
- `typeof(...)` Runtime assembly discovery；
- repository `bin/obj` search；
- package asset的development fallback；
- temporary reference/descriptor adapters；
- 已完成工作的 feature flags。

不得移除仍有 owner、reason、exit criterion 的 Runtime bootstrap declarations。

### 15.2 文件

更新：

- README 安裝、restore、CLI、generation與troubleshooting；
- Runtime/Models/CodeGen boundaries的實際 dependency diagram；
- Phase D handoff狀態與實作結果；
- compatibility matrix與manifest schema；
- package contents、release/rollback與upgrade操作；
- Runtime foundation handwritten ownership register。

### 15.3 Handoff

後續 handoff 應區分：

- 公開 NuGet release/signing/SBOM/provenance；
- physical Runtime/Models assembly split（若仍需要）；
- Profile generation與`SimpleQuantity` migration；
- deferred validation capabilities；
- 新FHIR patch/minor版本支援。

### 15.4 完成與驗收

- architecture scan無temporary discovery/fallback。
- package-only clean environment可重現831-artifact baseline。
- bootstrap與後續debt都有owner、理由、退出條件。
- rollback可以退回上一個tool package版本，不需回復handwritten models。

## 16. 建議實作順序與 PR 拆分

| WP | 建議 branch | 主要成果 |
|---|---|---|
| D0 | `feat/phase-d0-local-tool-baseline` | baseline、identity、descriptor/reference/package decisions |
| D1 | `feat/phase-d1-runtime-contract-descriptor` | versioned descriptor、loader、validator、Runtime shape gate |
| D2 | `feat/phase-d2-codegen-runtime-decoupling` | metadata builder改讀contract、移除SDK ProjectReference |
| D3 | `feat/phase-d3-explicit-roslyn-references` | explicit deterministic Runtime reference set |
| D4 | `feat/phase-d4-repository-independent-host` | package asset resolver、writer safety context、repo-free CLI |
| D5 | `feat/phase-d5-version-compatibility-manifest` | compatibility matrix、diagnostics、manifest extension |
| D6 | `feat/phase-d6-dotnet-tool-packaging` | PackAsTool、local manifest、package content tests |
| D7 | `feat/phase-d7-tool-smoke-ci` | clean install/restore/upgrade、Windows/Linux drift CI |
| D8 | `feat/phase-d8-tool-cleanup-handoff` | cleanup、operations、release/next-phase handoff |

每個 WP 建議從前一個已 merge 的 `main` 建立新 branch。不得把 SDK assembly 拆分、Profile
generation 或公開 feed release 混入同一個 Phase D PR。

## 17. Phase D Definition of Done

- Runtime foundation declaration保持手寫且有architecture shape gate。
- CodeGen production project不參考完整SDK project/assembly。
- CodeGen Runtime decisions只來自validated descriptor。
- Roslyn只使用explicit deterministic reference set。
- packaged tool不需repository clone或SDK build output。
- policies、descriptor與references由package擁有，CLI override precedence固定。
- compatibility mismatch全部在寫檔前以專用diagnostic失敗。
- `myfhir-codegen`可由local manifest restore並執行。
- clean Windows/Linux environment可生成primitive與full model batch。
- 831 model artifacts、public API、Runtime behavior與manifest provenance通過回歸。
- output two-run/cross-platform byte-identical，無absolute machine path。
- package contents受snapshot/hash gate保護。
- 無preview whitelist、assembly inventory scan、resource fallback或handwritten concrete dependency回歸。
- 文件、rollback、upgrade與後續debt完整。

## 18. 驗收流程

### 18.1 Entry baseline

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln -c Release --no-restore
dotnet test MyFhirSdk.sln -c Release --no-build --no-restore
dotnet test Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj -c Release --no-build --no-restore `
  --filter FullyQualifiedName~CommittedModelGenerationTests
git diff --check
```

### 18.2 Package/tool驗收

下列路徑應使用臨時資料夾；實際package version由D0決策代入：

```powershell
dotnet pack CodeGen/MyFhirSdk.CodeGen.csproj -c Release -o <package-source>
dotnet tool install MyFhirSdk.CodeGen.Tool `
  --tool-path <tool-path> `
  --add-source <package-source> `
  --version 1.0.0
& <tool-command> --help
```

然後從repository外目錄執行locked full model與primitive generation，與committed artifacts
比較hash。測試結束清理temp tool path；CI不得安裝到global tool location。

### 18.3 靜態快速檢查

```powershell
rg 'ProjectReference.*MyFhirSdk.csproj' CodeGen
rg 'using MyFhirSdk.Core|typeof\(FhirObject|typeof\(DataType' CodeGen
rg 'GetTypes\(|Assembly.Load|bin[/\\]|obj[/\\]' CodeGen
rg 'RepositoryRootLocator|MyFhirSdk.sln' CodeGen
rg 'DefaultComplexTypeNames|PrimitiveTypeNames|datatype-preview' CodeGen
```

預期 production CodeGen 無上述 dependency/discovery/fallback；若測試或development adapter
需要命中，必須位於明確test scope且有退出條件。

### 18.4 人工驗收情境

- 新使用者clone repository後只執行`dotnet tool restore`即可看到help。
- 將`.nupkg`複製到沒有repository source的環境仍可full-batch生成。
- 明確提供錯誤Runtime descriptor/reference時，不寫任何output。
- tool由前版升級後，CLI與manifest顯示正確version。
- 中斷大型generation後舊output完整，無staging/backup殘留。
- 同一input在Windows/Linux產生相同artifact hashes。

## 19. 主要風險與對策

| 風險 | 對策 |
|---|---|
| 為移除ProjectReference而複製Runtime type switch | versioned descriptor＋architecture reflection gate |
| compiler reference被誤用成inventory | contract/reference service分離；禁止assembly enumeration |
| 過早拆assembly造成type identity/API破壞 | Phase D不要求physical split；另立ADR與migration |
| package漏帶policy/reference | nupkg content snapshot、hash與clean-install smoke |
| 使用者NuGet cache掩蓋漏檔 | isolated packages/tool path與local source |
| repository外output安全退化 | universal safety context＋optional repository rules |
| manifest洩漏machine path | logical identity＋normalized path tests |
| 版本不符仍產生source | compatibility preflight在inventory/render/write前 |
| Windows/Linux reference或path drift | ordinal identity、cross-platform hash gate |
| clean-up誤刪bootstrap | ownership register與Runtime shape/API gate |
| scope膨脹到Profile或public release | PR gate；列入後續handoff |

## 20. ADR 與文件邊界

下列變更必須先有 ADR 或 D0 decision：

- Runtime/Models physical assembly split或public package topology；
- 既有public type的assembly identity改變；
- Runtime foundation由handwritten改為generated；
- `Extension`、`Meta`、`Narrative` declaration ownership搬移；
- tool package id、command name或major version改變；
- Runtime descriptor/reference distribution策略；
- manifest breaking schema change；
- 允許網路下載FHIR package或遠端policy；
- 支援新的FHIR版本或多target framework。

一般 loader、validator、reference provider、asset resolver、package target與test實作，只要符合
已核准contract，不需另立ADR。

Phase D完成後應另建release/next-phase handoff，不能只在PR描述留下未完成的packaging、
compatibility、bootstrap或Profile debt。
