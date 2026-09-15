# MyFhirSdk CodeGen Phase D handoff

Version 2.0

- 狀態：D0–D8 implemented、CI passed，已合併至 `main`
- Baseline：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、.NET 9 / `net9.0`
- Tool package：`MyFhirSdk.CodeGen.Tool` `1.0.0`
- Tool command：`myfhir-codegen`
- Runtime contract：`phase-a-v1+c4-primitives-v1`
- Compatibility policy：schema `1`、exact ordinal match
- Model/primitive manifest：schema `2`

## 1. Phase D 交付結果

| WP | 結果 |
| --- | --- |
| D0 | 固定 Phase C baseline、public assembly identity、asset precedence 與 package 決策 |
| D1 | 建立 versioned Runtime descriptor、strict loader/validator 與 Runtime shape gate |
| D2 | CodeGen production assembly 移除完整 SDK compile-time/reference dependency |
| D3 | Roslyn validation 改用 explicit `RuntimeReferenceSet`，Runtime DLL 由 packaging pipeline 注入 |
| D4 | packaged host 不依賴 repository/current directory，assets 由 tool root 或 CLI override 解析 |
| D5 | 建立 exact compatibility matrix 與 manifest schema v2 provenance contract |
| D6 | 封裝 `PackAsTool` local package、固定 layout/content/hash 與 local manifest |
| D7 | 建立 clean-environment、真實 upgrade、Windows/Linux build/test/smoke/drift CI |
| D8 | 移除 development repository adapter，完成 operations、rollback、release 與後續 handoff |

正式 model output 仍為 831 個 sources，另含
`Generated/R5/model-generation-manifest.json`。Phase D 未改變既有 public CLR type 的
`MyFhirSdk` assembly identity，也未拆分 Runtime/Models physical assemblies。

## 2. 實際 dependency 與交付架構

```text
Packaging build
  MyFhirSdk.csproj --Release--> compiler-only MyFhirSdk.dll
                                      |
                                      | explicit RuntimeReferenceAssetPath
                                      v
CodeGen source ----------------> MyFhirSdk.CodeGen.Tool.nupkg
  | no SDK ProjectReference          | owns policies + descriptor + reference
  |                                  v
  +-- official R5 package ----> installed myfhir-codegen
                                      |
                                      v
                         deterministic Generated/R5 sources
                                      |
                                      v
                         MyFhirSdk assembly (current physical host)
```

CodeGen 使用 descriptor DTO 與 PE metadata，不載入 compiler reference、不掃描 SDK assembly
建立 inventory。Roslyn references 由 package-owned Runtime reference 加上 host TPA 明確組成。
目前 Runtime foundation 與 generated R5 Models 仍物理編譯在同一 `MyFhirSdk` assembly；邏輯
seam 已建立，未來 physical split 必須另立 ADR/migration。

## 3. 固定相容性與 manifest contract

| Dimension | 固定值/規則 |
| --- | --- |
| Tool package / CodeGen | `MyFhirSdk.CodeGen.Tool` / `1.0.0` |
| Runtime descriptor | schema `1` / `phase-a-v1+c4-primitives-v1` |
| Runtime compiler reference | `MyFhirSdk, Version=1.0.0.0`, `net9.0`, descriptor SHA-256 |
| FHIR | `hl7.fhir.r5.core#5.0.0`, FHIR `5.0.0` |
| Primitive policy | `1.1.0` plus exact SHA-256 |
| Model policies | name plus exact SHA-256 |
| Version policy | exact ordinal match |

schema v2 manifest 的 `compatibility` object 記錄 tool、CodeGen、descriptor、compiler
reference 與 TFM identity/hash；不得包含 physical asset path。schema breaking change、range
compatibility 或新的 FHIR/TFM 都需先更新 machine-readable contract 與 migration tests。

## 4. Runtime foundation handwritten ownership register

