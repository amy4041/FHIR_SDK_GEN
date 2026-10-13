# MyFhirSdk CodeGen Phase D handoff

Version 2.3

- 狀態：D0–D8 implemented、CI passed，已合併至 `main`
- Baseline：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、.NET 9 / `net9.0`
- Tool package：current `MyFhirSdk.CodeGen.Tool` `1.2.0`；Phase D baseline `1.0.0`／post-D baseline `1.1.0`
- Tool command：`myfhir-codegen`
- Runtime contract：current `runtime-kernel-v2`（descriptor schema 2）；歷史 Phase D／post-D 為 `phase-a-v1+c4-primitives-v1`
- Compatibility policy：schema `1`、exact ordinal match
- Model/primitive manifest：current schema `3`；歷史 Phase D／post-D schema `2`

## 1. Phase D 交付結果

### Post-D：primitive package input handoff（歷史 1.1.0 基準）

Tool/CodeGen `1.1.0` 的 primitive mode 以本機 versioned FHIR `.tgz` 為 preferred input，
保留 flat-directory compatibility mode；兩者都要求 explicit `--policy`。操作與復原步驟見
[CodeGen README](../../CodeGen/README.md)，實作與驗證落點見
[P0–P7 指引](MyFhirSdk_CodeGen_Primitive_Tgz_Input_Implementation_Guide.md)。

- 同版本兩種 input 的完整 22 個 primitive artifacts byte-identical；與 `1.0.0` 比較，
  21 個 primitive/registry sources 與 831 個 model sources 不變。
- Manifest 維持 schema v2，tool/CodeGen provenance 與 descriptor SHA-256 更新；
  沒有新增 archive hash 或 input mode 欄位。archive provenance 擴充需另立 schema decision。
- Runtime 仍為 `MyFhirSdk, Version=1.0.0.0`／`net9.0`，13-symbol contract 與 reference bytes 不變。
- Descriptor SHA-256：`128ba716806fa276186586ec735bb30525a8d0ddfaf00f9525f48b68fd8cad5a`。
- Compiler reference SHA-256：`7c945def6e2414e7944367d0df0ac33aa6386e4cdbedce0d05922ae5023176c9`。
- 歷史 `1.0.0` 與目前 `1.1.0` help、manifest、inventory/hash 證據保存在
  [primitive input baselines](baselines/primitive-tgz-input/README.md)。
- P6 使用真實舊版 package 執行 `dotnet tool update`；Windows/Linux 除各自 pack 外，
  另消費同一 canonical package，核對 package/FHIR/policy 與 generated output hashes。
  使用者已確認P6/P7分支CI通過；PR #39已合併main，commit為
  `1a28f01d8a4c3aeea46c63da875d01594aeee086`。此merge commit的main CI亦於2026-09-30由使用者確認通過。
- 取消或寫入失敗會嘗試還原 output；取消後 rollback 失敗回報 FSG0011 與 backup 路徑，
  保留可復原的備份。Recovery 操作見 README。

下列 D0–D8 交付表保留 Phase D 歷史；後續 kernel extraction K0 應以 post-D `1.1.0`
完成狀態建立新的 immutable baseline，不能回用 `1.0.0` manifest 當 current contract。

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

K2 已完成最小 Runtime kernel physical extraction；K4 完成 contract-only reference／auxiliary
source 遷移；K6 Windows／Ubuntu package、fresh generated SDK／deployment gates 已由使用者
確認 CI 通過並合併 main。K7 handoff／source rollback 本機證據見
[Kernel handoff](MyFhirSdk_Runtime_Kernel_Extraction_Handoff.md)，本次 K7 PR／CI 另行驗收。

```text
Consumer / rebuilt dependent DLLs -> MyFhirSdk.dll -> MyFhirSdk.Runtime.dll implementation

Runtime Release build -> metadata-only Runtime reference
                         + descriptor / policies + SDK-owned SimpleQuantity.cs
                         -> MyFhirSdk.CodeGen.Tool -> Generated/R5 sources -> MyFhirSdk.dll
```

CodeGen production 無 SDK／Runtime ProjectReference；只讀 reference PE metadata，Roslyn
references 由單一 canonical Runtime reference 與 host TPA 明確組成。Kernel 14 declarations
定義於 Runtime；generated Models、wrappers/codecs、composition 與 engines 仍定義於 SDK。
完整 Models／engines／Client／IG physical package split 尚未完成。

## 3. 固定相容性與 manifest contract

| Dimension | 固定值/規則 |
| --- | --- |
| Tool package / CodeGen | `MyFhirSdk.CodeGen.Tool` / `1.2.0`（歷史 baseline `1.0.0`／`1.1.0`） |
| Runtime descriptor | schema `2` / `runtime-kernel-v2` |
| Runtime compiler reference | `MyFhirSdk.Runtime, Version=1.0.0.0`, `net9.0`, exact canonical SHA-256 |
| FHIR | `hl7.fhir.r5.core#5.0.0`, FHIR `5.0.0` |
| Primitive policy | `1.1.0` plus exact SHA-256 |
| Model policies | name plus exact SHA-256 |
| Version policy | exact ordinal match |

