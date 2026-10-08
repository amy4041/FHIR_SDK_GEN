# Runtime kernel ADR acceptance decision 核准紀錄

Version 0.4

- 狀態：Accepted（2026-10-07）；設計 gates 已核准，允許開始 K1；實作 gates 另行驗收。
- 日期：2026-10-06。
- 更新日期：2026-10-07，記錄原始核准及 K1 validation amendment 核准。
- 對應文件：[ADR v0.4](MyFhirSdk_Runtime_Kernel_Extraction_ADR.md)、[Implementation Guide v0.5](MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md)。
- 範圍：設計決策、驗收方法及 K1 validation amendment，不在本文件執行 K1 或搬移 production declarations。

已確認採最小 public primitive accessor，並保留 SDK-owned registry 的 internal partial composition。
單一 Runtime compiler reference、三層驗證、ownership、compatibility、release／rollback 方案
亦已確認並同步於 ADR 與 Guide。第 8 節記錄同步範圍，第 9 節記錄責任角色與最終核准，
ADR 狀態為 Accepted。K1 reference surface 發現後的較小修訂已依本對話授權核准，見第 5、10 節。
未執行的 gates 不因設計確認而標為通過；K1 的本機執行證據另記錄於第 10 節及 Guide。

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

2026-10-07 使用者依建議確認本節完整 ownership matrix；表外型別不得自行擴大搬移。

以下是目標 compile owner；K1 仍編譯於單一 MyFhirSdk.dll，K2 才搬移 Runtime-owned declarations。
表中的責任角色由本專案使用者兼任，已於 2026-10-07 明確確認；詳見第 9 節。

| Declaration 或 seam | 拆分後 owner | Review role |
| --- | --- | --- |
| FhirObject、Base、Element、DataType | Runtime | Runtime + Architecture |
| BackboneElement、BackboneType、Resource、DomainResource | Runtime | Runtime + Architecture |
| PrimitiveType<T>、IFhirExtensionValue | Runtime | Runtime + Architecture |
| Extension、Meta、Narrative bootstrap declarations | Runtime | Runtime + Architecture |
| IPrimitiveValueAccessor | Runtime | Runtime + Compatibility |
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
此結論已同步至 ADR §3.2，與 K0 ownership review input 一致。

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

已確認將 MyFhirSdk.Core.IPrimitiveValueAccessor 提升為 public integration interface，保留名稱
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
同日使用者要求「請依照上述建議修改」，核准依 K1 發現調整驗證責任，詳見第 10 節及 ADR §8。
此處記錄修訂後的設計，實作證據於 K1、K2、K4 分別完成，不代表已完成 Runtime-only 驗證。

| 驗證層 | 輸入及責任 | 不代表的保證 |
| --- | --- | --- |
| Generated models／primitive wrappers 的 Roslyn compilation | 此次 input／policy 生成的 models、wrappers + 封裝的真實 SimpleQuantity.cs + platform references + canonical Runtime reference | 不驗證 SDK-owned metadata／validation／registry composition 的完整語意 |
| CLI composition 局部／結構檢查 | Registry 保留現有 validator 的 validation declarations；metadata／validation 保留 IR、mapping 與生成結構檢查；失敗仍阻止 artifacts 寫出 | 不等同對 SDK-owned composition 的真實整合編譯 |
| 真正 SDK build 與 runtime regression | 此次完整生成產物 + 真實手寫 providers、rules、registry、codecs／validators 與 Runtime dependency | 不允許以 stub 或只編譯舊 committed 產物代替此次生成結果 |

CLI 生成成功不再代表兩個 SDK metadata／validation composition sources 已完成語意編譯。
完整生成驗收必須通過真實 SDK build／CI 及 runtime integration tests；驗證時明確取代相應
generated compile items，以此次輸出為準。移除 validation assembly 的 production friend access，
依賴它的 generated-runtime tests 改為真實 SDK 整合驗證，保留允許的窄範圍 test friend access。

`SimpleQuantity` 的 production owner 仍在 SDK。CodeGen 只封裝這份真實 auxiliary source，
固定 source inventory、identity／hash、package inventory、override precedence 與 provenance；
wrappers 使用此次生成結果，不封裝整套 SDK sources，不用 SDK metadata reference 補足依賴。

