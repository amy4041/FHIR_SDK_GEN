# MyFhirSdk CodeGen Phase D D0 決策與 baseline

Version 1.0

- 狀態：Accepted
- 決策日期：2026-09-04
- 適用範圍：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、.NET 9
- 實作指引：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_Implementation_Guide.md`
- 上位邊界：`docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`

## 1. D0 範圍

D0 只固定 Phase C 回歸基準及 D1-D8 的架構契約，不改變 generator production behavior，
也不提前移除 CodeGen 對 SDK 的參考。後續實作若要偏離本文件，必須先更新此決策或建立
ADR；不得加入 repository、assembly scan 或 current-directory fallback 暫時繞過。

## 2. Phase C entry baseline

| 項目 | 固定值或 gate |
| --- | --- |
| Git baseline | `f3cad27` |
| FHIR package | `hl7.fhir.r5.core#5.0.0` |
| Package SHA-256 | `74b27cd1bfce9e80eaceac431edf230b0945a443564fbf5512f82e5fa50a80d4` |
| CodeGen contract | `1.0.0` |
| Runtime contract | `phase-a-v1+c4-primitives-v1` |
| Primitive policy | `1.1.0` |
| Target framework | `net9.0` |
| Model source artifacts | 831 |
| Model manifest SHA-256 | `64a48eb35bd9378f4cef4c6d8677db7a87f8b0bc95315fad86c9ee91afb97f01` |
| Complete R5 public surface | 842 types：57 Types、773 Resources、12 Core |
| Complete public API snapshot SHA-256 | `f9e69795d89fc1c2ecfbf354e40d77bdc44b04267d4b46be79c407e1069dda3e` |
| R5 model API snapshot SHA-256 | `5d150daeb9594108154411c7c26d642a767b10f2c87caccfec60d0f8a2589a37` |

SHA-256 均以 committed UTF-8、LF 檔案的原始 bytes 計算。model manifest 的
`artifactInventory.count` 必須為 831；generation batch 另含 manifest 本身，所以 writer
artifact count 為 832。`CommittedModelGenerationTests` 現在同時固定這兩個數量及 manifest
hash。D5 若依核准 schema 擴充 manifest，必須在同一變更中明確更新此 baseline 與測試。

原始 entry snapshots：

- `docs/gen/baselines/phase-d0/codegen-project-references.txt`
- `docs/gen/baselines/phase-d0/codegen-cli-help.txt`
- `docs/gen/baselines/phase-d0/codegen-package-layout.txt`

## 3. Accepted decisions

### D0-001：Runtime declaration ownership

狀態：Accepted。`FhirObject`、`IFhirExtensionValue`、`PrimitiveType<T>`、`Base`、`Element`、
`BackboneElement`、`BackboneType`、`DataType`、`Resource`、`DomainResource`、`Extension`、
`Meta`、`Narrative` 與 `SimpleQuantity` 在 Phase D 維持手寫且不生成。declaration ownership
與 metadata ownership 分離；移動 ownership 必須另立 ADR 並通過 API、JSON、metadata 與
Runtime regression。

### D0-002：physical assembly boundary

狀態：Accepted。Phase D 保持現有單一 `MyFhirSdk` SDK assembly 及 public type assembly
identity，不強制拆成 Runtime/Models assemblies。邏輯上的 contract/reference seam 先完成；
physical split 延後到獨立 migration phase。以 assembly split 作為 local tool 的前置條件為
Rejected。

### D0-003：Runtime contract descriptor

狀態：Accepted。唯一 source asset 為 tool 擁有的
`CodeGen/Policy/runtime-contract.json`，初始 `schemaVersion` 為 `1`、`contractVersion` 為
`phase-a-v1+c4-primitives-v1`。Runtime foundation 的維護者負責在手寫 public shape 變更時
同步更新 descriptor；CodeGen contracts 元件負責 schema、loader、validator 及 immutable
view。

Schema v1 固定包含：

- schema/contract version、target framework；
- runtime logical assembly identity；
- 相容的 tool/CodeGen、FHIR package/version、primitive policy 與 model policy identities；
- 依 CLR full name ordinal 排序的 foundation/bootstrap symbols；
- 每個 symbol 的 role、base CLR name、abstract/sealed、generic arity；
- CodeGen 需要的 external bootstrap declared slots，含 property CLR name/type、collection 與
  nullability facts；
