# MyFhirSdk FHIR R4 support acceptance criteria

Version 0.1 — 2026-10-13

狀態：V0–V4 已提交並由使用者確認 CI 通過；V5 本機驗證見 [G5 evidence](baselines/v5/README.md)，新 CI 待提交；G6–G7 Pending。設計依據為
[ADR](MyFhirSdk_FHIR_R4_Support_ADR.md)，執行順序見
[Implementation Guide](MyFhirSdk_FHIR_R4_Support_Implementation_Guide.md)。

Gate 指進入下一階段或宣稱某項能力前必須滿足的驗收條件。CI 成功、CLI 能生成、單一範例成功
各有不同證據範圍，不能互相替代。

## 1. 階段 gates

| Gate | 工作包 | 必要證據 | 狀態 |
| --- | --- | --- | --- |
| G0 Baseline／decisions | V0 | 固定 source、R5 regression／API、R4 input lock、ownership／namespace／scope 決策 | 見 [V0 evidence](baselines/v0/summary.json) |
| G1 Runtime feasibility | V1 | Selected closure、bootstrap 差異報告、R4／R5 同程序與版本差異案例、跨版本誤用 | 見 [V1 evidence](baselines/v1/summary.json) |
| G2 Context／SPI | V2 | 最小公開契約、signature dependencies、版本一致性、default R5 行為、cache／並行隔離 | 見 [V2 evidence](baselines/v2/summary.json)；使用者確認 V2 CI 通過 |
| G3 R5 migration | V3 | 單一 compile owner、無 cycle、API／AQN diff、全面重編／部署、完整 R5 regression | 見 [V3 evidence](baselines/v3/summary.json)；使用者確認 V3 CI 通過 |
| G4 Generator | V4 | 精確 compatibility、policy／manifest、installed tool、determinism、無 implementation fallback、失敗不改輸出 | 見 [V4 evidence](baselines/v4/README.md)；使用者確認 V4 CI 通過 |
| G5 R4 SDK | V5 | Full approved scope inventory、fresh compilation／runtime tests、能力清單、同程序 consumer、R5 regression | 見 [V5 evidence](baselines/v5/README.md)；新 CI 待提交 |
| G6 R4 Client | V6 | 版本 API、受控 HTTP、R5 Client regression、明確版本 server smoke 與 skipped 清單 | Pending |
| G7 Delivery | V7 | Windows／Ubuntu clean generation／integration、deploy／missing dependency、rollback／fingerprint、handoff | Pending |

設計尚未收斂或實測顯示不相容時，記錄缺口與必要修訂，不把 Pending 改成 Passed。

## 2. 必要行為驗證

| 領域 | 正向案例 | 負面／隔離案例 |
| --- | --- | --- |
| Package／compatibility | 各自固定 R4 `4.0.1`／R5 `5.0.0` 身分與 hashes | 錯誤 package version／FHIR version／policy／hash／contract；R4B 與未列版本拒絕 |
| Generation destination | 正確 namespace、artifact paths、composition 與 compile owner | R4 input 配 R5 policy／destination、auxiliary 不符；寫檔前失敗並保留 sentinel output |
| Factories | 同名 `Patient` 在兩個 context 建立各自 CLR 型別；全部 scope 有 factory inventory | Unknown／abstract resource、另一版本 CLR type，禁止 scan fallback |
| Primitive | 各版本支援集合、lexical／JSON 及 metadata；array null/null slot 保持對齊 | 不支援的 primitive、另一版本 wrapper、錯誤 lexical value、metadata index 不被壓縮 |
| 實際版本差異 | R4 `Attachment.size` unsignedInt 與 R5 integer64，各自 CLR／JSON 規則 | 不把 R5 專有型別或規則當成 R4 支援；非法值依各版本規則拒絕 |
| JSON／choice | Round-trip、backbone、`value[x]`、open types、Extension、contained resource | 跨版本 root／nested resource／datatype、非法 choice、unknown JSON type；檢查不能只涵蓋 root |
| Bootstrap | Meta／Narrative／Extension 在每個 context 下成功解析與序列化 | Meta.Tag／Security 放入另一版本 Coding、Extension 裝入另一版本值，明確 diagnostic／failure |
| Context／caches | 同程序並行與交錯使用，重複相同 FHIR 名稱不串用 factory／rules | 混合不同版本 providers；一個 context 初始化或執行不改變另一個 context |
| Validation | 各版本已實作 required／cardinality／primitive／choice 等規則正負案例 | 另一版本規則不會被使用；未實作 terminology／FHIRPath 等明確列為 deferred |
| R5 API／defaults | 舊 source consumer 重編後的 default constructors／Client／JSON 行為 | Assembly identity／設定字串變更有 migration；任何破壞性 diff 不被 snapshot 自動接受 |
| Client | 各版本 CRUD／Search、Bundle entries、OperationOutcome、HTTP 與 engines wiring | 回應不能以錯誤版本 context 解讀；錯誤回應與版本不符的 model 有明確處理 |