| Types | Owner | 保留理由 | 退出條件 |
| --- | --- | --- | --- |
| `Base`, `Element`, `BackboneElement`, `BackboneType`, `DataType`, `Resource`, `DomainResource` | Runtime foundation maintainers | shared inheritance 與執行契約仍位於既有 SDK assembly | 核准 assembly-boundary ADR，且 API/JSON/metadata/behavior migration suite 通過 |
| `PrimitiveType<T>` | Runtime primitive maintainers | generated primitive wrappers 需要共同 value/metadata contract | standalone Runtime contract/package 完成版本化並由 Models 消費 |
| `Extension`, `Meta`, `Narrative` | Runtime + R5 model maintainers | base model properties 與 extension/open-type bootstrap 目前避免 assembly cycle | ownership migration 不改 public API/JSON/metadata，不產生 duplicate declaration |
| `FhirObject`, `IFhirExtensionValue` | Runtime contract maintainers | model root 與 extension marker 是執行契約 | 預設永久保留；只有核准 Runtime contract revision 可取代 |
| `SimpleQuantity` | future Profile generation owner | constraint Profile 不屬於 Phase C/D specialization scope | Profile phase 生成 constraint declarations 並通過既有 API snapshot |

上述 declaration ownership 與 generated metadata ownership 分離；D8 cleanup 不授權刪除或
搬移任何一項。

## 5. Operations 與 rollback

安裝、restore、model/primitive generation、asset overrides、troubleshooting、package contents、
upgrade 與 rollback 指令，以 `CodeGen/README.md` 為操作基準。

rollback 單位是 tool package：將 local manifest pin 回上一個已驗證版本、restore 該真實
package、重新產生 staging output 並比較。不得用修改目前 source 版本號產生假舊版，也不需
回復 handwritten models。第一版 `1.0.0` 只有 install/uninstall/reinstall baseline，不宣稱 upgrade。

## 6. 後續工作責任表

| 項目 | Owner | 目前理由 | Exit criterion / gate |
| --- | --- | --- | --- |
| 公開 NuGet license/release | Release + legal maintainers | Phase D 未核准 license、公開 feed 或 promotion policy | 核准 license 與 package metadata；signing、SBOM、provenance、credentials、rollback/promotion CI 全部通過 |
| Runtime/Models physical split | Architecture + Runtime maintainers | 現有單一 assembly 保護 public type assembly identity 並避免 bootstrap cycle | 先依 `MyFhirSdk_Runtime_Kernel_Extraction_ADR.md` 與 implementation guide 核准/完成 Runtime kernel extraction；consumer recompilation、舊 binary、API/JSON/runtime compatibility gates 通過 |
| Profile generation / `SimpleQuantity` | future Profile CodeGen owner | constraint Profiles 不在 R5 specialization scope | 定義 Profile ownership/policy，生成 migration 通過 public API 與 behavior baseline |
| Deferred validation capabilities | Validation + CodeGen maintainers | terminology、FHIRPath invariant、fixed/pattern、targetProfile 尚需 Runtime support | 各 capability 有 versioned policy、Runtime executor、positive/negative generation/runtime tests |
| 新 FHIR patch/minor 版本 | CodeGen compatibility owner | 目前只核准 R5 `5.0.0` exact matrix | 新 package lock/hash、policy review、descriptor/matrix 更新及 831-equivalent full regression |
| 新 .NET/TFM | Build + Runtime contract owner | 目前 central TFM 為 `net9.0` | 更新單一 TFM/SDK 設定，重建 Runtime reference/hash，Windows/Linux build/pack/smoke/TPA CI 通過 |
| Contract-only Runtime reference | Packaging + Runtime owner | 目前使用完整 `MyFhirSdk.dll` 作 compiler-only asset | reference assembly 覆蓋 required surface、identity/hash 更新並通過 full-batch Roslyn/runtime gates |

沒有 owner、理由與退出條件的新 debt 不得只留在 PR 描述。

Runtime kernel extraction 的 proposed decision 與工作分解位於：

- `docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_ADR.md`
- `docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md`

ADR 核准前，上述文件只代表 migration proposal，不取代 D0-002 的已接受單一 assembly
baseline。

## 7. Phase D 最終 gates

- production CodeGen 無 SDK `ProjectReference`、repository locator、current-directory、`bin/obj`
  或 loaded SDK assembly discovery；
- package-only clean environment 在 Windows/Linux 可重現 831-source baseline；
- output two-run/cross-platform hashes 一致，failed/canceled write 可 rollback；
- package layout、policies、descriptor、Runtime reference 與 compatibility identities 受 gate 保護；
- public API、R5 model API、parser、serializer、validator 與 Runtime behavior regression 通過；
- 公開發布 gate 仍關閉，後續 debt 全部具有 owner、reason 與 exit criterion。

D8 merge 後，本文件取代 Version 1.0 中「D4 ready to start」的過時狀態，作為 release/next
phase 的唯一 Phase D handoff。
