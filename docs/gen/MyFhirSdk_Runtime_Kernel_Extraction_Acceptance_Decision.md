# Runtime kernel ADR acceptance decision 草案

Version 0.1

- 狀態：Draft；第 3 節 accessor、第 4 節 registry composition 與第 5 節 reference／三層驗證方案已由使用者確認，其餘決策待 owner review；不是 ADR Accepted 紀錄。
- 日期：2026-10-06。
- 更新日期：2026-10-07，記錄 registry composition 與第 5 節 reference／三層驗證方案確認。
- 對應文件：[ADR v0.2](MyFhirSdk_Runtime_Kernel_Extraction_ADR.md)、[Implementation Guide v0.3](MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md)。
- 範圍：收斂進入 K1 前的設計決策與驗收方法，不執行 K1、不搬移 production declarations。

已確認採最小 public primitive accessor，並保留 SDK-owned registry 的 internal partial composition。
單一 Runtime compiler reference 與三層驗證方案亦已確認；其他 ownership、compatibility、release 邊界
仍須核准。已確認的 registry／reference 決策同步於 ADR §3.6／§3.7 與 Guide；
第 8 節列出其餘需同步的文字，整體 ADR 狀態保持 Proposed。

## 1. Baseline 與證據

K0 已由 PR #40 合併至 main，merge commit 為
`ead6fd7d5f78e876f6d1c6fa9aca359a97bd50a5`。Branch CI 與合併後 main CI 由使用者確認通過，
證據範圍見 [K0 交付紀錄](baselines/kernel-k0/README.md#k0-delivery-status-2026-10-06)。
既有本機 regression 為 817 passed、1 external-service test skipped；不將此數字冒充 CI artifact 統計。

拆分前 SDK source pin 維持 `1a28f01d8a4c3aeea46c63da875d01594aeee086`，
fixture content revision、SDK／descriptor／compiler reference hashes 以
[baseline pin](../../eng/kernel-migration-baseline.json) 為準。
K0 merge revision 是交付位置，不替換 SDK source pin。

## 2. Declaration 與 seam ownership 決策

以下是目標 compile owner；K1 仍編譯於單一 MyFhirSdk.dll，K2 才搬移 Runtime-owned declarations。
表中的責任角色是待指派的 review role，不代表已取得具名核准。

| Declaration 或 seam | 拆分後 owner | Review role |
| --- | --- | --- |
| FhirObject、Base、Element、DataType | Runtime | Runtime + Architecture |
| BackboneElement、BackboneType、Resource、DomainResource | Runtime | Runtime + Architecture |
| PrimitiveType<T>、IFhirExtensionValue | Runtime | Runtime + Architecture |
| Extension、Meta、Narrative bootstrap declarations | Runtime | Runtime + Architecture |
| 本草案的 IPrimitiveValueAccessor | Runtime | Runtime + Compatibility |
| FhirSdkException、SimpleQuantity | SDK | Runtime + Compatibility |
| PrimitiveRegistry、IPrimitiveDefinition、PrimitiveDefinition、PrimitiveValueAccess | SDK | Runtime |
| IPrimitiveCodec、IPrimitiveValidator、codecs、validators | SDK | Runtime |
| Generated primitives、PrimitiveRegistry.Composition.g.cs | SDK | Runtime + CodeGen |
| Generated R5 Types／Resources／metadata／factories | SDK | Runtime + CodeGen |
| Metadata provider abstractions、implementations 與 validation rule providers | SDK | Runtime |
| Parser／Serializer／Validator 與 default R5 composition | SDK | Runtime |
| Client、ImplementationGuides/TwCore | SDK | SDK maintainers |
| Descriptor loader、reference resolver、Roslyn validators、renderer | CodeGen | CodeGen |
| Compatibility type forwarders | SDK | Compatibility + Runtime |

其他未列出的現有 declarations 預設留在 SDK，不因目錄名稱含 Runtime 而搬移。
K2 以 explicit compile items 和 PE dependency tests 固定 owner。
若發現 kernel 尚依賴表外型別，先補 ownership review，不自動擴大搬移範圍。

FhirSdkException 留在 SDK：目前 parser／primitive codecs 使用它，擬搬移的 kernel 不需要它。
這將收斂 ADR §3.2 原先保留的選項，與 K0 ownership review input 一致。

## 3. Primitive accessor 決策

2026-10-06 使用者明確確認：accessor 改為 public，保留以下三個成員；null、錯誤型別及
第三方實作支援範圍採本節方案，並要求先行實作。此為 accessor 的單項授權，不代表 registry、
CodeGen、release 或整體 ADR 已核准，也不授權 physical extraction。
實作保留 PrimitiveType<T> 原有轉型邏輯，新增 XML docs、跨 assembly 行為測試，並更新
目前的 ApprovedPublicApi snapshot；K0 frozen inventories、fixture 與 source pin 維持歷史基準。

交付連動：使用者另選擇一併遷移 descriptor/reference 與生成證據，讓 pipeline 使用最新 SDK。
目前 contractVersion 為 `runtime-kernel-accessor-v1`，assembly identity 仍為 MyFhirSdk；
新 hashes 與重建方式見 [accessor contract evidence](baselines/kernel-accessor/README.md)。
這項授權允許更新 primitive policy 的 contract selection 與兩份 manifest 的 provenance，
不變更 primitive decisions、831 model sources、21 primitive sources 或 K0 歷史基準。
這是單一 SDK assembly 內的 accessor contract 遷移，不是 K4 的 Runtime assembly extraction。

建議將現有 MyFhirSdk.Core.IPrimitiveValueAccessor 提升為 public integration interface，保留名稱
與三個成員，由 PrimitiveType<T> 繼續 explicit implementation：

```csharp
namespace MyFhirSdk.Core;

public interface IPrimitiveValueAccessor
{
    object? UntypedValue { get; }
    Type ValueType { get; }
    void SetUntypedValue(object? value);
}
```

這是正式、受版本管理的 SPI；穩定的是契約 shape，不是 primitive instance 不可變。
它不提供 codec、validator、registry mutation 或自動註冊。

- UntypedValue 回傳既有 Value；ValueType 回傳 typeof(T)，nullable value type 不被改成其 underlying type。
- SetUntypedValue 保留目前 `(T?)value` 的直接轉型語義，不增加 parsing、numeric coercion 或 format validation。
- Reference／Nullable<T> 可寫入 null；非 nullable value type 的 null 或錯誤型別寫入維持現有例外行為。
  K1 以修改前後對照測試固定成功結果、例外型別及失敗後 Value 未被改寫。
- Public interface 技術上可由第三方實作，但單獨實作不表示它是可註冊的 FHIR primitive。
  Registry 保留 PrimitiveType<T> 衍生型別及宣告 value type 的驗證；Default registry 不自動接納第三方型別。
- SDK 的跨 assembly value access 透過此 interface，不使用 reflection／dynamic／production friend access。
  現有 PrimitiveValueAccess 的 base-type metadata 檢查留在 SDK；這不是反射讀寫 Value 的 fallback。

K1 應新增 API snapshot、XML docs、nullable/reference/value-type 行為測試，及使用獨立 test assembly
實際呼叫 SPI 的測試。禁止以只有同 assembly 的測試宣稱跨 assembly 契約已驗證。

這是明確核准範圍內的新增 public API，且 PrimitiveType<T> 的 public interface graph 會增加可見契約。
其他既有 public signatures 保持不變。未來變更 SPI 必須依 public API compatibility policy review，
不能視為可任意變動的 internal helper。

## 4. Registry 與 default composition 決策

2026-10-07 使用者確認：registry、generated composition、wrappers 全部留在 SDK。
保留手寫 PrimitiveRegistry 與 generated PrimitiveRegistry.Composition.g.cs 的 SDK-internal
partial composition。K1 必須解除跨越 Runtime／SDK 邊界的 same-assembly 依賴；
留在 SDK 內部的 partial/internal composition 可以保留，並以測試保證 Runtime 不依賴它。
因此本階段不新增 public registry provider／builder SPI。
不改 renderer 或 generated registry source 來配合不需要的實體拆分。

跨 assembly 邊界只有 Runtime primitive base／accessor 與 SDK 消費端；Runtime 不呼叫 registry，
也不引用 generated wrappers、R5 metadata、Client、IG 或 CodeGen。
Parser／Serializer／Validator 的 public default constructors 仍選用 SDK-owned
R5ModelMetadataProvider.Default 與 PrimitiveRegistry.Default。

K1 驗收包括：

- Registry 與 generated composition 的 compile owner 一致，並保留 missing／duplicate registration 的失敗行為。
- 無 production InternalsVisibleTo、assembly scan、動態 discovery 或 repository fallback。
- Generated model sources、primitive sources／composition 與 K1 manifests 保持 byte-for-byte 不變。
- Default composition 的行為測試與 dependency tests 通過；K2 再以真正 Runtime PE 驗證無反向依賴。

此決策取代「必須移除 SDK 內部 partial/internal 關係」的解讀。
若未來要拆出 Models 或 engines，另立 composition 決策；不在本次提前增加擴充 API。

## 5. CodeGen reference 與驗證責任決策

2026-10-07 使用者確認本節採用單一 Runtime compiler reference 與三層驗證方案。
拆分後使用一份 canonical MyFhirSdk.Runtime.dll reference assembly，加上 .NET platform references。
CodeGen production project 不新增 Runtime／SDK ProjectReference；compiler reference 僅供 metadata 使用，
不使用隱含 reference fallback。若 Runtime reference 不足，須先提出 ADR amendment 並核准。
此次確認設計與驗收方式，實作證據於 K1、K2、K4 分別完成，不代表已完成 Runtime-only 驗證。

| 驗證層 | 輸入及責任 | 不代表的保證 |
| --- | --- | --- |
| Generated models／primitive wrappers 的 Roslyn compilation | Generated sources + platform references + canonical Runtime reference | 不驗證 SDK-internal registry 實作 |
| Generated registry composition 局部驗證 | 現有 PrimitiveRegistryCompositionCompilationValidator 的 generated source、validation declarations 與 platform references | 不等同對真正 SDK registry 的整合編譯 |
| 真正 SDK build 與 runtime regression | 手寫 registry、generated composition、真實 wrappers、codecs／validators 與 Runtime dependency | 不允許拿 validation declarations 取代 production sources |

K1 必須檢查 generated sources 的實際 reference surface，並用測試證明 SDK-internal contract drift
能由真實 build／integration tests 捕捉。K2 建立實際 Runtime project 與 canonical reference 產出能力，
驗證 assembly 依賴方向；K4 才切換 descriptor／packaging，以實際 Runtime reference 執行完整 gates。
目前單一 assembly reference 包含較多 SDK 型別，不能以 K0 compilation 成功當成 Runtime-only 已驗證。

若 K1 的檢查證明仍需要 SDK metadata reference，應停止該遷移路徑並提出 ADR amendment，核准
多 reference schema／RuntimeReferenceSet／packaging／negative tests 後再實作。
本草案不授權第二個 reference，也不授權以 stub 擴充來掩蓋真實依賴。

K4 migration 應原子更新 descriptor contractVersion、runtimeAssembly／compilerReference identity、
canonical reference hash、compatibility matrix、package inventory 與 manifest provenance。
初始 Runtime assembly identity 對齊既有 `1.0.0.0`、PublicKeyToken=null；contractVersion 必須與 K0 不同，
不能只更改 DLL version。單 reference descriptor 結構不變時不升 schemaVersion；
實際新 contractVersion 值與 reference hash 在 K4 PR 固定並 review，不在尚未 build 時捏造。
13 個 model-generation symbols 保留；新增 accessor 是 compiler reference 的公開 SPI，
不因出現在 PE 就自動成為第 14 個 generator mapping symbol。

## 6. Compatibility 與舊 binary 決策

保留 SDK assembly simple name、namespace、既有 member shape 與 JSON behavior。
允許第 3 節的新增 SPI，以及已核准 Runtime declarations 的 defining assembly 改變。
對 typeof(T).Assembly、AssemblyQualifiedName 與依 assembly 掃描的 consumer 不承諾零差異。

K3 的 forwarder inventory 以「拆分前已在 SDK 公開、且實際搬移的型別」為準，要求 exact match。
K0 的 13 個 public kernel symbols 必須涵蓋；accessor 在 K1 成為 public、K2 搬移，因此也應有
forwarder。Forwarders 數量與 descriptor 的 13-symbol mapping 數量是不同驗收集合。

K0 frozen fixture 保持 content pin，不修改它來迎合拆分後 API。K3 應：

1. 從固定 SDK source 與 fixture 重建舊 consumer 一次，記錄 Consumer.dll hash。
2. 將該輸出複製到獨立執行目錄，以 split SDK／Runtime implementation assemblies 置換依賴。
   不重新編譯 consumer，不用 reference assembly 執行，不從 baseline 目錄補載舊 SDK。
3. 明確處理 .deps.json／runtimeconfig 與 Runtime dependency probing；若需要調整部署 metadata，
   記錄差異，不改寫 consumer IL，執行前後核對 Consumer.dll hash。
4. 執行既有 primitive、model/base、JSON round-trip 行為，另測 exact forwarders 與 reflection identity。
5. 移除測試目錄內 Runtime dependency 的負面測試必須失敗，不得從 repository／cache 意外補載。
6. 另用 K1 public SPI consumer 驗證 accessor 的 forwarding；不能把 K0 fixture 當成已涵蓋當時尚未公開的 SPI。

跨 assembly 載入、forwarding 與 deployment 的執行證據屬 K3 exit gate，不宣稱已由 K0 完成。

## 7. Release 與 rollback 決策

本階段只允許 repository build/test、local pack/install 與 CI artifacts，不授權公開 NuGet release。
SDK 執行輸出必須同時部署 MyFhirSdk.dll 與 MyFhirSdk.Runtime.dll；Tool 內的 compiler-only asset
不代替 SDK runtime deployment。Release owner 應 review workflow／pack／publish 路徑，
確認本 migration 不新增公開 promotion。正式 package ID、signing、版本範圍與 promotion 另行核准。

尚未公開 release 時，以可 review 的 revert commits 回復 migration 工作包，不改寫 main 歷史：

1. 回復 K1 前單一 SDK compile ownership／seams，撤除新增 Runtime project、forwarders 與 deployment 設定。
2. 由 K0 delivery revision 取得 harness／inventories，由固定 source pin 重建 canonical baseline。
3. 回復 Tool/CodeGen 1.1.0 的 descriptor、reference identity/hash、compatibility 與 package inventory。
4. 重跑完整 K0、Phase D、primitive tgz/directory equivalence 與 installed-tool smoke。

不以 zip package bytes 必須相同作為回復標準；核對 pinned identities/hashes 與 normalized inventories。
若已有公開發布，停止使用上述刪除 Runtime 的方式，轉交 release rollback／promotion policy。

## 8. 核准時需同步的文件修訂

| 文件位置 | 建議修訂 |
| --- | --- |
| ADR §3.2、Guide §4 | 固定 FhirSdkException 留 SDK，加入 accessor 與 internal seams ownership |
| ADR §3.5、Guide §5.1 | 採第 3 節 SPI shape／behavior，釐清穩定契約與可寫 instance value |
| ADR §3.6、Guide §5.2／K1 | 已於 2026-10-07 同步：保留 SDK partial composition；K1 驗證隔離邊界，不要求消除內部 partial |
| ADR §3.7、Guide K1／K2／K4 | 已於 2026-10-07 同步：三層驗證、單 reference 決策及不足時的 amendment 流程；執行證據待各工作包完成 |
| Guide §8、K3 | 新增 SPI 的 public API 例外、K0／K1 consumers 與 moved-public-type forwarder 集合 |
| ADR §3.8／§7 | 同步 release boundary 與可 review 的 source rollback |

上述變更與 owner 核准應在同一 acceptance PR 收斂。
未完成前 ADR 維持 Proposed；單純合併草案不代表核准。

## 9. Acceptance gate 核准紀錄

證據已具備與 owner 已核准是不同狀態。下列記錄區分使用者已確認的單項方案與仍 Pending 的 gates。
一人可兼任多個角色，但應明列各角色所核准的範圍。

| Gate | 決策與證據 | 必要 review role | 具名核准人 | 結果／日期／review link |
| --- | --- | --- | --- | --- |
| K0 baseline／inventory | 第 1 節、PR #40、K0 pin／inventories | Runtime + Architecture | 待指派 | Pending |
| 唯一 ownership | 第 2 節 | Runtime + Architecture | 待指派 | Pending |
| Accessor／registry seams | 第 3、4 節 | Runtime + CodeGen + Compatibility | 使用者（本對話）；角色歸屬待記錄 | 方案已確認：accessor 2026-10-06、registry 2026-10-07；實作 gates 另驗收，整體 ADR 未核准 |
| Facade／forwarding policy | 第 6 節 | Compatibility + Runtime | 待指派 | Pending |
| Old binary fixture／驗證方法 | 第 1、6 節 | Runtime + Compatibility | 待指派 | Pending |
| Descriptor/reference migration／rollback | 第 5、7 節 | CodeGen + Runtime | 使用者（本對話）；角色歸屬待記錄 | Partial：2026-10-07 確認第 5 節單 reference／三層驗證設計；K1／K2／K4 實作證據未完成，第 7 節 rollback Pending |
| Package／release boundary | 第 7 節 | Package/release owner | 待指派 | Pending |
| ADR 整體核准 | 全部 gates、第 8 節文件已同步 | Architecture + Runtime maintainers | 待指派 | Pending |

核准紀錄必須指向實際 review 的文件 revision／commit；草案 v0.1 的欄位不是授權。
有條件核准若仍留下 entry-blocking 決策，ADR 不得標 Accepted。
全部 gates 完成後更新 ADR status、核准日期／人員與 evidence links，並同步 Guide entry criteria。
K1–K7 的實作測試留在各工作包驗收，不回填成已在 acceptance review 執行。