K1 必須檢查 generated sources 的實際 reference surface，並用測試證明 SDK-internal contract drift
能由真實 build／integration tests 捕捉，特別補足 metadata／validation drift 的負面案例，
並完成驗證責任拆分與移除 production friend access。K2 建立實際 Runtime project 與 canonical reference 產出能力，
驗證 assembly 依賴方向；K4 才切換 descriptor／packaging，以實際 Runtime reference 執行完整 gates。
目前單一 assembly reference 包含較多 SDK 型別，不能以 K0 compilation 成功當成 Runtime-only 已驗證。

原先 K1 發現的 SDK metadata 依賴已由本次驗證責任修訂處理，允許依修訂繼續 K1。
若實作後仍需額外 metadata reference，須再提出並核准 amendment，包含 schema、
RuntimeReferenceSet、packaging 與 negative tests；本決策不授權第二個 reference 或擴充 stub。

K4 migration 應原子更新 descriptor contractVersion、runtimeAssembly／compilerReference identity、
canonical reference hash、SimpleQuantity auxiliary asset identity／hash、compatibility matrix、
package inventory 與 manifest provenance。K1 generated sources／manifests 仍保持 byte-for-byte 不變。
初始 Runtime assembly identity 對齊既有 `1.0.0.0`、PublicKeyToken=null；contractVersion 必須與 K0 不同，
不能只更改 DLL version。Descriptor 結構改變（含 auxiliary source asset 契約）時須審查 schema 升版；
只換 identity／hash 且結構不變時不升 schemaVersion。
實際新 contractVersion 值與 reference hash 在 K4 PR 固定並 review，不在尚未 build 時捏造。
13 個 model-generation symbols 保留；新增 accessor 是 compiler reference 的公開 SPI，
不因出現在 PE 就自動成為第 14 個 generator mapping symbol。

## 6. Compatibility 與舊 binary 決策

2026-10-07 使用者依建議確認本節相容性承諾、forwarder 範圍及舊 binary 驗證方法。
這是設計確認；K3 的執行證據仍須另行完成。

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

2026-10-07 使用者依建議確認本節 release 邊界及 rollback 方案；不授權公開發布。
此紀錄不宣稱已演練 rollback，K7 仍須提供演練證據；release 責任角色見第 9 節。

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
| ADR §3.2、Guide §4 | 已於 2026-10-07 同步：FhirSdkException 留 SDK，加入 accessor 與完整 seam ownership 依據 |
| ADR §3.5、Guide §5.1 | 已同步第 3 節 SPI shape／behavior，釐清穩定契約與可寫 instance value |
| ADR §3.6、Guide §5.2／K1 | 已於 2026-10-07 同步：保留 SDK partial composition；K1 驗證隔離邊界，不要求消除內部 partial |
| ADR §3.7／§8、Guide K1／K2／K4／K6 | 已於 2026-10-07 同步：單 reference、SimpleQuantity auxiliary source、CLI／SDK build 驗證責任及移除 production friend access；K1 本機證據見第 10 節及 Guide，K2／K4／K6 待完成 |
| ADR §3.4、Guide §8／K3 | 已於 2026-10-07 同步：SPI public API 例外、K0／K1 consumers 與 moved-public-type forwarder 集合 |
| ADR §3.8／§7 | 已於 2026-10-07 同步：release boundary 與可 review 的 source rollback |

上述設計修訂已同步；原始核准來自第 9 節，新增 amendment 核准來自第 10 節的使用者明確授權，
不是從文件合併或 CI 通過推定。

## 9. Acceptance gate 核准紀錄

核准人為本專案使用者（本對話），兼任 Architecture、Runtime、CodeGen、Compatibility、
Package／Release；日期為 2026-10-07。核准來源為本對話的明確聲明，不另推定姓名或 GitHub approval。
SDK maintainers 的 ownership 責任在本階段由同一位使用者以 Runtime 角色承接。
審查基準為 commit `5e198f14d977fe331b9d381de25492ff85c0c950` 的 ADR v0.2、
本文件 v0.1 與 Guide v0.3。v0.2 僅收錄當時核准與同步狀態；v0.3 的新增 amendment
核准另記第 10 節，不回填為原始核准已涵蓋。

核准原文：

> 本專案的 Architecture、Runtime、CodeGen、Compatibility 與 Package／Release 角色均由我負責。我核准目前 ADR 及已確認的 acceptance decisions，同意將 ADR 標為 Accepted，允許開始 K1；後續實作仍依各工作包 gates 驗收

