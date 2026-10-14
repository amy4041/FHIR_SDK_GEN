# MyFhirSdk FHIR R4 support implementation guide

Version 0.1 — 2026-10-13

狀態：V0 已提交並由使用者確認 CI 通過；V1 selected closure 實驗已建立，實測狀態見 [V1 baseline](baselines/v1/README.md)；V2–V7 Planned。依據 [ADR](MyFhirSdk_FHIR_R4_Support_ADR.md)，
驗收採 [Acceptance Criteria](MyFhirSdk_FHIR_R4_Support_Acceptance_Criteria.md)。
V 編號表示多版本工作包，與已交付的 Runtime extraction K0–K7 分開。

## 1. 工作順序

```text
V0 baseline / decisions
  → V1 bounded R4/R5 experiment
  → V2 provider/context contracts + engines injection
  → V3 R5 Models extraction + consumer migration
  → V4 shared Generator version configuration
  → V5 full R4 models + runtime integration
  → V6 R4 Client integration
  → V7 cross-platform clean delivery + handoff
```

V1 允許隔離的實驗修改，不先承諾完整 production SPI。V4 的盤點可提前進行，正式生成產物
應使用 V2／V3 已固定的 ownership。V3 每次搬移後保持 R5 可建置可執行；不能先留下壞掉的 SDK
再等待 R4 完成。依使用者決定，V0／V1 先在同一 feature branch 以獨立 commits 接續，暫不合併 main；
保留 main 與已驗證 V0 的完整 revision 作為隔離 checkout／重建還原點，各工作包仍需 review 與 gates。

## 2. 工作包

### V0 — Baseline 與設計決策

- 固定完整 source revision、現有 tool／descriptor／policy／reference hashes、R5 API 與生成 inventories。
- 執行現有 R5 regression，記錄實際 passed／failed／skipped 與 TRX；外部 Client smoke 的設定與未執行原因另記。
- 盤點所有 consumers、相依 DLL、reflection／AQN 設定、project Compile 項目及 R5 default entry points。
- 準備官方 `hl7.fhir.r4.core#4.0.1` input，驗證 package metadata 與 SHA-256；記錄取得來源與儲存政策。
- 收斂 ADR §5 的 namespace、Models assembly、facade、SPI owner、能力範圍與生成檔提交方案。

交付：baseline pin／inventory、決策紀錄、R4 package lock 與 capability inventory。
R4 檔案數與規則涵蓋率由實際 definitions 盤點，不套用 R5 的 831／21／158 數字。

### V1 — 小範圍雙版本實驗

選用 `Patient`、`Observation`、`Attachment`，加入所需完整 dependency closure（含 primitives、
backbone、metadata、Extension／Meta／Narrative）。selected closure 不代表完整 R4 支援。

- 在隔離目錄以明確版本 namespace／output 生成或建構實驗 models，記錄暫時修改與限制。
- 同一程序建立各版本 context，測試 parse／serialize／validate 與 resource factory。
- 對照 Runtime bootstrap；測試 `Meta.Tag`／Security、Extension value、contained resource 與 choice。
- 使用實際版本差異確認不是同一套 model 換名稱：R4 `Attachment.size` 為 `unsignedInt`，R5 為 `integer64`。
- 測試跨版本 resource／datatype／primitive 混用的拒絕行為及並行操作。

交付：實驗報告、支持或修訂共用 Runtime 的結論、最小必要 SPI 清單、完整生成前的阻塞項目。
若失敗顯示需要改 bootstrap shape，先修訂 ADR／compiler contract，再推進 V2。

