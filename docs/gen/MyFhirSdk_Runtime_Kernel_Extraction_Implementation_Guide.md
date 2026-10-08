# MyFhirSdk Runtime kernel extraction 實作指引

Version 0.9

- 狀態：K2 commit／push 與 CI 已由使用者確認（2026-10-08）；K3 重編／部署本機 gates 已完成，Windows／Ubuntu CI 證據待完成；K4 assets 遷移與 K6 仍待驗收
- 適用範圍：第一階段 Runtime kernel physical extraction
- Baseline：post-D Tool/CodeGen `1.1.0` handoff、FHIR R5 `5.0.0`、.NET 9 / `net9.0`
- 決策文件：`docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_ADR.md`
- 不包含：完整 Models/Client/IG package split、公開 NuGet release、新 FHIR/TFM

## 1. 目標

將現有單一 `MyFhirSdk.dll` 拆為以下兩個實體 assemblies：

```text
MyFhirSdk.Runtime.dll
  最小 foundation/bootstrap kernel

MyFhirSdk.dll
  R5 Models + Serializer/Parser/Validator + Client + IG support
  consumers 與相依 DLL 必須以 split assemblies 全面重編
```

完成後 dependency 必須是：

```text
MyFhirSdk ─────────► MyFhirSdk.Runtime
Runtime ──X────────► MyFhirSdk / Models / CodeGen
CodeGen ──contract/reference metadata──► Runtime
```

本階段保留既有 namespace 與 public member shape，但有意將指定 Runtime types 的 defining
assembly 改為 `MyFhirSdk.Runtime`。遷移由所有 consumers／相依 DLL 全面重編、獨立部署與
migration 文件驗證；不提供 forwarders 或舊 binary binding，不宣稱 `AssemblyQualifiedName` 不變。

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

2026-10-07 使用者以 Architecture、Runtime、CodeGen、Compatibility、Package／Release
兼任角色核准 ADR 與 acceptance decisions。ADR acceptance 的 K1 entry gate 已完成；
K1／K2／K3 本機實作與驗證紀錄見下文；K3 CI／跨平台執行與 K4–K7 尚未完成。

- Phase D D0-D8 已合併且完整 CI 綠燈。
- 工作分枝只包含本 migration 的變更；任何既有未提交變更已盤點。
- K0 可在 ADR Proposed 時執行，且只能新增 read-only baseline/inventory/test harness，不得改
  production behavior。
- 進入 K1 前，`MyFhirSdk_Runtime_Kernel_Extraction_ADR.md` 已由 Architecture + Runtime
  maintainers 標為 Accepted。
- 已保存拆分前可重建的 Git commit/tag 與 Release artifacts。
- rollback 不需要改寫或刪除使用者資料。

### 3.1 Primitive package-input前置順序

Primitive package input已在Tool/CodeGen `1.1.0`實作；必須先完成其P0-P7並通過CI，
再建立本階段K0 baseline。K0應固定完成後的Tool/CodeGen版本、primitive manifest schema與
`.tgz`/directory equivalence hashes；不得讓package-input與assembly extraction在同一migration
PR平行變動。

P6/P7已由PR #39合併，拆分前source pin為`1a28f01d8a4c3aeea46c63da875d01594aeee086`。
使用者於2026-09-30確認P6/P7分支及此merge commit的main CI均通過；此處記錄使用者
確認結果，未附CI run URL。後續純文件commit不自動改變source pin。
固定Tool/CodeGen `1.1.0`、primitive policy `1.1.0`、manifest schema `2`，
以及下列現行 hash，再由 canonical pipeline 重建核對：

| Asset | SHA-256 |
| --- | --- |
| Runtime descriptor | `128ba716806fa276186586ec735bb30525a8d0ddfaf00f9525f48b68fd8cad5a` |
| Compiler reference `MyFhirSdk/net9.0` | `7c945def6e2414e7944367d0df0ac33aa6386e4cdbedce0d05922ae5023176c9` |

完整 primitive inventory/hash 與 help 使用
[目前 `1.1.0` baseline](baselines/primitive-tgz-input/README.md)，保留 `1.0.0` 作歷史證據。
K0 同時重跑 `.tgz`／directory 完整 output equivalence 與 installed-tool smoke，記錄
FHIR archive、policy 及本次 `.nupkg` hash；package hash 由該次 canonical pack 取得。
目前 assembly 仍是 `MyFhirSdk, Version=1.0.0.0`，本前置功能沒有啟動 kernel extraction，
K1 的 ADR acceptance gate 保持有效。

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

