# ADR：MyFhirSdk Runtime kernel physical extraction

Version 0.1

- 狀態：Proposed；核准前不得搬移 public type declaration
- 決策 owner：Architecture + Runtime maintainers
- 適用基準：Phase D handoff、FHIR R5 `5.0.0`、.NET 9 / `net9.0`
- 實作指引：`docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md`
- 上位文件：
  - `docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_Handoff.md`
  - `docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`

## 1. Context

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

`FhirSdkException` 是否移入 Runtime 必須在 K0 public API inventory 中以實際 consumer
dependency 決定；它不是 initial descriptor symbol，不得只因位於 `core/` 就自動搬移。
`SimpleQuantity` 保留在 `MyFhirSdk.dll`，因為它是 R5 constraint Profile，並依賴 generated
`Quantity`。

目錄位置不是 ownership source；最終 compile ownership 必須由 project 的 explicit
`Compile` items 與 architecture tests 固定。

### 3.3 Bootstrap ownership

本階段接受 `Extension`、`Meta`、`Narrative` 為版本化 Runtime bootstrap contract。雖然其
FHIR property shape 與 R5 有關，立即放入 Models 會使 `Element`、`Resource`、
`DomainResource` 反向引用 Models。重新設計 version-specific base models 不在本階段範圍。

### 3.4 Public identity compatibility

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

目前 `PrimitiveType<T>` 以 internal `IPrimitiveValueAccessor` 向 Serializer/Validator 提供
untyped value access。搬移 `PrimitiveType<T>` 後，這個 same-assembly seam 不再成立。

實作前必須建立一個最小、不可變且有 API snapshot 的 Runtime integration contract。它只能
暴露 primitive value read/write 所需能力，不得暴露 codec、validator 或 mutable registry。
具體命名由 K1 API review 決定；不得以 reflection、`dynamic` 或廣泛
`InternalsVisibleTo` 取代正式 contract。

### 3.6 Primitive registry 與 model metadata composition

下列內容第一階段保留在 `MyFhirSdk.dll`：

- `PrimitiveRegistry`、primitive codec/validator implementation；
- generated `PrimitiveRegistry.Composition.g.cs`；
- `IModelMetadataProvider` implementation 與 generated R5 metadata；
- Serializer/Parser/Validator default R5 composition。

目前 generated primitive composition 與手寫 registry 使用同 assembly 的
`partial/internal` seam。K1 必須把它改為明確的 SDK-owned composition boundary，使 Runtime
kernel 不引用 generated wrappers，且不為了拆分將 codec/validator 全部改成 public。

### 3.7 CodeGen Runtime contract/reference

CodeGen production assembly 仍不得 `ProjectReference` Runtime 或 SDK。完成 K1 composition
seam 與 K2 physical extraction 後：

- `runtime-contract.json.runtimeAssembly` 改為 `MyFhirSdk.Runtime`；
- compiler reference 改為同一次 Release build 的 canonical
  `MyFhirSdk.Runtime.dll` reference assembly；
- 更新 logical name、assembly version/public key token、TFM 與 SHA-256；
- packaged asset path 由 `$(TargetFramework)` 推導，不寫死 `net9.0`；
- Windows/Linux tool package 使用同一份 canonical bytes；
- compiler reference 只作 Roslyn metadata，不載入、不掃描 inventory。

若 full-batch compilation 在 K1 後仍需要 `MyFhirSdk.dll` 的 SDK composition surface，必須先
另行核准 descriptor/compiler-reference schema 的多 reference 設計；不得暗中加入第二個
reference 或回復 `bin/obj` 搜尋。

### 3.8 Package與版本策略

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

拆分應以可回復工作包進行。在正式 release 前若 compatibility 或 composition gate 失敗：

1. 將 compile ownership 回復到原 `MyFhirSdk.csproj`；
2. 移除尚未發布的 Runtime project/output；
3. 回復 Phase D descriptor、reference identity/hash 與 tool package inventory；
4. 重新執行 Phase D 完整 gates，確認 generated output 無 drift。

已發布後不得以刪除 `MyFhirSdk.Runtime.dll` 回復；必須依 package rollback/promotion policy 發布
修正版並繼續提供 compatibility facade。