schema v3 manifest 的 `compatibility` object 記錄 tool、CodeGen、descriptor、compiler
reference 與 TFM identity/hash；不得包含 physical asset path。schema breaking change、range
compatibility 或新的 FHIR/TFM 都需先更新 machine-readable contract 與 migration tests。

## 4. Runtime foundation handwritten ownership register

| Types | Owner / defining assembly | 保留理由 | 後續變更條件 |
| --- | --- | --- | --- |
| Base hierarchy、PrimitiveType、Resource/DomainResource、FhirObject/IFhirExtensionValue、IPrimitiveValueAccessor | Runtime maintainers / MyFhirSdk.Runtime | 已交付的 kernel 執行／SPI contract | 只有核准契約 revision 與 API/JSON/runtime migration 可變更 |
| Extension、Meta、Narrative | Runtime + R5 maintainers / MyFhirSdk.Runtime | 核准 bootstrap contract，generated metadata 仍屬 SDK | 另立 ownership decision，無 cycle／duplicate declaration，維持 API/JSON/runtime |
| SimpleQuantity | SDK + future Profile CodeGen owner / MyFhirSdk | constraint Profile 超出 specialization generation；工具只攜帶明確 auxiliary source | Profile generation migration 維持 API／behavior 並更新 source contract |

完整 14 declarations、source compile owner、identity/hash 與後續責任見 Kernel handoff。
Phase D 歷史 snapshots 不隨目前 ownership 改寫。

## 5. Operations 與 rollback

Tool build／pack／install／CLI overrides 以 [CodeGen README](../../CodeGen/README.md) 為操作基準。
Consumer 全面重編、AQN migration、implementation DLL deployment、troubleshooting 與整套
source rollback 以 [Runtime operations](MyFhirSdk_Runtime_Kernel_Extraction_Operations.md) 為準。

Tool-only rollback 必須使用匹配的 SDK source／contract／assets。跨 Runtime extraction boundary
時必須回復 source ownership、seams、consumers／相依 DLL、deployment、tool pin 與 manifests，
不能只降 tool version 或移除 Runtime DLL。K7 runner 重建固定 post-D 1.1.0 source，並分開
驗證 current solution、restored OLD solution、歷史 SDK deployment 與 hashes／inventories。
公開 NuGet release／已發布版本 rollback policy 尚未核准。

## 6. 後續工作責任表

| 項目 | Owner | 目前理由 | Exit criterion / gate |
| --- | --- | --- | --- |
| 公開 NuGet license/release | Release + legal maintainers | Phase D 未核准 license、公開 feed 或 promotion policy | 核准 license 與 package metadata；signing、SBOM、provenance、credentials、rollback/promotion CI 全部通過 |
| Models physical split | Architecture + Models + Runtime maintainers | 最小 kernel 已交付，Models/wrappers/composition 仍在 SDK | 另立 dependency／provider ADR，API/JSON/runtime、fresh SDK、全面重編／部署 gates 通過 |
| Profile generation / `SimpleQuantity` | future Profile CodeGen owner | constraint Profiles 不在 R5 specialization scope | 定義 Profile ownership/policy，生成 migration 通過 public API 與 behavior baseline |
| Deferred validation capabilities | Validation + CodeGen maintainers | terminology、FHIRPath invariant、fixed/pattern、targetProfile 尚需 Runtime support | 各 capability 有 versioned policy、Runtime executor、positive/negative generation/runtime tests |
| 新 FHIR patch/minor 版本 | CodeGen compatibility owner | 目前只核准 R5 `5.0.0` exact matrix | 新 package lock/hash、policy review、descriptor/matrix 更新及 831-equivalent full regression |
| 新 .NET/TFM | Build + Runtime contract owner | 目前 central TFM 為 `net9.0` | 更新單一 TFM/SDK 設定，重建 Runtime reference/hash，Windows/Linux build/pack/smoke/TPA CI 通過 |
| Primitive `.tgz` input acceptance／handoff | CodeGen + Compatibility maintainers | P0–P7與K0 immutable baseline已交付；Decision正式owner acceptance尚未記錄 | 由owner於獨立Decision補記正式acceptance；不重新標記已完成的K0為未實作 |

沒有 owner、理由與退出條件的新 debt 不得只留在 PR 描述。

directory 退場與 packaged policy default 延後至 K0–K7 完成後，另立決策處理。
Owner為CodeGen + Compatibility maintainers；延後理由是固定Runtime拆分期間的CLI與policy
基準。退出條件為K0–K7完成，且另案核准directory migration／版本策略與packaged policy
解析、override及相容性測試；目前directory支援與required `--policy`不變。

最小 kernel physical extraction 與 contract-only reference 已完成（K2／K4–K6）；K7 本機
handoff／rollback 已執行，PR／CI 待驗收。Engines、Client、IG、Models、Profile generation
與 release 的完整 owner／理由／exit criteria 位於 Kernel handoff 第 4 節，不只保留在 PR 描述。

Primitive package input 功能已於 P0–P7 交付；current Tool 1.2.0 保留 preferred tgz／directory
compatibility input 與 required explicit policy。Primitive input Decision 的正式 owner acceptance
以該決策文件為準，K7 不自行替其他決策改狀態。K0 baseline 使用 post-D 1.1.0 immutable source。

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