版本差異來源：[R4 datatype definitions](https://hl7.org/fhir/R4/datatypes-definitions.html)、
[R5 datatype definitions](https://hl7.org/fhir/R5/datatypes-definitions.html)。

### V2 — Provider／context 契約與 engines 注入

- 將 metadata／factory、primitive definitions、validation rules 與能力清單的必要契約放入中立 owner。
- 盤點 public SPI signature 的所有參數／回傳型別，不留下 public 介面依賴 internal 型別。
- 明確拆分版本 provider 與通用 executor；Models provider 不依賴 engines／SDK，避免新 cycle。
- 新增不可變版本 context 與一致性檢查；不同 package／policy／models 的混合組合明確失敗。
- Parser／Serializer／Validator 使用指定 context；cache 包含 context／型別身分，無全域 current version。
- 保留既有 R5 default constructor 行為，由 facade 或 composition root 組裝；新增 R4 入口明確指定版本。
- 驗證巢狀跨版本物件的拒絕；shared bootstrap 根物件則按 context 檢查其內容與規則。

交付：SPI／API snapshots、ownership、dependency tests、R5 regression 與 context 正向／負面測試。
如需 engines assembly，三個 engines 可共用一個專案；不以多拆 DLL 作為驗收目標。

### V3 — R5 Models 分離與重編遷移

- 將 R5 generated primitives／datatypes／resources、必要 constraint declarations 與版本 composition
  編譯到獨立 Models assembly，維持單一 compile owner。
- 拆開共用 codecs／executor 與版本 registry composition；internal partial 不能跨 assembly 任意延伸。
- 對照 API、base／interface graph、namespace、JSON、factory、choice 與 validation snapshots。
- SDK facade 組裝 R5 provider 與 engines；處理 root project 的預設 recursive Compile，避免再次編譯新專案 sources。
- 全面重編所有 current consumers／相依 DLL，更新 assembly-name-based 設定，獨立部署測試。

交付：R5 Models project、dependency／API／defining assembly evidence、consumer migration／rollback
與完整 R5 regression。尚無公開 consumers 的既有全面重編策略繼續適用，不新增 forwarders。

### V4 — 共用 Generator 多版本化

- 盤點 package／policy loaders、IR、CLR naming、artifact paths、renderers、composition validation、
  CLI defaults、manifests、descriptor／matrix 與 fixture helpers 的所有 R5 固定假設。
- 建立明確 release configuration；沿用既有 package／FHIR version 選項或正式遷移它們，
  不在文件先承諾未實作的 CLI option。
- R4／R5 policies 分開，確定 mapping／ownership／choice／backbone／constraint／validation capability。
- 同一輸入批次的 version、policy、namespace、destination、auxiliary asset、compiler contract 要一致。
- 現行 R5 用法保留；若 V3 改變合法生成內容，單獨記錄受控 diff 與新 baseline，不聲稱 bytes 完全不變。
- schema／tool／policy 的遷移依契約實際變更版本化。改 Runtime public contract 時原子更新 canonical reference 與 consumers。
- 從 installed tool 在 checkout 外生成；使用明確 packaged compiler reference／auxiliary sources，不偷用 SDK implementation。

交付：一套雙版本工具、固定 policies／locks、完整 compatibility table、CLI migration notes、
determinism／invalid combination／output 保留測試。生成驗證仍分 model compile、composition
structure checks 與 fresh SDK runtime integration 三層。

### V5 — 完整 R4 Models 與 runtime integration

- 從固定官方 input 生成已核准 scope 的 primitives／datatypes／resources，建立 R4 Models assembly／provider。
- 盤點全部 definitions：generated、Runtime bootstrap、constraint auxiliary、excluded／unsupported 各有理由；禁止無聲遺漏。
- 實際建置這次生成的 metadata／factory／registry／rules composition，再執行 engines regression。
- 版本 primitive lexical／JSON 規則、primitive metadata array alignment、choice、open type、Extension、
  contained resources、unknown resource、required／cardinality 及已執行 validation rules 都要有案例。
- 同程序、並行 context 與跨版本誤用測試；同名 resource 使用自己的 factory。
- R4 validation capability inventory 與 API／source snapshots 固定；deferred 規則不能宣稱已執行。

交付：完整 scope inventories、fresh generated R4 integration、R4／R5 同程序 consumer 與 R5 regression。
完成此工作包代表基礎 R4 SDK 的功能整合通過；正式交付另需 V7 的對應驗證，
尚未宣稱 R4 Client／IG 或完整 FHIR validation。

### V6 — R4 Client integration

- 處理 Search 回傳 `Bundle`、OperationOutcome、resolver、default engines／validator 的版本相依。
- 固定 source API 選擇（版本 facade 或型別化 context 等），維持既有 R5 Client 行為並更新 migration notes。
- 每個 Client instance 的版本固定，不把 server 回應用另一版本的 parser 解讀。
- 以受控 HTTP handler 驗證 CRUD／Search、headers、Bundle entries、error response 與 context wiring。
- 設定明確 R4／R5 endpoint 執行外部 server smoke，記錄 server identity／version；未設定則明列 skipped。

交付：R4 Client API／HTTP tests、R5 Client regression、外部服務證據或尚未驗收項目。
Client physical package 拆分可延後；但以上版本耦合不能延後到宣稱 R4 Client 支援之後。

### V7 — 跨平台交付與 handoff

- Windows／Ubuntu 安裝同一 canonical tool package，從 checkout 外以乾淨 cache 生成兩個版本。
- 同一版本重複生成與跨平台比較 hashes；R4、R5 不要求互相 bytes 相等。
- 在乾淨 source tree 編譯本次輸出，執行各版本與同程序 tests，核對 assembly、測試數與 skipped 清單。
- 獨立部署 Runtime、Models、必要 contracts／engines／facade 與重編 consumers，驗證 missing dependency。
- 演練 source rollback；若重用舊證據，綁定 source fingerprint、input／tool hashes 與測試設定。
- 文件同步實際 tool／schema／SPI／namespace／assembly／能力清單，標示已驗收的交付層級。

交付：portable evidence summaries、CI artifacts、migration／rollback instructions、handoff register。
V6 外部 smoke 未執行時，V7 可交付基礎 SDK，但不能標示 R4 Client 外部整合驗收通過。

## 3. Handoff register 對應

| 既有後續項目 | 本次安排 |
| --- | --- |
| 新 FHIR / TFM | V0、V4、V5、V7 新增 R4；TFM 維持目前設定 |
| Models physical assembly/package | V3 建立 R5 邊界，V5 新增 R4；公開 package promotion 另案 |
| Engines split | V2 必做 provider/context 與 dependency 解耦；physical assembly 按需要 |
| Client physical package | V6 解決版本相依；完整 package 分離可延後 |
| IG / TW Core | 另案，不直接把 R5 profile rules 套到 R4 |
| Constraint Profile / SimpleQuantity | V0／V4／V5 處理必要版本 asset；通用生成另案 |
| Directory input 退場／packaged policy default | 維持既有 CLI 契約，另案 |
| Deferred validation capabilities | 列入各版本能力清單；新 executor 另案 |
| 公開 NuGet release | 基礎交付只做可驗證的本機／CI packages，公開發布另案 |

## 4. Evidence 與變更管理

建議可提交證據位置為 `docs/fhir_versions/baselines/vN/`，實際執行產物放 `artifacts/fhir-versions/vN/`。
V0 runner 為 `eng/Export-FhirVersionBaseline.py`，使用 Python 3.10+ 標準庫並共用既有 Inventory／Pack／regression。
新腳本延續 `eng/` 維護，能共用現有 harness 時先共用。
Portable summary 記錄 source／tool／input／policy／contract identities、環境、每個 gate 的狀態與 artifact
位置；不提交 machine absolute paths、DLL、完整 TRX 或快取。

每個工作包進入下一階段前完成 code review 與對應驗證；文件-only 變更檢查連結與 diff，
production 移動／生成變更則執行該工作包的實際 regression。新功能完成後才更新 current baseline，
K0–K7 歷史 pins 與 source rollback 證據保持原意，不能拿雙版本 SDK 結果覆寫歷史單版本基準。
