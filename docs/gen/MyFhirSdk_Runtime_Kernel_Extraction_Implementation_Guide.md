# MyFhirSdk Runtime kernel extraction 實作指引

Version 0.1

- 狀態：Planning；K0 可在 ADR Proposed 時建立，ADR Accepted 後才可進入 K1 並搬移
  public declarations
- 適用範圍：第一階段 Runtime kernel physical extraction
- Baseline：Phase D handoff、FHIR R5 `5.0.0`、.NET 9 / `net9.0`
- 決策文件：`docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_ADR.md`
- 不包含：完整 Models/Client/IG package split、公開 NuGet release、新 FHIR/TFM

## 1. 目標

將現有單一 `MyFhirSdk.dll` 拆為以下兩個實體 assemblies：

```text
MyFhirSdk.Runtime.dll
  最小 foundation/bootstrap kernel

MyFhirSdk.dll
  R5 Models + Serializer/Parser/Validator + Client + IG support
  並提供舊 Runtime type 的 compatibility forwarders
```

完成後 dependency 必須是：

```text
MyFhirSdk ─────────► MyFhirSdk.Runtime
Runtime ──X────────► MyFhirSdk / Models / CodeGen
CodeGen ──contract/reference metadata──► Runtime
```

本階段保留既有 namespace 與 public member shape，但有意將指定 Runtime types 的 defining
assembly 改為 `MyFhirSdk.Runtime`。相容性由 type forwarding、old-binary fixture 與 migration
文件驗證，不宣稱 `AssemblyQualifiedName` 不變。

## 2. 非目標

- 不把 generated R5 Models 移到 `MyFhirSdk.R5.Models.dll`。
- 不拆 Client、Implementation Guide、Serialization 或 Validation package。
- 不重新生成或重新設計 831 個 model artifacts。
- 不把 `Extension`、`Meta`、`Narrative` 搬到 Models。
- 不改變 FHIR JSON wire format、metadata shape 或 validation behavior。
- 不以 `InternalsVisibleTo`、reflection、`dynamic`、assembly scan、repository/bin/obj lookup
  作為 production seam。
- 不同時升級 FHIR package、TFM、tool major version 或公開發布 SDK package。

## 3. Entry criteria

- Phase D D0-D8 已合併且完整 CI 綠燈。
- 工作分枝只包含本 migration 的變更；任何既有未提交變更已盤點。
- K0 可在 ADR Proposed 時執行，且只能新增 read-only baseline/inventory/test harness，不得改
  production behavior。
- 進入 K1 前，`MyFhirSdk_Runtime_Kernel_Extraction_ADR.md` 已由 Architecture + Runtime
  maintainers 標為 Accepted。
- 已保存拆分前可重建的 Git commit/tag 與 Release artifacts。
- rollback 不需要改寫或刪除使用者資料。

### 3.1 Primitive package-input前置順序

`MyFhirSdk_CodeGen_Primitive_Tgz_Input_Decision.md`若被Accepted，必須先完成其P0-P7並通過CI，
再建立本階段K0 baseline。K0應固定完成後的Tool/CodeGen版本、primitive manifest schema與
`.tgz`/directory equivalence hashes；不得讓package-input與assembly extraction在同一migration
PR平行變動。

## 4. 固定 ownership

### 4.1 Runtime kernel

初始 required set 由 `CodeGen/Policy/runtime-contract.json` 的 13 symbols 決定：

```text
FhirObject
IFhirExtensionValue
Base
Element
BackboneElement
BackboneType
DataType
PrimitiveType<T>
Resource
DomainResource
Extension
Meta
Narrative
```

`Extension`、`Meta`、`Narrative` 是刻意保留的 bootstrap types。它們不是因資料夾位置被
歸類，而是為了讓 Runtime base hierarchy 不依賴 Models。

### 4.2 保留於 MyFhirSdk

- `SimpleQuantity` 與全部 generated R5 primitive/datatype/resource declarations；
- primitive codecs、validators、registry 及 generated R5 composition；
- R5 model metadata/factory/validation composition；
- Serializer/Parser/Validator；
- Client；
- ImplementationGuides/TwCore。

`FhirSdkException`、其他 `core/` public/internal types 由 K0 inventory 決定；未列入表格的檔案
預設不搬移。