| Gate | 決策與證據 | 必要 review role | 具名核准人 | 結果／日期／review link |
| --- | --- | --- | --- | --- |
| K0 baseline／inventory | 第 1 節、PR #40、K0 pin／inventories | Runtime + Architecture | 使用者（兼任左列角色） | Accepted，2026-10-07，本節核准聲明；CI 證據範圍依第 1 節 |
| 唯一 ownership | 第 2 節 | Runtime + Architecture | 使用者（兼任左列角色） | Accepted，2026-10-07；K2 compile／PE 驗收待完成 |
| Accessor／registry seams | 第 3、4 節 | Runtime + CodeGen + Compatibility | 使用者（兼任左列角色） | Accepted，2026-10-07；剩餘 K1 實作 gates 另驗收 |
| Facade／forwarding policy | 第 6 節 | Compatibility + Runtime | 使用者（兼任左列角色） | Accepted，2026-10-07；K3 驗收待完成 |
| Old binary fixture／驗證方法 | 第 1、6 節 | Runtime + Compatibility | 使用者（兼任左列角色） | Accepted，2026-10-07；K3 驗收待完成 |
| Descriptor/reference migration／rollback | 第 5、7 節 | CodeGen + Runtime | 使用者（兼任左列角色） | Accepted，2026-10-07；K1／K2／K4／K7 實作證據待完成 |
| Package／release boundary | 第 7 節 | Package/release owner | 使用者（兼任左列角色） | Accepted，2026-10-07；公開發布未授權 |
| ADR 整體核准 | 全部 gates、第 8 節文件已同步 | Architecture + Runtime maintainers | 使用者（兼任左列角色） | Accepted，2026-10-07；允許開始 K1 |

本次已同步 ADR status、核准日期／責任人與 Guide entry criteria；沒有尚待選定的 K1 entry 設計決策。
K1–K7 的實作測試留在各工作包驗收，不回填成已在 acceptance review 執行。

## 10. K1 validation amendment 核准紀錄

- 日期：2026-10-07。
- 核准人：本專案使用者，依第 9 節既有角色紀錄兼任 Architecture、Runtime、CodeGen、Compatibility。
- 核准來源：本對話原文「請依照上述建議修改」。所指建議為保留單一 Runtime reference，
  models／wrappers 以此次生成 sources 與封裝的真實 SimpleQuantity.cs 做 Roslyn compilation，
  SDK-owned metadata／validation composition 語意編譯由真實 SDK build／CI 負責，
  移除 production friend access 並補足真實 contract drift tests。
- 同步位置：ADR v0.4 §3.7／§8、本文件 v0.3 §5、Guide v0.5 K1／K4／K6。
- 核准結果：Accepted（設計）；解除先前等待 amendment 的設計阻礙，允許依此繼續 K1。
  K1 exit gate、Runtime-only canonical reference、K2／K4 的實作驗收仍待完成。

本次採較小修訂，不採先前 ADR §8 的整套 SDK sources 封裝方向，也不新增 metadata reference。
CLI success 的保證限於 models／wrappers 語意編譯及 composition 局部／結構檢查；
完整生成成功的驗收保證來自此次全部生成產物的真實 SDK build／CI 與 runtime regression。
此授權不擴大 ownership／public API／公開 release 範圍，不替代後續各工作包的執行證據。

2026-10-07 K1 實作紀錄：production pipeline、embedded 真實 auxiliary source、fresh generated
SDK integration 與真實 metadata／validation／registry drift tests 已完成；本機 solution
857 passed、1 external-service test skipped、0 failed。詳見 [Guide K1 證據](MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md#k1-實作進度與-reference-surface-證據2026-10-07)。
部署 SDK 已移除 generator friend。為固定 K1 manifest／reference bytes，歷史 compiler-only 資產
仍保留舊 friend metadata，由條件 build 產出且不部署 implementation；CodeGen 已不用該 friend name。
此過渡機制及 auxiliary descriptor／provenance 於 K4 原子遷移；不宣稱 K2／K4／跨平台驗收已完成。

2026-10-07 K2 實作紀錄：production kernel 的 14 個 declarations 已由 `MyFhirSdk.Runtime`
獨占編譯，SDK 單向依賴 Runtime；evaluated MSBuild items、compiled PE 與未修改的 public
snapshots 驗證通過。Clean 後 solution 860 passed、1 skipped、0 failed；詳見 Guide K2。
歷史 compiler-only reference／descriptor／manifest 保持不變，host TPA 不補足部署 SDK／Runtime。
K3 forwarders 與舊 binary 驗收尚未完成；K2／K3 必須在相容性 gates 完成後再合併 main。