- compiler reference logical identity、target framework 與 SHA-256。

Descriptor 不包含 generated concrete Types/Resources inventory，也不嵌入自己的 hash。
descriptor identity 是 committed/package asset 的精確 UTF-8 bytes SHA-256，檔案必須 no BOM、
LF；hash 由 resolver 計算並於 D5 寫入 manifest。loader 必須拒絕 duplicate JSON property、
unknown top-level/member field、unknown role、unsupported schema、缺少 required field、空白或
非 canonical identity。validator 必須拒絕 duplicate symbol/slot、錯誤 ordering、base/arity/
modifier 衝突、reference cross-link 不一致。所有比較使用 ordinal；錯誤排序使用 logical
identity、dimension、actual、expected。

### D0-004：Roslyn compiler reference asset

狀態：Accepted。初始 reference 由同一次 Release build 的 `MyFhirSdk.dll` 產生，作為
compiler-only SDK baseline；它不得被載入 default load context、掃描 inventory 或參與 model
mapping。package entry 固定為：

```text
tools/net9.0/any/Assets/RuntimeReferences/net9.0/MyFhirSdk.dll
```

logical identity 固定由 assembly simple name、assembly version、public key token（未簽署時為
`null`）及 target framework 組成；content identity 是 DLL 精確 bytes 的 SHA-256。descriptor
記錄 expected logical identity/hash，resolver 驗證實際 PE metadata 與 hash。reference asset
由 packaging build 明確產生及注入；CodeGen project 不得為此保留或新增 production
`ProjectReference`、搜尋 `bin/obj` 或選用任意已載入 assembly。未來改成 contract-only
reference assembly 屬相容的 implementation change，但必須保持所需 compile surface，更新
descriptor/hash 並通過 full-batch gate。

### D0-005：tool identity 與 target package layout

狀態：Accepted。

```text
PackageId:       MyFhirSdk.CodeGen.Tool
ToolCommandName: myfhir-codegen
ToolVersion:     1.0.0
```

tool package 的執行檔與相依項使用標準 `tools/net9.0/any/` layout；package-owned assets 放在
該目錄下的 `Policy/`、`Contracts/`、`Assets/RuntimeReferences/net9.0/`。policy、descriptor、
reference 與 package metadata 都必須進 package content/hash tests。正式 public NuGet 發布、
簽章及 SBOM 為 Deferred，由 release phase 負責。

### D0-006：CLI asset overrides 與 precedence

狀態：Accepted。D4 增加以下名稱：

| Option | Cardinality | 說明 |
| --- | --- | --- |
| `--policy-root <directory>` | 0..1 | model policy set 的整體 override |
| `--runtime-contract <file>` | 0..1 | Runtime descriptor override |
| `--runtime-reference <file>` | 0..* | explicit ordered reference inputs；重複 logical identity 為錯誤 |

primitive mode 現有 `--policy <file>` 保持 required 且語意不變。model mode 現有
`--policy <file>` 保持 primitive-policy file override；不得改名後默默改義。每一種 asset
各自採「CLI 明確值 > package-owned default」，沒有 repository/current-directory/environment
fallback。CLI 指定 `--policy-root` 時整組 model policy 都由該 root 解析，不與 packaged model
policies 混用；缺檔直接失敗。repeatable references 先解析 logical identity，再依 identity
ordinal 排序，CLI 順序不影響結果。

### D0-007：packaged host 與 development adapter

狀態：Accepted。`Program.Main` 只建立 packaged host，asset root 來自 tool installation
directory，不搜尋 solution 或 repository。repository-aware development adapter 只能位於 test/
development composition，且必須由明確 repository root 注入；它可以增加 repository protected
paths，但不能改變 production asset precedence。`RepositoryRootLocator` 作為 production default
在 D4 移除，D8 刪除暫時 adapter。

### D0-008：compatibility matrix 與 diagnostics allocation

狀態：Accepted。baseline matrix 為 tool/CodeGen `1.0.0`、Runtime contract
`phase-a-v1+c4-primitives-v1`、primitive policy `1.1.0`、FHIR/package
`5.0.0`/`hl7.fhir.r5.core#5.0.0`、target framework `net9.0`。初始只接受 exact ordinal match；
允許 range 前必須由 D5 machine-readable schema 明確表達，不能以 warning 降級。