負面案例固定實際 diagnostic／例外類別及失敗後狀態，V2／V4 決定契約後加入 snapshot；
不在這份規劃先發明目前不存在的 error code。跨版本 JSON 字串可能同時合法，不能承諾只看內容
就能偵測所有錯誤版本；版本來自明確 context。上述跨版本拒絕主要針對已知 CLR 型別與 configuration。

## 3. 三層生成與整合驗證

1. **Generated model compile**：以核准 canonical compiler reference、當次生成的 primitives 及版本相符 auxiliary sources 編譯 models。
2. **Composition validation**：對 metadata／factory／registry／validation composition 做 IR／mapping／結構一致性檢查。
3. **Fresh integration**：乾淨 source tree 實際編譯所有當次生成來源與 production contracts／engines，再執行 runtime／consumer tests。

第 1／2 層成功不等同第 3 層成功。CLI 不需為此重新依賴完整 SDK implementation；
完整整合由 build／CI 負責。各層證據均記錄 package／policy／tool／reference hashes 與 scope。

## 4. 部署、跨平台與 rollback

- 同一 canonical tool package 在 Windows／Ubuntu 從 checkout 外安裝；乾淨工作目錄與快取不能隱含提供缺漏依賴。
- 各版本兩次生成與跨平台 artifact hashes 相符；比較同一版本／同一契約，不比較不同平台 implementation DLL bytes。
- 使用本次輸出編譯 R4、R5 與雙版本 consumer，核對預期 test assemblies／tests，禁止只看到 test command exit 0 就通過。
- 所有搬移型別的 consumers／相依 DLL 全面重編；部署包含 implementation DLL、deps／runtimeconfig 與實際必要依賴。
- 移除 Runtime、必要 Models／contracts DLL 的負面部署案例在隔離目錄執行；正常部署不從 repo／cache 補載。
- Source rollback 固定最後驗收的 revision／input／tool，從還原來源重建，分別記錄 current 與 restored regression。
- 重用 TRX／consumer 或 baseline 證據時檢查 source fingerprint、hash、測試設定與預期輸出，防止舊證據誤用。

外部 server smoke 必須記錄 endpoint 的 FHIR 版本、環境與執行結果；summary 不含 credentials。
未設定 endpoint 只能記錄 skipped，受控 HTTP tests 不替代外部 server 證據。

## 5. 完成定義與文件狀態

| 宣稱 | 必須完成 |
| --- | --- |
| R4 experiment 可行 | G0／G1；只能涵蓋 selected closure |
| 基礎 R4 SDK 支援 | G0–G5、G7 中對應基礎 SDK 的交付驗證，公開能力清單與 migration notes |
| R4 Client 支援 | 基礎 SDK，加 G6／G7 Client 對應驗收；外部服務未驗證需明列限制 |
| R4 IG／TW Core 支援 | 指定 package／version／FHIR base 的另案驗收，不能由以上 gates 推論 |
| 公開 NuGet release | 另案 release policy／license／signing／provenance／feed／promotion 與 rollback 驗收 |

提交 portable summary 時記錄完整 source revision、tool／policy／package／contract identities、
作業系統／SDK／TFM、gate 狀態、實際 test counts、skips、證據位置與已知缺口。
不得直接沿用 K0–K7 的 counts／hashes 當成 R4 成果；歷史 baseline 保持不變。

## 6. 規格依據

- [R4 official downloads](https://hl7.org/fhir/R4/downloads.html)：官方版本及 package identity。
- [R4 Attachment 定義](https://hl7.org/fhir/R4/datatypes-definitions.html#Attachment.size) 與
  [R5 Attachment 定義](https://hl7.org/fhir/R5/datatypes-definitions.html#Attachment.size)：版本差異案例。

以上規格來源支持版本與 datatype 差異；本文件的工作順序、project 配置與 gates 是 MyFhirSdk 的設計方案。