## 5. 必須先解決的 seams

### 5.1 Primitive value accessor

現況：`PrimitiveType<T>` 實作 internal `IPrimitiveValueAccessor`，Serializer/Validator 直接消費
該 interface。兩者分到不同 assemblies 後無法沿用相同 internal boundary。

K1 必須設計最小 Runtime integration contract，並符合：

- 只提供 untyped primitive value、value type 與必要 set 操作；
- 不公開 codec、format validator、registry mutation；
- public surface 有 API snapshot、XML docs 與負面 architecture tests；
- 不使用 reflection/dynamic fallback；
- 若是 public SPI，明確標示為 assembly integration contract，並納入 semantic versioning。

### 5.2 Primitive registry composition

現況：手寫 `PrimitiveRegistry` 和 generated `PrimitiveRegistry.Composition.g.cs` 依靠
same-assembly `partial/internal` composition。

K1 必須使 composition owner 仍在 `MyFhirSdk.dll`，並讓：

- Runtime kernel 不引用 generated wrapper types；
- CodeGen full-batch compilation 只依核准 compiler contract；
- codecs/validators 不因方便而全部成為 public API；
- generated output 保持 deterministic，registry 行為與目前一致。

若需要新增跨 assembly provider/builder SPI，先在 ADR 補上 shape、owner、versioning 與負面
測試。若需多個 CodeGen compiler references，先升版 descriptor schema 與
`RuntimeReferenceSet` contract，不能局部硬編碼。

### 5.3 R5 default composition

Serializer/Parser/Validator 目前的 default constructors 直接使用
`R5ModelMetadataProvider.Default` 和 `PrimitiveRegistry.Default`。本階段它們與 R5 composition
一起保留於 `MyFhirSdk.dll`，因此不得把 default provider 反向搬入 Runtime。

## 6. 工作包與順序

### K0：固定 migration baseline

交付：

1. 記錄 baseline commit/tag、assembly identity、TFM、public key token 與 deterministic Release
   hashes；Git 只提交 identity/hash/inventory，不提交 DLL。
2. 產生 assembly-aware public API inventory，至少包含：
   - public type full name 與 defining assembly；
   - base/interface assembly identity；
   - public field/property/method signature 中的外部 type identity；
   - 13 Runtime symbols、842 R5 surface types及其他 SDK public namespaces。
3. 保存 `ApprovedPublicApi.txt`、`ApprovedR5ModelApi.txt`、831 generated artifacts、model
   manifests 與 tool package inventory/hash。
4. 建立以拆分前 Release output 編譯的 consumer fixture。CI 必須從 pinned baseline
   commit/tag 在隔離 staging 建立舊 consumer binary，再以 split 後 assemblies 執行；驗證階段
   不得重新編譯該 consumer，也不得將 DLL 提交 Git。
5. 建立 current source consumer fixture，供 split 後重新編譯驗證。

Exit gate：baseline 可在 clean checkout 重現，且未改 production behavior/generated output。

### K1：建立跨 assembly integration seams

交付：

1. 取代 internal primitive accessor 的 same-assembly 假設。
2. 取代 generated primitive registry 的 partial/same-assembly 假設。
3. 對 metadata/provider/default composition 增加 dependency direction tests。
4. 確認 full-batch CodeGen Roslyn compilation 所需 reference surface。
5. 新增負面測試，禁止 Runtime 引用 `MyFhirSdk.Resources`、`MyFhirSdk.Types`、generated R5
   metadata、Client、IG 或 CodeGen。

Exit gate：仍在單一 `MyFhirSdk.dll` 時所有 behavior tests 通過，831 sources 與 manifest
byte-for-byte 不變；新 seam 已可在不使用 friend/reflection fallback 下跨 assembly。

### K2：建立 Runtime project 與 physical ownership

交付：

1. 新增 `MyFhirSdk.Runtime.csproj`，`TargetFramework` 只從
   `$(MyFhirSdkTargetFramework)` 推導。
2. 使用 explicit compile ownership，避免同一 source 同時編入兩個 assemblies。
3. `MyFhirSdk.csproj` 加入單向 `ProjectReference` 至 Runtime，並明確排除 Runtime-owned
   sources。