保留 `FSG0100`-`FSG0199` 給 Phase D，既有 `FSG0001`-`FSG0040` 不重新編號：

| Range | Owner |
| --- | --- |
| `FSG0100`-`FSG0109` | Runtime descriptor read/JSON/schema/validation |
| `FSG0110`-`FSG0119` | Runtime reference missing/read/identity/hash/duplicate/TFM |
| `FSG0120`-`FSG0129` | tool/Runtime/policy/FHIR compatibility |
| `FSG0130`-`FSG0139` | packaged asset missing/corrupt/layout |
| `FSG0140`-`FSG0149` | output/host/package safety additions |
| `FSG0150`-`FSG0199` | reserved；需先更新本表再使用 |

實際 code assignment 由擁有該 validator 的 WP 固定，所有 Phase D preflight error 都必須在
讀取完整 definitions、render 或 write 前返回。

## 4. 現有 coupling inventory

| Assumption | Entry baseline location | Removal owner / exit criterion |
| --- | --- | --- |
| SDK compile-time reference | `CodeGen/MyFhirSdk.CodeGen.csproj` | D2：production dependency graph 不含 `MyFhirSdk` |
| `using MyFhirSdk.Core` | `Compilation/RoslynCompilationValidator.cs`、`Metadata/ModelMetadataIrBuilder.cs` | D2-D3：改讀 validated contract/reference set |
| `typeof(DataType).Assembly.Location` | `Compilation/RoslynCompilationValidator.cs` | D3：explicit reference set 可 full-batch compile |
| `typeof(FhirObject).Assembly` 與 Runtime reflection | `Metadata/ModelMetadataIrBuilder.cs` | D2：external slots 只來自 contract view |
| repository solution upward search | `Program.cs`、`Cli/RepositoryRootLocator.cs` | D4：任意工作目錄可啟動 packaged host |
| `AppContext.BaseDirectory/Policy` implicit lookup | `Cli/GeneratorCommandLineParser.cs`、`Policy/PrimitiveGenerationPolicyDefaults.cs` | D4：集中由 asset resolver 處理 |
| repository-specific protected paths | `Writing/GeneratedFileWriter.cs` 與 pipeline composition | D4：拆成 universal 與 optional repository safety context |
| .NET reference-pack discovery | `Compilation/RoslynCompilationValidator.cs` | D3：TPA/framework 與 Runtime references 分離且 deterministic |

Renderer 中輸出的字串 `using MyFhirSdk.Core;` 是 generated source contract，不是 CodeGen
assembly compile-time coupling，因此不列為移除目標。

## 5. Work-package ownership 與 exit criteria

| WP | Owner | D0 固定的 exit criterion |
| --- | --- | --- |
| D1 | CodeGen Contracts + SDK Architecture tests | descriptor 可 deterministic load/hash，並與手寫 Runtime shape 完全一致 |
| D2 | CodeGen dependency seam | production 無 SDK ProjectReference/reflection，Phase C output byte-identical |
| D3 | CodeGen compilation assets | explicit references 可 package-only full-batch compile，無 `typeof`/`bin`/`obj` discovery |
| D4 | CLI host + output safety | repository 外可執行，asset precedence 與 universal safety tests 通過 |
| D5 | compatibility + manifest | 每一 matrix dimension 有正負測試，manifest 無 machine path 且 deterministic |
| D6 | packaging | local manifest restore/run、nupkg content/hash tests 與 identity 全部一致 |
| D7 | CI | Windows/Ubuntu clean install、generation、upgrade 與 drift matrix 通過 |
| D8 | CodeGen maintainers | temporary fallback 全數移除，操作/rollback/後續 handoff 完整 |

## 6. D0 verification gate

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln -c Release --no-restore
dotnet test MyFhirSdk.sln -c Release --no-build --no-restore
dotnet test Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj -c Release `
  --no-build --no-restore --filter FullyQualifiedName~CommittedModelGenerationTests
git diff --check
```

D0 的成功結果不得產生任何 `Generated/R5` diff。D1 只有在本文件所有 D0-001 至 D0-008
保持 Accepted，且上述 gate 通過後才能開始。