2026-10-07 使用者確認 `FhirSdkException` 留 SDK，public `IPrimitiveValueAccessor` 隨
`PrimitiveType<T>` 歸 Runtime；descriptor mapping 仍為 13 symbols。
完整 ownership 依 [acceptance decision 第 2 節](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#2-declaration-與-seam-ownership-決策)。
其餘 metadata provider abstractions／implementations、internal seams 與 composition 留 SDK；
表外既有 declarations 預設不搬移，額外依賴須先 review。

## 5. 必須先解決的 seams

具體方案見 [Acceptance decision 核准紀錄](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md)。
全部設計 acceptance gates 已於 2026-10-07 核准；K1 依下列範圍實作並提供驗收證據。

### 5.1 Primitive value accessor

2026-10-06 使用者已單項確認並要求先行實作 public IPrimitiveValueAccessor，保留三個成員、
既有 null／型別轉型語義與第三方支援邊界；見 [accessor 決策](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#3-primitive-accessor-決策)。
當時的單項授權涵蓋 accessor、文件、snapshot 與驗證；2026-10-07 的整體核准已允許繼續 K1。

使用者另授權同步遷移 CodeGen descriptor/reference 至 `runtime-kernel-accessor-v1`，
使用最新單一 SDK compiler reference；允許 policy contract selection 與 manifest provenance 更新。
此為下述 K1 manifest byte-for-byte 規則的單項例外，所有 generated source bytes 仍保持不變。
新舊基準分開保存，見 [accessor evidence](baselines/kernel-accessor/README.md)。

原始問題：`PrimitiveType<T>` 實作 internal `IPrimitiveValueAccessor`，Serializer/Validator 直接消費
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

2026-10-07 使用者確認保留此 SDK 內部關係；registry、generated composition、wrappers
全部留在 `MyFhirSdk.dll`。K1 僅解除跨越 Runtime／SDK 邊界的 same-assembly 依賴，
不為此新增 public registry provider/builder SPI。

K1 必須使 composition owner 仍在 `MyFhirSdk.dll`，並讓：

- 以 ownership／dependency tests 保證 Runtime kernel 不引用 registry、generated composition 或 wrappers；
- CodeGen models／wrappers compilation 只依核准 Runtime reference 與來源契約；SDK composition
  完整語意編譯由此次生成產物的真實 SDK build／CI 負責；
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

K0 harness、固定 inventory 與本機驗證記錄見
[Runtime kernel K0 baseline](baselines/kernel-k0/README.md)。
執行入口為 `eng/Test-KernelMigrationBaseline.ps1 -RunRegressionAndSmoke`；
branch CI 與提交後的 clean-checkout 驗證仍是出口條件，不因新增 harness 自動視為已通過。

交付狀態（2026-10-06）：K0 已透過 PR #40 合併至 `main`，Git history 確認 merge commit
為 `ead6fd7d5f78e876f6d1c6fa9aca359a97bd50a5`。使用者已於合併前確認 branch CI 通過，
並於 2026-10-06 確認合併後 main CI 通過；據此記錄 K0 已交付。CI 結果來源為使用者確認，
未另取得 run URL 或 CI artifacts 進行獨立核驗。開發期間的 code review、P1/P2 修正與
後續 CI 修正見 [K0 交付紀錄](baselines/kernel-k0/README.md#k0-delivery-status-2026-10-06)。
此 K0 紀錄不代表 GitHub reviewer approval 或 ADR acceptance；K1 entry 核准已依 ADR 第 6 節
完成，validation amendment 的同日核准另見 ADR 第 8 節。

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
   commit/tag 在隔離 staging 建立舊 consumer binary，僅對歷史 SDK 執行；不將 DLL 提交 Git。
   2026-10-08 amendment 取消後續以 split assemblies 執行未重編 binary 的要求。
5. 建立 current source consumer fixture，供 split 後重新編譯驗證。

Exit gate：baseline 可在 clean checkout 重現，且未改 production behavior/generated output。

#### K0執行範圍與驗收

- Baseline metadata記錄完整source revision、CI證據、SDK `9.0.317`（依pinned `global.json`）、
  Release／TFM、assembly full identity與public key token、descriptor/reference/policy/FHIR
  hashes。實際SDK assembly與compiler-only asset分別標示用途，不將既有reference hash
  當成所有Release DLL的預期值；`.nupkg` hash由本次canonical pack取得。
- Inventory輸出採ordinal排序及固定換行，不含machine path或timestamp；generic arguments、
  constraints、nested types、constructors、events及signature中的type references也需包含
  assembly identity。既有API snapshots保留，不以新增inventory覆寫核准快照。
- 13 symbols、842 R5 surface types與831 model artifacts是核對基準，數量由實際資料取得；
  有差異先解釋，不刪減inventory來符合數字。另保存21個primitive/registry sources及兩份
  schema v2 manifests的inventory/hash，重跑22個primitive產物的雙input equivalence。
- Old consumer fixture source由K0提交，明確固定fixture revision與SDK baseline revision，
  不要求歷史SDK commit已含fixture。以隔離checkout產生的舊Release SDK編譯fixture，
  保存consumer DLL hash與編譯reference identity；僅驗證歷史基線，不置換split依賴。
- K0時尚無split assemblies：old binary先對拆分前SDK執行成功，current source fixture
  對當時單一SDK重新編譯／執行成功。2026-10-08起K3改驗全面重編／獨立部署，
  不列為K0已通過；current fixture現已參考split SDK／Runtime。兩種fixture至少觸及primitive value、model/base assignment與
  public parser/serializer round-trip，避免只有assembly載入而沒有實際API使用。
- 測試harness可增加獨立test projects、scripts及CI gate；不得新增production Runtime
  project、改Compile ownership／public declarations、加入forwarders、修改production seams、
  descriptor／compatibility版本或generated output。DLL只存ignored artifacts／CI artifacts。
- K0出口證據包含clean checkout重現、inventory deterministic比較、old/current consumer
  執行、既有API與Runtime regression及tool smoke結果。完成K0不表示ADR已Accepted，
  也不表示K1或physical extraction已開始。

### K1：建立跨 assembly integration seams

交付：

1. 取代 internal primitive accessor 的 same-assembly 假設。
2. 保留 SDK-owned registry、generated composition 與 wrappers 的 partial/internal composition；
   以測試保證 Runtime 不依賴它們，只解除跨越 Runtime／SDK 邊界的 same-assembly 依賴。
3. 對 metadata/provider/default composition 增加 dependency direction tests。
4. 依 ADR §8 的已核准修訂拆分驗證責任：models／wrappers 使用此次生成 sources 與真實
   `SimpleQuantity.cs`，只對 platform references 與核准 compiler reference 做 Roslyn compilation；
   registry 保留局部檢查，metadata／validation composition 保留 IR、mapping 與生成結構檢查。
   真實 SDK build／CI 必須編譯此次全部生成產物，與實際 providers、rules、registry、codecs／validators
   做整合驗證。補足 metadata／validation 與 registry contract drift 負面測試，不以 stub 取代實作。
5. 新增負面測試，禁止 Runtime 引用 `MyFhirSdk.Resources`、`MyFhirSdk.Types`、generated R5
   metadata、Client、IG 或 CodeGen。
6. 移除 validation assembly 的 production `InternalsVisibleTo`，調整依賴它的 generated-runtime
   tests；保留允許的窄範圍 test friend access。固定真實 `SimpleQuantity.cs` auxiliary source 契約，
   不封裝整套 SDK sources；wrappers 使用此次 input／policy 生成結果。

Exit gate：仍在單一 `MyFhirSdk.dll` 時所有 behavior tests 通過，831 sources 與 manifest
byte-for-byte 不變；新 seam 已可在不使用 production friend/reflection fallback 下跨 assembly。
CLI 的 models／wrappers compilation 與 composition 局部／結構檢查通過，且此次完整輸出的
真實 SDK build／integration gates 通過，才能宣稱完整生成驗收成功。K1 不宣稱已完成 K4
Runtime-only canonical reference／auxiliary asset packaging。

#### K1 實作進度與 reference surface 證據（2026-10-07）

狀態：**K1 本機實作與 exit gates 完成**。依 ADR §8 已核准的 amendment，production
pipeline 已拆分 models 語意編譯與 SDK composition 結構檢查；全部 declarations 仍由單一 SDK
production assembly 編譯。當時未建立 K2 Runtime project 或 K4 canonical Runtime assets；K3 原 forwarders 設計已由全面重編取代。

新增 `KernelIntegrationSeamTests` 與 `KernelPrimitiveRegistrationTests`，驗證：

- 13 個 kernel declarations 加 accessor 的真實來源可只用 .NET platform references 編譯；
  加入 Resources、Types、wrappers、R5 metadata、Client、IG 或 CodeGen 依賴的負面案例都失敗。
  測試排除 testhost TPA 中的 SDK／CodeGen／test assemblies，避免意外補足 reference。
- 829 個 generated models 與 20 個 wrappers 在 kernel reference 下，只缺 SDK-owned
  `SimpleQuantity`；加入真正 `Types/SimpleQuantity.cs` 可成功編譯。
  `KernelSdkIntegrationTests` 另以此次完整生成結果呼叫 production Roslyn validator，
  只有 kernel reference、fresh wrappers 與真實 SimpleQuantity source 時成功，移除 auxiliary 時失敗。
- 加入兩個 generated metadata／validation sources 後，仍缺 SDK-owned declarations。
  此測試預期失敗，用以記錄目前依賴，不能當成修訂後 models compilation／SDK integration gate 通過。
- 全部真實 SDK 功能來源可對隔離 kernel reference 編譯／emit，保留 SDK-internal composition，
  不加入 production friend attributes 或 validation stubs。Regex implementations 使用專案實際解析的
  framework source generator；不以手寫替代 validator。
- 將真實手寫 registry 的 `Define` 改名，generated composition 編譯即失敗，證明真實 drift
  可被捕捉；現有 runtime tests 另固定 missing／duplicate registration 行為。
- Public default engines 使用同一 SDK-owned R5 provider 與 primitive registry；僅實作 accessor
  不可註冊成 FHIR primitive，衍生 primitive 必須符合 declared value type，且不自動註冊。
- Accessor assignments 與原本直接轉型逐案比較，固定 reference／nullable／non-nullable value type
  的成功值、例外型別及失敗後保留原值／metadata 的行為。

Production pipeline 與測試的具體變更：

- `ModelGenerationPipeline` 從同一 input／policy 生成 wrappers，只作 compiler auxiliary sources，
  不新增輸出 artifacts。需要 SimpleQuantity 的 scope 讀取 CodeGen assembly 的 embedded 真實
  `Types/SimpleQuantity.cs`；沒有 repository／current-directory fallback。
- Auxiliary 契約 `simple-quantity-source-v1` 固定 UTF-8／LF SHA-256
  `321d5cd03d6ec3f2e70a26e608f088563c1f4d7c3ad4e680488d47f0e0affde8`；來源變動測試會失敗。
  K4 才將來源接入 descriptor、override、provenance 與外部 asset missing／mismatch 契約。
- `ModelMetadataGenerationPipeline` 對 metadata／validation 做 IR、mapping、syntax 與 entry point
  結構檢查；`RoslynCompilationValidator` 使用新 assembly name，不再要求舊 generator friend。
- `RealSdkSourceCompiler` 用此次生成的 831 model／composition 與 21 primitive／registry sources，
  加真正 SDK implementations／framework Regex generator 編譯並執行 metadata runtime tests。
  它不讀 committed `Generated`，不 reference 已編譯 SDK，也不注入 validation stubs。
  metadata provider、required-field rule、registry contract drift 及缺失 fresh composition 均會失敗。
- 部署用 SDK 只保留 `MyFhirSdk.Architecture.Tests` 的窄範圍 friend，測試檢查實際 assembly attributes。

K1 compiler contract 過渡機制：為遵守 frozen reference／manifest bytes，
`GetCompilerReferenceAsset` 先正常 build SDK，再以 `MyFhirSdkCompilerContract=true` 重建歷史
compiler-only 契約。該條件 build 仍含舊 friend attribute，僅 stage reference 至
`artifacts/compiler-contract/Release/net9.0/MyFhirSdk.dll`，不複製 implementation 到部署輸出。
條件 build 的 `IntermediateOutputPath` 隔離至 `artifacts/compiler-contract/obj/Release/net9.0/`，
`OutputPath` 也隔離至 compiler-contract artifacts；正常 SDK 的 `obj`／`bin` 不共用。
以 `PathMap` 將隔離來源路徑映射回歷史 logical path，維持 framework Regex generator 的
file-local type identity 與 deterministic bytes。這是歷史 SDK reference，
**不是 Runtime-only canonical reference**。SHA-256 保持
`9bedf2420e4290afdc0df144d04b77cb8e243bb2380d2ea181a952d5689afa01`。
CodeGen 已不使用該 friend name；K4 必須刪除 conditional symbol／contract build target／歷史屬性，
連同 descriptor、reference hash 與 provenance 原子切換。

本機驗證（Windows、.NET SDK 9.0.317）：Architecture **158 passed**；完整 solution
**857 passed、1 external-service test skipped、0 failed**，包含 full generation、API snapshots、
primitive equivalence、真實 SDK compilation/runtime 與 package tests。
`Invoke-CodeGenToolSmoke.ps1` 的乾淨 install／uninstall／reinstall 已通過，inputs 與工作目錄
置於 checkout 外；兩次 model generation 的 832 artifacts（831 sources + manifest）及 primitive
22 artifacts bytes 相同，且 `.tgz`／directory primitive input bytes 相同。完整 model／primitive
output 與 committed artifacts 相同；缺損 gzip trailer 仍阻止輸出。此項未執行版本升級 smoke。
結果與 log 留在 ignored `artifacts/kernel-k1/installed-tool-smoke`；執行使用既有 portable
PowerShell 7.5.3，未安裝額外系統工具。
既有 generated sources、manifests、policy、descriptor 與 frozen baselines 無修改。
這是本機結果，不宣稱 CI／Ubuntu 已通過。

```powershell
dotnet test Tests/Architecture/MyFhirSdk.Architecture.Tests.csproj -c Release --no-restore
dotnet test MyFhirSdk.sln -c Release --no-restore --logger trx --results-directory artifacts/kernel-k1/final-regression
```

測試編譯的 `K1.Kernel`／`K1.Sdk` 只存在記憶體，不是 K2 production assembly 或 K4 canonical
compiler reference。TRX 證據留在 ignored `artifacts/kernel-k1/final-regression`。

K1 review 的 P1 修正：原先條件 build 共用 `obj`，會刪除正常 SDK reference，並使 consumer
以 `BuildProjectReferences=false` 建置時出現 CS0006；現已隔離並保留 reference hash。
`eng/Test-CompilerReferenceIsolation.ps1` 比較正常 SDK 全部 16 個 `obj`／`bin` 檔案的 inventory
與 SHA-256，驗證 contract Rebuild、incremental build 前後完全不變，及上述 consumer build 成功。
此 gate 已接入 CI compiler-reference job；本機證據為
`artifacts/kernel-k1/compiler-isolation-summary.json`，修正後完整 solution regression 為
`artifacts/kernel-k1/p1-regression`（857 passed、1 skipped、0 failed）。

```powershell
pwsh -NoProfile -File eng/Test-CompilerReferenceIsolation.ps1
```

### K2：建立 Runtime project 與 physical ownership

本工作包建立真正 Runtime assembly 與 canonical reference 產出能力，提供第 5 節方案的
physical ownership／依賴方向證據；descriptor／package reference 切換仍於 K4 驗收。

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

#### K2 實作與驗證證據（2026-10-07）

Production compile ownership 已切換為 `MyFhirSdk.dll -> MyFhirSdk.Runtime.dll`。
`Runtime/KernelCompileItems.props` 明列 13 個核准 kernel declarations 與 public accessor，共 14 個
來源；Runtime project 關閉 default compile glob 並 link 原 `core/*.cs`，不複製來源、不改 namespace。
SDK 以同一 inventory 排除這些來源並單向 ProjectReference Runtime。
`FhirSdkException`、generated wrappers、registry、metadata、engines、Client 與 IG 仍由 SDK 定義。
Assembly name／version、TFM 與 deterministic／path mapping 設定由 shared props 推導。

Solution、正常 SDK build／clean 及 `eng/MyFhirSdk.CodeGen.Build.proj` 的 Clean 已納入 Runtime。
Runtime project 提供 Release `GetCompilerReferenceAsset`，eng build 的
`GetRuntimeCompilerReferenceAsset` 提供單獨產出能力；CodeGen package 仍選歷史 SDK reference，
descriptor、policy、generated sources、manifests 與 frozen baselines 未切換。

K1 的 legacy compiler-contract 條件 build 是非部署用的歷史單一 assembly 重建，保留 kernel
sources 並不 reference Runtime；其獨立 intermediate／output 與既有 hash 維持不變。
這不是第二個 production compile owner；K4 移除整個過渡機制。
`RuntimeReferenceService` 排除 host TPA 中的部署 SDK／Runtime，避免歷史 contract 與已載入的
Runtime 形成重複型別，也防止缺失 explicit asset 時由 host 補足。

`RuntimePhysicalOwnershipTests` 驗證 evaluated MSBuild Compile／ProjectReference items、compiled
PE dependencies、14 個 public type definitions 與 absence of duplicate SDK definitions。
既有 API／R5 snapshots 改為從兩個真實 owners 合併檢查，approved snapshots 不變；SDK engines
dependency tests 明確檢查 SDK，避免搬移後只掃空 Runtime 而失去保護。
Fresh generated SDK integration 現在 reference 真正 Runtime，不再將 kernel sources 併入 SDK。

本機 Windows 驗證：eng Clean 後完整 solution build／test **860 passed、1 external-service test
skipped、0 failed**。結果保留於 `artifacts/kernel-k2/clean-regression`；compiler isolation regression
同時檢查 SDK 與 Runtime 的 `obj`／`bin`。Runtime canonical reference 的 PE identity 為
`MyFhirSdk.Runtime, Version=1.0.0.0`，尚未替換 package reference。
Toolchain contract 與 compiler isolation checks 通過（36 個正常輸出檔的 inventory／hash 不變）；
乾淨 tool install／uninstall／reinstall smoke 通過，831 model sources 與全部 manifests／primitive
artifacts byte-for-byte 相同。Smoke 證據留在 `artifacts/kernel-k2/installed-tool-smoke`。

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Clean /p:Configuration=Release
dotnet test MyFhirSdk.sln -c Release --no-restore --logger trx --results-directory artifacts/kernel-k2/clean-regression
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:GetRuntimeCompilerReferenceAsset /p:Configuration=Release
pwsh -File eng/Test-CompilerReferenceIsolation.ps1 -OutputPath artifacts/kernel-k2/compiler-isolation-summary.json
```

2026-10-08 使用者確認 K2 已 commit／push 且 CI 通過；current consumer 已明確參考 SDK
輸出目錄的 Runtime DLL，完整 K0 runner 本機 9 gates 通過（860 passed、1 skipped、0 failed）。
依同日全面重編 amendment，K2 不再因缺少 forwarders 而被阻擋合併；仍須符合一般 PR／CI policy。
這不代表 K3 的完整 consumer／相依 DLL／獨立部署驗收已完成；K2／K3 保留可辨識的 commits／gates。

### K3：全面重編 consumers 與相依 DLL、驗證部署

2026-10-08 使用者核准以全面重編取代原 forwarders 與舊 binary 不重編驗收，詳見 ADR §3.4／§9
及 acceptance decision §6／§11。K3 不新增 `TypeForwardedTo`，不要求 K0 舊 consumer 搭配 split SDK 執行。

交付：

1. 建立 consumer／相依 DLL inventory，記錄 source、reference 方式、重編命令與部署依賴。
   至少包含七個 SDK test projects、current migration consumer、動態／fresh generated SDK
   consumers，以及依 DLL 載入的 inventory tool。Runtime／SDK／CodeGen 的 production graph
   一併檢查；CodeGen 仍不新增 SDK／Runtime ProjectReference，compiler asset 遷移留 K4。
2. 從 clean output restore/build 所有 current consumers 與相依 DLL，避免沿用拆分前產物；
   額外驗證一個使用搬移型別的 library → application 相依鏈，兩端皆重編，PE references 指向
   新 Runtime identity，SDK 不重複定義 14 個 kernel declarations，也沒有這些型別的 forwarders。
3. 執行 primitive value access、model/base assignment、public parser/serializer JSON round-trip
   與 K1 public accessor read/write、ValueType、既有 cast 行為。K0 fixture 不涵蓋 accessor，
   以 current fixture／獨立 SPI consumer 補足，不修改 frozen fixture。
4. 將建置產出的 consumer、重編相依 DLL、SDK／Runtime implementation assemblies 與
   deps/runtimeconfig 放入獨立部署目錄；以新 process 執行並核對載入路徑，不依 repository、
   baseline 目錄或 cache 補載 MyFhirSdk assemblies，不以 compiler-only reference 代替執行 DLL。
5. 記錄及測試 `typeof(T).Assembly`、`AssemblyQualifiedName`、reflection scan 的已核准差異；
   assembly-name-based scan／設定須指向新 defining assembly，不能假設重編會自動修正字串設定。
6. 在同一獨立部署情境移除 Runtime DLL，執行必須以明確 dependency failure 失敗，不能 fallback。
7. 保留 K0 pinned source／fixture／hash／inventories；舊 consumer 僅對歷史 SDK 執行。
   新增 K3 evidence 與 CI gate，不覆寫 K0 或把 baseline 重建通過等同 K3 部署驗收完成。

Exit gate：inventory 中所有 current consumers／相依 DLL 的 clean build/run 通過，獨立部署
正面測試無 `TypeLoadException`、`FileNotFoundException`、`MissingMethodException`，缺少 Runtime
的負面測試按預期失敗；14 個 declarations ownership／無 forwarders、reflection identity 與
public SPI 行為符合核准範圍，K0 baseline 無 drift。

#### K3 本機實作與驗證證據（2026-10-08）

新增 `eng/Test-KernelConsumerRebuild.ps1`，以 fresh artifact directory 執行七個 gates：
consumer inventory、clean regression、rebuilt consumers、isolated deployment、missing Runtime、
split API inventory、historical baseline。盤點 15 個 projects，包括三個 production projects、
七個 SDK test consumers、current／historical fixtures、inventory tool 及新增的 library／application。
盤點掃描整個 repository 的 source projects，剪除 artifacts／bin／obj 與工具 metadata 目錄，
`Tests` 外的未知 projects 也會失敗，須先補分類與重編計畫。
動態 model compilation 與 fresh generated SDK tests 由完整 regression 覆蓋。
Regression 逐一核對七個 assembly identities，各需唯一報告與實際 passed tests；缺少／重複／
不相關 reports、未知 skip／失敗 outcomes、counters 不符都會失敗。唯一允許的 skip 是未設定
`MYFHIRSDK_INTEGRATION_BASE_URL` 時既有 Client smoke test 的明確缺少設定原因；
每專案 counters 另寫入 `evidence.regressionProjects`，不只核對全 solution 的加總。

`Tests/KernelMigration/RebuiltLibrary` 對 application 暴露 `Base`／`PrimitiveType<T>`，
以真實 SDK 做 JSON round-trip、nullable／non-nullable accessor casts、ValueType 與 failed-write
state preservation；`RebuiltConsumer` 檢查兩個重編 PE 的 moved-type references，14 declarations
的 defining assembly、SDK 無對應 forwarders、AQN／reflection resolution 及全部四個 DLL 的載入路徑。
current consumer 也從 fresh source 重建、執行；沒有改寫 frozen K0 fixture。

隔離部署使用真實 implementation DLL 與建置生成的 deps/runtimeconfig。移除該目錄的 Runtime
後，新 process 以 `FileNotFoundException`／`MyFhirSdk.Runtime` dependency failure 失敗，
不從 repository、baseline 或 cache 補載；測試後還原部署 DLL，runner 成功時明確 `exit 0`。
Inventory tool 保留原單 DLL 模式，新增顯式 SDK＋Runtime 輸入，combined inventory 重跑 hash 一致，
908 個 exported types 中有 14 個 Runtime declarations；K0 單 DLL inventory 的 bytes 不變。

新增 `eng/Test-KernelConsumerRebuildHarness.ps1`，以合成 source trees 與 TRX 驗證上述拒絕條件，
包括整個 CodeGen suite 被跳過、以其他 assembly report 取代必要專案、以及 `Tests` 外新增 consumer。
14 個 harness checks 本機通過，證據位於 `artifacts/kernel-k3-review-harness/summary.json`；
CI matrix 也會執行並上傳 summary。

本機完整 clean solution regression：**860 passed、1 external-service test skipped、0 failed**；
K3 七個 gates 全部通過，code review 修正後最終證據位於
`artifacts/kernel-k3-review-fixed/evidence.json`，包含七個測試專案各自的執行 counters。
最終執行另使用明確 canonical compiler reference 與 Actions shell wrapper，成功 exit code 為 0；
這驗證 CI 呼叫模式與 expected negative-test exit handling，不代表遠端 CI 已執行。
另重跑 K0 baseline runner（未指定 `-RunRegressionAndSmoke`），歷史 SDK hash、old/current
consumers、API/source/package inventories 全部通過；該次 regression／primitive／tool smoke
明確標為 skipped，整體狀態為 partial，不宣稱完整 K0 smoke 已重跑。
歷史驗證證據位於 `artifacts/kernel-k3-k0-verified/evidence.json`。

```powershell
pwsh -File eng/Test-KernelConsumerRebuild.ps1 -OutputDirectory artifacts/kernel-k3
pwsh -File eng/Test-KernelConsumerRebuildHarness.ps1 -OutputDirectory artifacts/kernel-k3-harness
pwsh -File eng/Test-KernelMigrationBaseline.ps1 -OutputDirectory artifacts/kernel-k3-k0-check
```

CI 的 Windows／Ubuntu build/test/tool-smoke matrix 已接入 K3 runner，沿用下載的 canonical
compiler reference 供 CodeGen metadata build，仍不改 K4 descriptor／reference／package。
兩平台分別上傳 `kernel-k3-windows`／`kernel-k3-linux` evidence；此為 workflow 實作，
**尚未宣稱遠端 CI／Ubuntu 已通過**。Fixture 與部署操作見
[migration consumers README](../../Tests/KernelMigration/README.md)。

### K4：遷移 versioned Runtime contract

2026-10-07 已確認以單一 canonical MyFhirSdk.Runtime.dll compiler reference 加上 platform
references 為目標，CodeGen production 不新增 Runtime／SDK ProjectReference，不使用隱含 fallback。
三層驗證責任見 [acceptance decision 第 5 節](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#5-codegen-reference-與驗證責任決策)。
若 reference 不足，先提出並核准 ADR amendment，不自行追加 SDK reference。
設計已確認與本工作包執行通過為不同狀態；實際 hashes 與 gates 結果須於 K4 記錄。

交付：

1. 升版 Runtime `contractVersion`；schema 只有在結構改變時才升版。
2. 將 `runtimeAssembly` 與 `compilerReference` identity 更新為
   `MyFhirSdk.Runtime`。
3. Runtime project 提供 canonical Release `$(TargetRefPath)`。
4. packaging pipeline 只建立一次 reference asset並明確注入 CodeGen build/pack。
5. 更新 SHA-256、compatibility matrix、manifest provenance 與 package inventory。
6. asset path 由單一 TFM property 推導；禁止 literal `net9.0` staging path。
7. Runtime reference 仍不得載入 default load context或作 inventory/model mapping。
8. 明確封裝真實 `SimpleQuantity.cs` auxiliary source，固定 source inventory、asset identity／hash、
   override precedence、compatibility／manifest provenance 與 package inventory；不從 repository／bin／obj
   查找。Wrappers 必須由此次生成取得，不封裝過時的 committed wrappers。
   Descriptor 結構因來源契約改變時審查 schema 升版；K1 的 source／manifest bytes 不在此提前修改。

Exit gate：descriptor strict loader/validator、PE identity/hash、auxiliary source identity/hash、
models／wrappers Roslyn、composition 局部／結構檢查、此次完整生成產物的真實 SDK build／runtime
integration、clean package install 與負面 missing／mismatch tests 全部通過。

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
2. package content snapshot 驗證 Runtime reference 與 SimpleQuantity auxiliary source 的 logical
   identity/hash、exact inventory 及無 machine path。
3. Windows/Ubuntu 各自 build/test/tool smoke；artifact compare 使用同一 canonical reference
   staging，不能比較平台各自建出的不同 DLL bytes。
4. clean environment 不依賴 repository、current directory、SDK `bin/obj` 或 NuGet cache。
5. 連續兩次生成與 Windows/Linux normalized output hashes 一致。
6. build/test output 同時含 `MyFhirSdk.dll` 與 `MyFhirSdk.Runtime.dll`。
7. Windows/Ubuntu CI 必須將此次 clean tool 生成的完整 SDK 產物明確接入真實 SDK build／runtime
   regression；models CLI success 與 SDK composition 語意編譯結果分別記錄，不只驗證 committed sources。

Exit gate：Phase D D7/D8 gates 在新 topology 下保持綠燈。

### K7：文件、rollback 與 handoff

directory 退場與 packaged policy default 延後至 K0–K7 完成後，另立決策處理。
K7 handoff須保留此後續事項與CodeGen + Compatibility maintainers ownership；
本migration不移除directory input，也不將`--policy`改為optional。K7完成不等於核准
上述CLI變更，決策範圍見[Primitive input Decision](MyFhirSdk_CodeGen_Primitive_Tgz_Input_Decision.md)。

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
| PE/type ownership | 14 declarations（13 descriptor symbols＋accessor）定義於 Runtime；SDK 無重複定義或對應 forwarders |
| Existing API | public type/member snapshot無未核准變更 |
| Historical baseline | K0 frozen fixture 僅對歷史 SDK 執行；pin／hash／inventories 不變 |
| Rebuilt consumers | 所有 current consumers／相依 DLL clean restore/build/run；library → application 相依鏈與 PE identity 通過 |
| Deployment | 新 process 從獨立目錄載入 SDK／Runtime／重編相依 DLL；缺少 Runtime 明確失敗，無 fallback |
| Reflection | defining assembly/AQN 差異符合核准 baseline |
| Runtime contract | descriptor shape、identity、TFM、hash exact match |
| CodeGen | 831-source full generation；models／wrappers Roslyn、composition 局部／結構檢查及此次完整輸出的 SDK build／runtime gates |
| Auxiliary source | 真實 SimpleQuantity 的 source inventory／identity／hash、clean install、missing／mismatch 與無 fallback |
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

- 已確認的 public accessor SPI 與 `PrimitiveType<T>` 可見 interface graph 增加；保留既有 cast 行為；

- approved Runtime types 的 defining assembly 改為 `MyFhirSdk.Runtime`；
- consumers 與相依 DLL 全面重編並共同部署；不提供 type forwarders 或舊 binary binding；
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
| K2 | Runtime project、compile ownership、dependency tests | 是，依全面重編決策與一般 PR／CI policy |
| K3 | 全面重編 consumers／相依 DLL、SPI／reflection／獨立部署 | 驗證 K2 已核准 identity 遷移，不新增 forwarders |
| K4 | descriptor、reference、packaging、manifest | 只改核准identity/hash |
| K5 | behavior與integration補強 | 否 |
| K6 | Windows/Linux CI、clean package smoke | 否 |
| K7 | operations、migration與handoff文件 | 否 |

K2 不再因缺少 K3 forwarders 而被阻擋合併。K3 獨立 PR 完成全面重編／部署驗收；
K2 CI 通過不代表 K3–K7 已完成，仍須保留可辨識的工作包與證據。

## 11. Final definition of done

- ADR 狀態為 Accepted，實作與核准 decision一致。
- `MyFhirSdk.Runtime.dll` 實際定義 approved kernel types。
- `MyFhirSdk.dll` 保留其餘 SDK功能，無搬移型別的 forwarders。
- Runtime 無 SDK/Models/CodeGen 反向 dependency。
- 所有 current consumers／相依 DLL 全面重編與獨立部署通過；reflection identity 差異有明確文件。
- K0 舊 consumer 只對歷史 SDK 驗證，pin／fixture／inventories 保持不變。
- CodeGen 只依 versioned descriptor、canonical compiler reference 與明確封裝的 SimpleQuantity source，
  不依 Runtime／SDK production project，不攜帶整套 SDK sources。
- 831 generated sources、JSON、metadata、validation、Client與IG regression通過；metadata／validation
  composition 必須由此次完整輸出的真實 SDK build／CI 做語意編譯，CLI success 不代替此 gate。
- Windows/Ubuntu build/test/pack/smoke全部通過。
- 沒有 DLL binary 被提交到 Git，沒有 repository/bin/obj fallback。
- handoff 明確說明本階段只完成 kernel extraction，未完成完整 Models/package split。