4. solution、build/test pipeline 與 clean targets 納入 Runtime project。
5. assembly name/version/deterministic/path-map 設定集中，不複製 `net9.0` literal。
6. 新增 architecture test，從 MSBuild/project metadata 和 compiled PE 驗證依賴方向。

Exit gate：兩個 assemblies clean build；Runtime PE assembly references 不含 `MyFhirSdk`、
CodeGen 或 R5-specific assembly；沒有 duplicate public declarations。

### K3：建立 compatibility facade/type forwarders

交付：

1. `MyFhirSdk.dll` 為所有搬移的 public Runtime types加入明確 `TypeForwardedTo`。
2. forwarder inventory 必須與 approved moved-type inventory exact match；多或少都失敗。
3. source consumer 重新編譯通過。
4. K0 舊 consumer binary 不重新編譯即可載入並執行。
5. 記錄 `typeof(T).Assembly`、`AssemblyQualifiedName` 與 reflection scan 的預期差異。

Exit gate：無 `TypeLoadException`、`FileNotFoundException`、`MissingMethodException`；缺少
`MyFhirSdk.Runtime.dll` 時測試必須以清楚的 dependency failure 失敗，不能靜默 fallback。

### K4：遷移 versioned Runtime contract

交付：

1. 升版 Runtime `contractVersion`；schema 只有在結構改變時才升版。
2. 將 `runtimeAssembly` 與 `compilerReference` identity 更新為
   `MyFhirSdk.Runtime`。
3. Runtime project 提供 canonical Release `$(TargetRefPath)`。
4. packaging pipeline 只建立一次 reference asset並明確注入 CodeGen build/pack。
5. 更新 SHA-256、compatibility matrix、manifest provenance 與 package inventory。
6. asset path 由單一 TFM property 推導；禁止 literal `net9.0` staging path。
7. Runtime reference 仍不得載入 default load context或作 inventory/model mapping。

Exit gate：descriptor strict loader/validator、PE identity/hash、full-batch Roslyn、clean package
install 與負面 mismatch tests 全部通過。

### K5：Runtime/SDK behavior migration

至少驗證：

- primitive construction、value access、codec 與 format validation；
- primitive JSON raw/metadata alignment；
- Resource、DomainResource、Extension/Meta/Narrative round-trip；
- complete R5 resource factory與open type/choice behavior；
- Parser/Serializer/Validator public default constructors；
- Client integration smoke；
- TW Core rules/package behavior；
- metadata/factory/validation composition不使用 assembly scan fallback。

Exit gate：現有 tests 加 migration-specific tests 全部通過，public member shape與FHIR JSON
fixtures沒有未核准 drift。

### K6：package、CI 與 cross-platform gates

交付：

1. CodeGen tool `.nupkg` 攜帶 canonical `MyFhirSdk.Runtime.dll` compiler-only reference。
2. package content snapshot驗證 Runtime asset logical identity/hash與無 machine path。
3. Windows/Ubuntu 各自 build/test/tool smoke；artifact compare 使用同一 canonical reference
   staging，不能比較平台各自建出的不同 DLL bytes。
4. clean environment 不依賴 repository、current directory、SDK `bin/obj` 或 NuGet cache。
5. 連續兩次生成與 Windows/Linux normalized output hashes 一致。
6. build/test output 同時含 `MyFhirSdk.dll` 與 `MyFhirSdk.Runtime.dll`。

Exit gate：Phase D D7/D8 gates 在新 topology 下保持綠燈。

### K7：文件、rollback 與 handoff

交付：

1. 更新 Runtime/Models/CodeGen boundaries 的實際狀態與 dependency diagram。
2. 更新 Phase D handoff 後續責任表，將 kernel extraction 標為完成，但不得把完整
   Runtime/Models split誤標完成。
3. 更新 SDK consumer migration、assembly-qualified-name、deployment file list與 troubleshooting。
4. 更新 CodeGen build/pack/asset override 文件。
5. 建立 Runtime kernel extraction handoff，列出後續 engine、Models、Client、IG physical split
   的 owner、理由與 exit criterion。
6. 演練尚未公開 release 的 source rollback；公開發布 rollback 仍由 release policy 管理。

Exit gate：沒有只留在 PR 描述中的新 debt；文件命令可由 clean environment 執行。

