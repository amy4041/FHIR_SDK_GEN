# ADR：MyFhirSdk Runtime kernel physical extraction

Version 0.5

- 狀態：Accepted（2026-10-07）；K1／K2 本機 gates 已完成，K3 compatibility 與 K4 reference 遷移仍待驗收
- 核准人：本專案使用者（本對話），兼任 Architecture、Runtime、CodeGen、Compatibility、Package／Release
- 核准基準：`5e198f14d977fe331b9d381de25492ff85c0c950` 的 ADR 與 acceptance decisions；原始核准見 §6，K1 validation amendment 的本對話核准見 §8
- 決策 owner：Architecture + Runtime maintainers
- 適用基準：post-D primitive package input `1.1.0` handoff、FHIR R5 `5.0.0`、.NET 9 / `net9.0`
- 實作指引：`docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md`
- 上位文件：
  - `docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_Handoff.md`
  - `docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`

## 1. Context

Primitive `.tgz` P0–P7已交付；P6/P7於PR #39合併至main，合併commit為
`1a28f01d8a4c3aeea46c63da875d01594aeee086`。使用者已確認P6/P7分支CI通過；
合併commit的main CI亦於2026-09-30由使用者確認通過。Tool/CodeGen為`1.1.0`，
primitive policy為`1.1.0`，manifest維持schema v2；`.tgz`與directory都要求explicit policy。
本ADR的拆分前基準應使用此post-D狀態，不回用Tool `1.0.0`的descriptor或manifest。

本 ADR 在 Proposed 階段允許 K0 建立唯讀 baseline、assembly-aware inventory 與 consumer test
harness。2026-10-07 已完成第 6 節最終核准並允許 K1；後續 production assembly、type ownership、
forwarders 與 descriptor 變更仍依核准範圍及各工作包 gates 執行。
Primitive input Decision的正式owner acceptance另行記錄，不能以CI通過代替。