## 7. Required test matrix

| Gate | 必須驗證 |
| --- | --- |
| Project graph | 只有 `MyFhirSdk → Runtime`；Runtime 無反向 reference |
| PE/type ownership | 13 symbols 定義於 Runtime；MyFhirSdk 有 exact forwarders |
| Existing API | public type/member snapshot無未核准變更 |
| Old binary | 拆分前編譯 fixture 不重編譯即可執行 |
| New source | consumer restore/build/run 成功 |
| Reflection | defining assembly/AQN 差異符合核准 baseline |
| Runtime contract | descriptor shape、identity、TFM、hash exact match |
| CodeGen | 831-source full generation/Roslyn/runtime gates |
| Serialization | 全部 JSON fixtures 與 primitive alignment |
| Validation | generic rules、primitive rules、R5 generated rules |
| Client/IG | integration smoke與 TW Core behavior |
| Packaging | nupkg inventory、clean install、無 repository assumptions |
| Cross-platform | Windows/Ubuntu build/test/smoke與 normalized hashes |

## 8. Compatibility policy

以下項目預設必須保持：

- namespace、type name、generic arity；
- base/interface graph；
- public constructors、properties、methods與 nullability contract；
- JSON property names、resourceType、primitive lexical behavior；
- generated inventory與 deterministic output；
- CodeGen tool command、asset override precedence與 repository-independent host。

以下項目是本 migration 唯一預先允許的差異：

- approved Runtime types 的 defining assembly 改為 `MyFhirSdk.Runtime`；
- `MyFhirSdk.dll` 增加 matching type forwarders；
- descriptor/reference/manifest/package inventory 中相應 identity與 hash更新；
- SDK deployment 多一個必要 Runtime assembly。

任何其他 public API、FHIR behavior、manifest schema或package command差異都必須另行核准。

## 9. Implementation rules

- 每一工作包應可獨立 review；不要以單一巨大 PR 同時完成 K0-K7。
- 先建立測試與 seam，再搬 public declarations。
- 不手工複製 Runtime sources；每個 `.cs` 只有一個 production compile owner。
- 不因 namespace 相同就假設 type identity相同。
- 不更新 approved snapshots來掩蓋非預期差異；每個 baseline更新都要在 PR說明原因。
- 不提交 build output DLL；canonical reference由 pipeline build/stage。
- 不修改 generated R5 output來配合 assembly layout，除非 CodeGen contract確實要求且已核准。
- TFM、configuration、asset path與 assembly version使用單一設定來源。

## 10. Suggested PR sequence

| PR | Scope | 可否改type identity |
| --- | --- | --- |
| K0 | baseline、inventory、old/new consumer fixtures | 否 |
| K1 | primitive accessor/registry/composition seams | 否 |
| K2 | Runtime project、compile ownership、dependency tests | 是，需與K3在受控migration branch協調 |
| K3 | forwarders與binary/source compatibility | 是，完成相容bridge |
| K4 | descriptor、reference、packaging、manifest | 只改核准identity/hash |
| K5 | behavior與integration補強 | 否 |
| K6 | Windows/Linux CI、clean package smoke | 否 |
| K7 | operations、migration與handoff文件 | 否 |

若 repository policy不允許 K2在缺少K3時進入main，可將K2+K3合併為一個原子PR；仍須在
commit/測試結構中保留兩個可辨識步驟。

## 11. Final definition of done

- ADR 狀態為 Accepted，實作與核准 decision一致。
- `MyFhirSdk.Runtime.dll` 實際定義 approved kernel types。
- `MyFhirSdk.dll` 保留其餘 SDK功能並提供完整 forwarders。
- Runtime 無 SDK/Models/CodeGen 反向 dependency。
- 舊 binary與新 source consumers都通過，但 reflection identity差異有明確文件。
- CodeGen只依 versioned descriptor/canonical compiler reference，不依 Runtime project。
- 831 generated sources、JSON、metadata、validation、Client與IG regression通過。
- Windows/Ubuntu build/test/pack/smoke全部通過。
- 沒有 DLL binary 被提交到 Git，沒有 repository/bin/obj fallback。
- handoff 明確說明本階段只完成 kernel extraction，未完成完整 Models/package split。