K0 已由 PR #40 合併至 main，merge commit 為
`ead6fd7d5f78e876f6d1c6fa9aca359a97bd50a5`。使用者已確認 branch CI 通過，並於
2026-10-06 確認合併後 main CI 通過；證據來源與 review 修正記錄見
[K0 交付紀錄](baselines/kernel-k0/README.md#k0-delivery-status-2026-10-06)。
拆分前 source pin 維持 `1a28f01d8a4c3aeea46c63da875d01594aeee086`。
K0 交付與 ADR acceptance 分別記錄；本 ADR 的 owner 最終核准見第 6 節。

Phase D 已將 production CodeGen 與完整 SDK project/implementation 解耦，但 Runtime
foundation、generated R5 Models、Serializer/Parser/Validator、Client 與 TW Core
Implementation Guide 仍由 `MyFhirSdk.csproj` 編譯進單一 `MyFhirSdk.dll`。

目前實體配置為：

```text
MyFhirSdk.dll
├─ Runtime foundation/bootstrap types
├─ primitive runtime 與 generated primitive composition
├─ generated R5 primitives/datatypes/resources
├─ R5 metadata/factory/validation composition
├─ Serializer / Parser / Validator
├─ Client
└─ ImplementationGuides/TwCore
```

Phase D D0-002 刻意保留這個單一 assembly，並要求任何 physical split 必須另立 ADR 與
migration。這個 ADR 只處理第一階段：抽出最小 Runtime kernel；其餘 SDK 功能先繼續放在
`MyFhirSdk.dll`。它不是完整 Runtime/Models/package modularization。

## 2. Decision drivers

- 以編譯器強制 `MyFhirSdk` 只能單向依賴 Runtime kernel。
- 讓 generated models 使用獨立、版本化且較小的 Runtime compile contract。
- 降低一次搬移 842 個 R5 public model types 的 migration 風險。
- 保留現有 namespace、public member shape、FHIR JSON 與 Runtime behavior。
- 為未來 R4/R5 model assemblies 或 contract-only compiler reference 建立穩定落腳點。
- 不讓 assembly 拆分重新引入 CodeGen `ProjectReference`、repository lookup 或 assembly scan。

## 3. Decision

### 3.1 第一階段 assembly topology

採用兩個 production assemblies：

```text
MyFhirSdk.dll ───────────────► MyFhirSdk.Runtime.dll
     │
     ├─ generated R5 Models
     ├─ primitive/runtime composition
     ├─ Serializer / Parser / Validator
     ├─ Client
     └─ Implementation Guide support

MyFhirSdk.Runtime.dll ──X────► MyFhirSdk.dll
```

`MyFhirSdk.dll` 保持既有 assembly simple name，並在本階段同時扮演完整 SDK assembly 與舊
consumer compatibility facade。`MyFhirSdk.Runtime.dll` 是 kernel/contracts assembly；本階段
不宣稱已包含完整 Serializer/Parser/Validator engine。

### 3.2 Runtime kernel declaration ownership

下列 13 個 versioned Runtime descriptor symbols 移至 `MyFhirSdk.Runtime.dll`，namespace 與
public member shape 不變：

| Type | 第一階段 owner | 理由 |
| --- | --- | --- |
| `FhirObject`, `Base` | Runtime | 所有 model 的根契約 |
| `Element`, `DataType` | Runtime | element/datatype inheritance contract |
| `BackboneElement`, `BackboneType` | Runtime | generated backbone inheritance contract |
| `Resource`, `DomainResource` | Runtime | generated Resource inheritance contract |
| `PrimitiveType<T>` | Runtime | generated primitive wrapper base contract |
| `IFhirExtensionValue` | Runtime | extension open-type marker contract |
| `Extension`, `Meta`, `Narrative` | Runtime bootstrap | 避免 base hierarchy 形成 Runtime→Models cycle |

2026-10-07 使用者確認完整 ownership matrix：`FhirSdkException` 留在 SDK，因為 parser／primitive
codecs 使用它，擬搬移的 kernel 不需要它。它不是 initial descriptor symbol。
Public `IPrimitiveValueAccessor` 隨 `PrimitiveType<T>` 歸 Runtime，但不增加 descriptor mapping symbol。
其餘 internal seams、metadata provider abstractions／implementations 與 composition 的 owner
依 [acceptance decision 第 2 節](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#2-declaration-與-seam-ownership-決策)；
表外既有 declarations 預設留 SDK，額外搬移須先 review。
`SimpleQuantity` 保留在 `MyFhirSdk.dll`，因為它是 R5 constraint Profile，並依賴 generated
`Quantity`。

目錄位置不是 ownership source；最終 compile ownership 必須由 project 的 explicit
`Compile` items 與 architecture tests 固定。

### 3.3 Bootstrap ownership

本階段接受 `Extension`、`Meta`、`Narrative` 為版本化 Runtime bootstrap contract。雖然其
FHIR property shape 與 R5 有關，立即放入 Models 會使 `Element`、`Resource`、
`DomainResource` 反向引用 Models。重新設計 version-specific base models 不在本階段範圍。

### 3.4 Public identity compatibility

2026-10-07 使用者確認本節相容性邊界與 acceptance decision 第 6 節驗證方法。
Forwarders 必須涵蓋所有已公開且搬移的型別：K0 的 13 個 kernel symbols，加上已公開的 accessor；
此集合不同於 descriptor 的 13-symbol mapping。K3 保留 K0 consumer IL／hash，僅置換部署依賴，
並記錄必要的 deps/runtimeconfig 調整；另以 accessor consumer 驗證該 SPI，及缺少 Runtime DLL
時明確失敗的負面測試。此為設計確認，尚未完成 split binary 驗收。

移動 type declaration 會把 defining assembly 從 `MyFhirSdk` 改為 `MyFhirSdk.Runtime`；這是
有意且受控的 identity migration，不得描述為 identity 完全不變。

為保留舊 binary binding，`MyFhirSdk.dll` 必須為每個已搬移的 public type 提供明確
`TypeForwardedTo`。舊版 consumer fixture 必須在 split 後不重新編譯即成功執行。

以下差異視為已知後果，必須記錄及測試：

- `typeof(T).Assembly` 指向 `MyFhirSdk.Runtime`；
- `AssemblyQualifiedName` 的 defining assembly 改變；
- 依 assembly name 進行 reflection scan、DI registration 或設定檔解析的 consumer 可能需要
  migration；
- source consumer 重新編譯後直接參考 `MyFhirSdk.Runtime`。

本 ADR 不承諾 reflection identity 零變更。若產品要求 `AssemblyQualifiedName` 完全不變，
則不得搬移 public declarations，只能拆 internal implementation；此 ADR 必須改為 Rejected 或
Superseded。

### 3.5 Cross-assembly primitive seam

2026-10-06 使用者已確認並要求先行公開 IPrimitiveValueAccessor，保留三個成員與既有
value cast 行為，第三方僅實作介面不自動取得 FHIR registry／serialization 支援。
詳細範圍見 [accessor 單項決策](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#3-primitive-accessor-決策)。
以下描述原始問題與設計限制；單項授權之後的整體核准見第 6 節。

拆分前 `PrimitiveType<T>` 以 internal `IPrimitiveValueAccessor` 向 Serializer/Validator 提供
untyped value access。搬移 `PrimitiveType<T>` 後，這個 same-assembly seam 不再成立。

已選定最小、穩定且有 API snapshot 的 Runtime integration contract；primitive instance value 仍可寫。
它只能
暴露 primitive value read/write 所需能力，不得暴露 codec、validator 或 mutable registry。
介面為 `IPrimitiveValueAccessor`，保留 `UntypedValue`、`ValueType`、`SetUntypedValue` 與既有 cast 行為；不得以 reflection、`dynamic` 或廣泛
`InternalsVisibleTo` 取代正式 contract。

### 3.6 Primitive registry 與 model metadata composition

下列內容第一階段保留在 `MyFhirSdk.dll`：

- `PrimitiveRegistry`、primitive codec/validator implementation；
- generated `PrimitiveRegistry.Composition.g.cs`；
- `IModelMetadataProvider` implementation 與 generated R5 metadata；
- Serializer/Parser/Validator default R5 composition。

2026-10-07 使用者確認：registry、generated composition 與 wrappers 全部留在
`MyFhirSdk.dll`，保留 SDK 內部的 `partial/internal` composition。
K1 必須解除跨越 Runtime／SDK 邊界的 same-assembly 依賴，不要求消除 SDK 內部的 partial。
以 ownership 與 dependency tests 保證 Runtime 不依賴 registry、generated composition 或 wrappers；
K2 再以實際 Runtime project／PE 驗證依賴方向。本階段不新增 public registry provider/builder SPI，
也不為了拆分將 codec/validator 改為 public。此為 registry composition 單項決策，
整體 ADR 最終核准另見第 6 節。

### 3.7 CodeGen Runtime contract/reference

2026-10-07 使用者確認採單一 canonical Runtime compiler reference，加上 .NET platform references，
不新增 CodeGen production ProjectReference 或隱含 reference fallback。使用者於同日要求依 K1
發現採用 §8 的較小修訂；三層驗證責任更新為：

1. Generated models／wrappers：以此次 input／policy 生成的 sources、真實且明確封裝的
   `SimpleQuantity.cs`、platform references 與單一 Runtime reference 做 Roslyn compilation。
2. CLI composition 檢查：generated registry 保留既有 validation declarations 的局部檢查；
   SDK-owned metadata／validation composition 保留 IR、mapping 與生成結構檢查，
   不再對 Runtime reference 宣稱完成這兩個 sources 的語意編譯。
3. 真正 SDK build／runtime regression：以此次全部生成產物、實際手寫 providers、rules、registry、
   codecs／validators 與 Runtime dependency 完成語意編譯及行為驗證，不用 stub 替代 SDK 實作。
   必須編譯此次生成結果，不能只編譯既有 committed sources。

CLI 生成成功代表第 1、2 層通過；完整生成驗收還必須通過第 3 層 SDK build／CI。
移除 `MyFhirSdk.Generated.CompilationValidation` 的 production friend access，
並改寫依賴它的 generated-runtime tests；窄範圍 test friend access 依既有政策保留。

此為設計與驗收方式的確認；K1 檢查 reference surface 與整合測試責任，K2 建立實際 Runtime
assembly／reference 並驗證依賴方向，K4 切換 descriptor／packaging 後執行完整 gates。
尚未宣稱 Runtime-only reference 已驗證；整體 ADR 最終核准另見第 6 節。

CodeGen production assembly 仍不得 `ProjectReference` Runtime 或 SDK。完成 K1 composition
seam 與 K2 physical extraction 後：

- `runtime-contract.json.runtimeAssembly` 改為 `MyFhirSdk.Runtime`；
- compiler reference 改為同一次 Release build 的 canonical
  `MyFhirSdk.Runtime.dll` reference assembly；
- 更新 logical name、assembly version/public key token、TFM 與 SHA-256；
- packaged asset path 由 `$(TargetFramework)` 推導，不寫死 `net9.0`；
- Windows/Linux tool package 使用同一份 canonical bytes；
- compiler reference 只作 Roslyn metadata，不載入、不掃描 inventory。

K1 發現的 SDK composition surface 依賴按 §8 已核准的驗證責任處理，維持單一 Runtime
metadata reference。若此方案實作後仍需額外 metadata reference，必須另提 ADR amendment，
核准 schema／packaging／negative tests 後才實作；不得暗中加入第二個 reference 或回復 `bin/obj` 搜尋。

### 3.8 Package與版本策略

2026-10-07 使用者確認本節及 acceptance decision 第 7 節的 release 邊界。
允許 repository build/test、local pack/install、CI artifacts；release 責任角色與整體核准另行記錄。

本階段不授權公開 NuGet release。repository build/test output 必須同時提供：

```text
MyFhirSdk.dll
MyFhirSdk.Runtime.dll
```

未來 SDK package 必須確保 `MyFhirSdk.Runtime` 是必要 dependency 或 package content，不能讓
consumer 只取得 facade。正式 package ID、independent version range、signing、license 與
promotion 仍屬 release ADR/gate。

初始 Runtime assembly version 與相容 SDK baseline 對齊；Runtime contract version 必須在
descriptor 中明確升版，不能只靠 assembly file version 表達 contract breaking change。

## 4. Alternatives considered

### 4.1 直接完成 Runtime/Models/Client/IG 多 assembly 拆分

Rejected for this stage。搬移面過大，會同時處理 model identity、metadata composition、package
topology 與 consumer migration，難以隔離失敗原因。

### 4.2 完全保留 public type defining assembly

Not selected。只拆 internal implementation 能避免 identity migration，但無法讓 generated
models 直接依賴獨立 Runtime contract assembly，也無法達成本階段目的。

### 4.3 不提供 compatibility facade/type forwarders

Rejected。這等同立即 breaking migration；除非另立 major-version release ADR 並明確要求所有
consumer 重新編譯。

### 4.4 使用 `InternalsVisibleTo` 保留所有既有 internal seams

Rejected as default。它把 assembly name/signing 變成隱藏 contract，也不能解決第三方或未來
多 Models assembly composition。只有針對測試 assembly 的 narrow friend access 可依現有政策
評估；production composition 不採用此捷徑。

### 4.5 同一步將 Serializer/Parser/Validator 移入 Runtime

Deferred。長期邊界文件將通用 engines 視為 Runtime 責任，但目前 default constructors 直接
綁定 R5 metadata 與 primitive composition。第一階段先抽 kernel；完成 provider/composition
seam 後，再由後續 ADR 決定是否移動 engines。

## 5. Consequences

### 5.1 Positive

- 實體 dependency graph 可由 MSBuild/architecture tests 強制。
- Runtime compiler reference 的物理範圍可與 13-symbol logical contract 對齊。
- Models、Client 與 IG 的後續拆分可以分開進行。
- Runtime 不會因新增 R5 concrete type 而自然膨脹。

### 5.2 Cost and risk

- 搬移的 public types defining assembly 改變，type forwarding 不能保證所有 reflection-based
  consumer 零變更。
- 需要新增正式 primitive integration/composition contract。
- CodeGen descriptor、reference hash、package inventory、manifest provenance 與 CI golden 都會
  有受控更新。
- build、test 與 publish 必須把兩個 assemblies 視為不可缺少的一組。

## 6. Acceptance gates

2026-10-07，本專案使用者明確確認兼任 Architecture、Runtime、CodeGen、Compatibility、
Package／Release，核准目前 ADR 與全部已確認 acceptance decisions，允許開始 K1。
審查基準為 commit `5e198f14d977fe331b9d381de25492ff85c0c950` 的 ADR v0.2、
acceptance decision v0.1 及 Guide v0.3；此段保留原始核准紀錄。v0.4 新增的 validation
amendment 核准另見 §8，不回填成原始 review 已涵蓋。
逐項證據、角色及原文見 [Acceptance decision 核准紀錄](MyFhirSdk_Runtime_Kernel_Extraction_Acceptance_Decision.md#9-acceptance-gate-核准紀錄)。

ADR 只有在下列項目有 owner 並通過 review 後才能標為 Accepted：

- K0 baseline 與 assembly-aware public API inventory 已提交；
- Runtime ownership matrix 對每個 public/internal seam 有唯一 owner；
- primitive accessor 與 registry composition seam 已選定，不使用隱含 fallback；
- compatibility facade/type-forwarding policy 已核准；
- old-binary-without-recompile fixture 的來源版本與驗證方式已固定；
- CodeGen descriptor/reference migration 與 rollback 已定義；
- package/release owner確認本階段不會意外公開不完整 package；
- Architecture + Runtime maintainers 核准本 ADR。

實作完成還必須滿足 implementation guide 的 K0-K7 gates，特別是 API、舊 binary、JSON、
metadata、Runtime behavior、831-source generation 與 Windows/Linux tool smoke。

## 7. Rollback

2026-10-07 使用者確認以下發布前 rollback 方案，以可 review 的 revert commits 執行，
不改寫 main 歷史、不改動使用者資料。K7 須另提供演練證據。

拆分應以可回復工作包進行。在正式 release 前若 compatibility 或 composition gate 失敗：

1. 將 compile ownership 與 seams 回復到 K0 單一 `MyFhirSdk.csproj` 狀態；
2. 移除尚未發布的 Runtime project/output、forwarders 與新增 deployment 設定；
3. 回復K0固定的post-D `1.1.0` descriptor、reference identity/hash與tool package inventory；
4. 重新執行Phase D與primitive `.tgz`完整gates，確認兩種input及generated output無drift。

以 K0 delivery revision 取得 harness／inventories，由原 source pin 重建 baseline，並重跑完整 K0、
installed-tool smoke。比對 pinned identities/hashes 與 normalized inventories，不要求 NuGet zip bytes 相同。

已發布後不得以刪除 `MyFhirSdk.Runtime.dll` 回復；必須依 package rollback/promotion policy 發布
修正版並繼續提供 compatibility facade。

## 8. K1 reference surface amendment（2026-10-07）

**狀態：Accepted，設計已核准，實作 gates 待完成。** 核准來源為使用者在本對話要求
「請依照上述建議修改」，對應前一則建議：保留單一 Runtime reference、封裝真實
`SimpleQuantity.cs`、將 SDK composition 語意編譯交給真實 SDK build／CI、移除 production friend access。
此修訂解除原先等待 amendment 核准的設計阻礙，允許依本節繼續 K1；不代表實作驗收通過。
可重現的既有 K1 測試與結果見
[Guide K1 實作進度](MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md#k1-實作進度與-reference-surface-證據2026-10-07)。

### 8.1 實際依賴

目前 model full-batch 包含 829 個 models 及 2 個 SDK-owned composition sources。
Models 使用 SDK-owned `SimpleQuantity` 與 generated primitive wrappers；metadata 使用
`ImmutableModelMetadataProvider` 等 SDK-internal declarations；validation 使用
`ResourceRuleRegistry` 及 SDK-internal rules。現有 SDK compiler reference 提供這些型別，
`MyFhirSdk.Generated.CompilationValidation` 的 production friend access 提供 internal accessibility。
核准 kernel 的 14 個 declarations 沒有這些型別；單純替換 compiler reference 不足。

K1 隔離測試使用真正 wrappers／SimpleQuantity sources，證明 model declarations 可只依 kernel；
但現有完整 generation pipeline 並未納入這些來源，且 metadata／validation 還有額外 SDK 依賴。
不能把這項局部成功當成完整 pipeline 的 Runtime-only 驗收。

### 8.2 核准的驗證責任

採範圍較小的修訂，保留單一 canonical Runtime metadata reference 與原 ownership matrix。
CodeGen production 不新增 Runtime／SDK ProjectReference，metadata/provider/rules 留在 SDK。

1. Models／wrappers 維持 CLI 寫出 artifacts 前的 Roslyn compilation。輸入包含此次 FHIR
   package／policy 生成的 primitive wrappers、models，以及實際 `Types/SimpleQuantity.cs`。
   使用 .NET platform references 與單一 Runtime reference；不以舊 SDK metadata 補足 wrapper 型別。
2. `SimpleQuantity.cs` 作為明確封裝、版本管理且固定 hash 的 auxiliary source，仍屬 SDK
   production owner。建立 exact source inventory、asset identity／SHA-256、package inventory、
   override precedence 與 provenance；clean installed tool 不依賴 repository／bin／obj 查找。
   Wrappers 由此次生成取得，不封裝一份可能過時的 committed wrapper sources。
3. SDK-owned metadata／validation composition 的語意編譯移至真實 SDK build／CI。
   CLI 保留 IR、mapping 及生成結構檢查，檢查失敗仍阻止 artifacts 寫出；registry 保留既有
   局部 validation declarations 檢查，不擴充 stub 掩蓋 metadata 依賴。
   完整生成驗收必須以此次全部生成產物取代 SDK 的相應 compile items，與真實手寫
   providers、rules、registry、codecs／validators 編譯並執行整合測試；不得只 build 舊 committed 產物。
4. 移除 `MyFhirSdk.Generated.CompilationValidation` 的 production friend access，將依賴它的
   generated-runtime tests 改為真實 SDK 整合驗證；窄範圍 test friend access 依既有政策保留。
   補上 metadata／validation 及 registry 的真實 contract drift 負面測試。

CLI 成功只保證 models／wrappers 語意編譯與 composition 局部／結構檢查通過，
**不保證兩個 SDK composition sources 已完成語意編譯**。完整保證由真實 SDK build／CI 提供。
生成 artifact 數量與 source bytes 保持既有規則；K1 manifests 保持不變，新增 auxiliary asset
的 descriptor／compatibility／provenance／package 連動於 K4 原子遷移，不在文件中捏造 hash 或版本值。

### 8.3 實作 gates 與未採方案

K1 完成驗證責任拆分、移除 production friend access、真實 metadata／validation drift tests，
並固定 `SimpleQuantity` auxiliary source 契約；K2 建立實際 Runtime assembly；K4 完成 canonical
reference 與 auxiliary asset 的 packaging、hash、missing／mismatch、clean install、deterministic
及跨平台 gates。K1／K2 本機實作與驗證已完成，見 Guide 的實作證據；K3／K4／K6 證據仍待完成。

未採整套 SDK sources 封裝進 CodeGen，也未採多 metadata references；不新增 public metadata
SPI、不擴大 Runtime ownership。若此方案後續仍不足，另提 amendment，不能暗中增加 reference、
production friend access 或 source fallback。Production pipeline 已依本節切換；K1 為固定 manifests，
暫時重建歷史 compiler-only reference bytes，該資產仍保留舊 friend metadata，部署的 SDK 則已移除。
CodeGen 使用新的 compilation assembly name，無須該 friend；過渡 build 與歷史屬性於 K4 移除。
K1 review 後已將過渡 build 的 intermediate／output 隔離；logical path mapping 維持既有 hash，
並以 CI regression gate 確認不修改或刪除正常 SDK 的 `obj`／`bin`。

K2 production ownership 已切換為 SDK 單向依賴 Runtime，14 個 kernel declarations 由 Runtime
獨占編譯，source／public API shape 未變；實作證據見 Guide K2。歷史 compiler-only 契約仍由
隔離的非部署 build 重建，K4 才切換 descriptor／package；K3 forwarders 與舊 binary gates 待完成。
