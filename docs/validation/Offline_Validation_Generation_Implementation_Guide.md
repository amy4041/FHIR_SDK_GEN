# 不需外部服務的驗證規則產生：實作指引

整理日期：2026-11-02。

本文是依本專案現有架構撰寫的後續實作指引，涵蓋規則產生、執行、覆蓋報告與驗收。本文中的新增類型、規則名稱及報告格式均為設計建議，並非已存在的 API；建立本文件不代表已實作相關功能。

規則分類與來源概觀請先參閱 [FHIR 物件驗證規則與來源](FHIR_Validation_Rules_and_Sources.md)。

## 1. 目標與依賴邊界

目標：依據建置時已取得、版本固定的 FHIR／IG 定義，產生可在本地執行的規則；驗證期間不下載定義、不連線術語服務、不查詢外部 Resource 或業務資料庫。

| 項目 | 本指引的處理方式 |
| --- | --- |
| 本地 FHIR／IG 套件與相依套件 | 允許；固定版本並記錄來源。 |
| 既有 SDK 與 .NET 功能 | 允許；第一批不要求增加第三方套件。 |
| 內嵌 metadata、代碼表與本地 resolver | 可以使用，但必須有明確的完整度與能力宣告。 |
| 本地 FHIRPath 程式庫 | 可在後續階段評估；不需要網路不等於禁止程式庫依賴。 |
| 遠端術語、Profile 或 Resource 解析 | 本階段不依賴，也不自動啟用。 |
| 系統權限、重複病人、資料庫唯一性 | 留在業務或服務層。 |

若未來另有「完全不得新增第三方套件」要求，FHIRPath 引擎的選擇與成本需單獨評估，不將自行實作完整語言當成第一批工作的前提。

離線可執行不代表可涵蓋全部 FHIR 相容性；結果應說明套用了哪些定義、規則與能力。

## 2. 現況與設計限制

### 2.1 目前的產生管線

```text
本地套件／StructureDefinition
  → DTO 與模型 IR
  → ModelMetadataIrBuilder
  → ValidationRuleMetadataIr
  → ValidationCompositionRenderer
  → Generated/R4 或 Generated/R5 的規則登錄
  → 模型提供者的 ValidationProviderDefinition
  → FhirModelContext 規則 adapter
  → FhirValidator
```

目前 [ValidationRuleKind](../../CodeGen/Metadata/ModelMetadataIrBatch.cs) 只有四種：

| 種類 | 執行方式 |
| --- | --- |
| RequiredScalar | 一般欄位必填。 |
| RequiredCollection | 集合至少包含一個非 null 項目。 |
| ChoiceAtMostOne | 一般 choice 最多一個選項有值。 |
| ChoiceExactlyOne | 一般 choice 恰好一個選項有值。 |

Primitive 格式與集合 null 結構檢查已有全域執行機制，不必為每個欄位重複產生相同規則。

### 2.2 必須一起處理的現有政策

- [R5 validation capability policy](../../CodeGen/Policy/r5-validation-capability-policy.json)。
- [R4 validation capability policy](../../CodeGen/Policy/R4/r4-validation-capability-policy.json)。
- [R5ValidationCapabilityPolicyTests](../../Tests/CodeGen/Policy/R5ValidationCapabilityPolicyTests.cs)。

現有政策以 specialization 模型產生為範圍，排除 constraint Profile 的通用產生；fixed／pattern 等能力也有明確的未支援處置。開啟新能力時，須在同一變更中更新政策、診斷、實作與測試，不能只修改 renderer。

[ModelIrBuilder.CreateValidation](../../CodeGen/Ir/ModelIrBuilder.cs) 目前會對 specialization 範圍內出現 fixed／pattern 產生未核准形狀診斷。若要支援，須在能力檢查完成後，以明確政策替換這項限制，不能直接刪除診斷後忽略未支援的內容。

### 2.3 維持既有組件邊界

- 共用規則演算法優先放在 ModelSupport，使用 ModelContracts 的中立契約。
- 模型提供者負責版本與型別對應、selector 及規則登錄。
- SDK Validation 負責巡覽、適用規則組合與回傳問題。
- 不在共用引擎新增具體 R4／R5 Resource 型別分支。
- 不在每個產生模型類別內加入 Validate 方法。
- 不讓 Runtime／ModelSupport 反向依賴 CodeGen 或具體模型組件。

## 3. 分階段交付範圍

| 階段 | 內容 | 完成判準 |
| --- | --- | --- |
| P0：盤點與覆蓋報告 | 規則候選盤點、政策能力、來源及未支援原因。 | 每個範圍內的候選都有明確處置，可重現輸出。 |
| P1：第一批規則 | 完整 cardinality、既有 choice 整合、maxLength、明確支援型別的 primitive fixed。 | 本地產生及執行，不需新外部服務；邊界案例通過。 |
| P2：元素比較 | 支援型別的 min/max value、複合 fixed、pattern。 | 有依 FHIR 型別實作的比較器，未支援型別不默認通過。 |
| P3：本地 invariants | 有限且明確的 FHIRPath 能力，或可追溯的特定 invariant 實作。 | 不宣稱完整 FHIRPath；能區分 false、語意上的空結果及執行失敗。 |
| P4：本地 Profile | Profile 定義載入、適用範圍與繼承、有限 slicing。 | Profile 規則不污染基本驗證；每種支援的 discriminator 有驗收案例。 |
| P5：本地術語及參照 | 可完整判定的本地 ValueSet／CodeSystem、contained／Bundle resolver。 | 無法確認時回報未完成，而非當成代碼無效或目標不存在。 |

P2 以後可各自拆分，不要求全部完成才交付 P1。若正式基礎套件沒有 finite max、fixed 或 maxLength 等案例，仍需使用合成 fixture 驗證；不能以官方輸出未變動推定功能正確。

## 4. P0：先建立規則盤點與覆蓋報告

### 4.1 候選規則來源

從有效元素定義盤點 cardinality、choice、maxLength、fixed、pattern、值域、constraint、slicing、binding 與 targetProfile。有效定義包含基底繼承後的要求。

Profile 的 differential 不能直接視為完整規則集。後續 P4 必須使用可信任 snapshot，或實作受控的本地 snapshot／繼承處理；缺少基底時應產生定義診斷。

盤點還要涵蓋根元素上的 constraint，以及外部／共用型別。只遍歷一般 member，可能遺漏 Resource 根節點、Element、Extension 等規則。

### 4.2 報告狀態

| 狀態（建議） | 意義 |
| --- | --- |
| generated | 已產生可執行規則。 |
| covered-by-runtime | 由全域檢查或其他既有規則執行，附執行依據。 |
| covered-by-model-shape | 有型別形狀保證，僅適用物件層；不宣稱原始 JSON／XML 也已驗證。 |
| unsupported | 屬於定義範圍，但目前無法執行。 |
| requires-context | 需要根資源、Bundle、術語或參照等尚未提供的情境。 |
| out-of-scope | 未納入本次產生範圍，例如尚未啟用的 Profile。 |
| invalid-definition | 定義缺損、互相衝突或無法解析，應中止該輸出。 |

報告至少記錄套件 ID／版本、FHIR 版本、StructureDefinition canonical／版本、Element ID、規則種類／原始 constraint key、狀態、原因及實作依據。規則來源須可追溯，不能只記 CLR 類別名稱。

建議新增 JSON 報告產物，由既有 generation pipeline 與 writer 統一輸出。排序使用 ordinal，排除時間戳等會破壞相同輸入產生相同位元組的欄位。

政策中已宣告必須支援的規則若變成 unsupported，應在 render 前失敗；允許延後的規則可以保留並回報，不以「存在未支援規則」為由一律禁止所有產生。

## 5. P1：資料模型與產生管線修改

### 5.1 DTO 與模型 IR

檢查 [ElementDefinitionDto](../../CodeGen/Definitions/ElementDefinitionDto.cs)、[ModelValidationMetadataIr](../../CodeGen/Ir/ModelValidationMetadataIr.cs) 及其 builder：

1. min/max 已有 [CardinalityModel](../../CodeGen/Models/CardinalityModel.cs)，須保留實際上下限，不能只使用 IsRequired／IsCollection。
2. maxLength、minValue、maxValue 等目前沒有完整的專屬 IR 欄位，應補上型別化讀取及定義驗證。
3. fixed／pattern 雖有 raw JSON 保存，仍須驗證型別名稱、值與目標型別是否相容。
4. 區分「未指定」「繼承後的值」與明確設定為零，不以預設零取代未知資訊。
5. 盤點未映射的規則相關欄位；DTO 的 AdditionalProperties 不是完整能力宣告，也不能把未讀取視為不適用。

### 5.2 規則 IR

現有 ValidationRuleMetadataIr 只有種類、Element ID、路徑與屬性名稱，不足以表達上下限與固定值。建議使用專屬的 payload，例如：

```text
RuleOrigin
  套件／FHIR 版本／canonical／Element ID／constraint key

CardinalityRuleSpec
  origin, selector, min, max（null 表示 *）

MaxLengthRuleSpec
  origin, selector, maxLength, primitiveType

PrimitiveFixedRuleSpec
  origin, selector, fhirType, typedExpectedValue
```

以上是概念模型，不是既有 API。不要將所有參數塞入通用字串陣列，再交由 runtime 重新猜測型別。對 expected value 建立不可變表示，避免生成規則共用可變模型實例。

修改 [ModelMetadataIrBuilder](../../CodeGen/Metadata/ModelMetadataIrBuilder.cs) 的一般模型與 external metadata 兩條路徑，避免僅擴充其中一條。同步處理 ordinary choice 與 single polymorphic property 的 open type。

### 5.3 Runtime 與模型組合

共用演算法放在 [ModelRules](../../ModelSupport/ModelRules.cs) 或同組件的新檔案。以既有 [ValidationRuleDefinition](../../ModelContracts/ValidationProviderDefinition.cs) 回傳中立的 ModelValidationIssue。

[Models/R4/ValidationComposition.cs](../../Models/R4/ValidationComposition.cs) 與 [Models/R5/ValidationComposition.cs](../../Models/R5/ValidationComposition.cs) 是目前生成規則使用的組合橋接，也必須同步擴充；只在 Validation/Rules 新增類別不足以完成模型提供者的規則產生與執行鏈。

[ValidationCompositionRenderer](../../CodeGen/Rendering/ValidationCompositionRenderer.cs) 只負責產生 selector、常數與規則登錄，演算法保留在共用層。所有字串及字面值應使用可靠的 C# literal 產生方式，測試換行、反斜線、引號與 Unicode；數值使用 invariant culture。

## 6. P1：各規則的執行語意

### 6.1 完整 cardinality

- 支援有限 min/max 及 `*`，驗證定義的上下限合法性。
- 一般 scalar 依元素存在與否計數；集合計算非 null 元素，null 項目由集合結構規則回報。
- 空字串或無內容物件的非法性，不應只靠 cardinality 檢查。
- primitive 的值、id、Extension 與元素存在性要分別建模；不要直接沿用目前 IsPresent 當成所有情境的正式語意。
- 有 Extension 但無 primitive value 的元素，是否符合要求應依完整定義判斷；值格式、元素內容與必要值限制各自檢查。
- 可選父物件不存在時，不執行其內部必填子欄位檢查。
- `max = 0` 仍須保留可判斷的禁止欄位 metadata；若模型形狀已移除欄位，原始輸入的違規須由解析層回報。
- scalar 的 CLR 形狀可限制物件層最大數量，但不能捕捉 JSON 重複屬性或 XML 重複元素。

遷移策略：同一元素在新規則接管後，停止產生等價 Required 規則，避免重複問題。若公開的 issue code 或 RuleId 改變，須有明確相容性決策及測試。

### 6.2 Choice

ordinary choice 要合併所有選項的存在次數；不可對每個選項各自套用必填。維持「基底允許型別、選項互斥、總 cardinality」三項要求。

open type 已是單一多型屬性時，仍需必要性與允許型別檢查，不另假設它具有多個平行屬性。Profile 的型別限縮直到 P4 才加入對應 scope。

### 6.3 maxLength

僅對規範適用且已支援的 primitive 型別產生規則。未出現的可選值不報長度問題；必要性由 cardinality 處理。

先確認各型別的長度語意，再實作計數；不要一律以 UTF-8 bytes 或 .NET string.Length 代替。測試中文、非 BMP 字元與型別特殊情況，未確認的型別列為 unsupported。

### 6.4 Primitive fixed

先支援具明確比較語意的型別，例如 boolean、integer、string、code、uri；實際啟用清單由政策宣告。

- 使用 FHIR 型別的值比較，不使用任意 ToString 或序列化字串相等。
- 數值、日期精度、時區與其他型別不能未經定義便套用同一比較器。
- fixed 不自行提高 min；元素缺少是否非法仍由有效 cardinality 判定。
- 固定值比較須處理元素內容的限制，包括是否允許額外 id／Extension；不能只檢查 primitive Value 就宣稱完整 fixed 支援。
- 若第一版尚未完成額外內容語意，不能將該型別標為 generated，應縮小支援範圍或延後。

## 7. P2：比較器擴充

### 7.1 minValue／maxValue

對每個型別分別實作可比較性，先從明確支援的數值型別開始。部分精度的日期時間、Quantity 單位換算等不可直接用 CLR 的日期或 decimal 比較取代。無法確定大小關係時，回報未完成的原因，不默認通過或失敗。

### 7.2 複合 fixed 與 pattern

建立依 FHIR 邏輯模型走訪的比較器，不將 JSON 屬性順序或 CLR 參考相等當成規範語意。

- fixed 與 pattern 使用不同比較策略。
- 明確處理額外子元素、id／Extension、重複元素與順序。
- pattern 的集合匹配測試包含重複項目，避免同一實例項目被錯誤重用以滿足多個要求。
- 只要尚有未支援的結構，就不能宣稱整個 pattern 已通過。

## 8. P3：本地 invariants

### 8.1 執行模式

可以選擇：使用本地 FHIRPath 引擎，或先提供少量可追溯到官方 constraint key 的特定演算法。後者必須明確標成特定規則支援，不能宣稱已實作通用 FHIRPath。

若採通用引擎，於產生／初始化階段解析並編譯 expression，以不可變且版本隔離的快取重用；不在每次 Validate 重新解析。

### 8.2 所需語意與情境

- 使用 FHIR 邏輯模型名稱：例如 `value`，而不是只辨識 JSON 的 valueString／valueQuantity。
- 區分 FHIR primitive 元素與其實際值，支援定義要求的集合語意。
- 依規則需求提供目前節點、根資源及 `%resource`／`%rootResource` 等情境。
- 保留 constraint key、human、severity、source 及實際節點路徑。
- 布林 false、空集合、型別錯誤、未支援函式與引擎例外須依規範及明確執行契約分別處理，不可一律轉成 true。
- `resolve()` 與術語相關函式不能僅依函式名稱就當作已支援；須確認是否有相應的本地能力。

目前 ModelValidationNode 只有 Value 與 Path，若新增根節點等情境，需先擴充中立契約及 adapter。不能為了執行運算式讓 ModelSupport 反向引用 SDK Validation。

## 9. P4／P5：Profile、slicing 與本地服務

### 9.1 Profile 適用範圍

通用演算法可以共用，但 Profile 規則只在該 Profile 被要求時執行。不可把所有 Profile 限制直接加入基底 Resource 的規則表，否則一般 Patient 也會被迫符合所有已載入 IG。

沿用 ProfileValidator 的組合概念，建立以 canonical、版本、資源型別為鍵的 Profile 規則來源；處理基底繼承與覆寫，再轉接為 Profile 規則。模型產生支援 specialization，不代表已能直接載入 constraint Profile，需另行實作此入口。

### 9.2 有限 slicing

ElementDefinition 定義 slicing 的資料欄位，Profiling 規範說明其使用與繼承行為，兩者共同決定實作。

第一批可限定 value discriminator、簡單支援路徑、可本地比較的 fixed／pattern，以及 open／closed 與 slice min/max。不得把尚未實作的 ordered、openAtEnd、profile discriminator、re-slicing 默認視為通過。

分類結果須能表示無匹配、唯一匹配及多重匹配，對不明確的匹配產生診斷或未完成結果。測試列表整體 cardinality 與各 slice cardinality，避免只檢查分類後的內容。

### 9.3 本地術語及參照

本地術語只在集合與代碼系統資料足以作決定時產生封閉判斷；資料不完整時，找不到代碼不等於已證明代碼不存在。Extensible 的概念適用性也不能完全以代碼集合包含關係取代。

本地參照可先支援 contained／Bundle，須採該情境的解析規則。缺少外部目標回報未完成，不自行發起 HTTP 請求。

## 10. 識別碼、繼承、能力與結果契約

### 10.1 規則識別與去重

建議以來源 canonical、版本、Element ID 與規則種類／constraint key 建立穩定規則識別。保留現有 RuleId 的相容性，必要時另建 provenance 對應，不無條件重寫所有 ID。

目前 FhirModelContext.RuleProvider 會沿 CLR 繼承鏈取得規則；若產生器也展開基底規則，可能重複執行。實作前確認每條規則實際登錄的 AppliesTo，選定一種清楚的繼承組合策略；同一 effective rule 只執行一次，Profile 的收緊限制則必須生效。

### 10.2 問題分類與能力宣告

若擴充 ModelIssueCode／ValidationIssueCode，須同步處理目前 adapter 以 enum 數值轉型的實作。建議改為顯式映射並測試未知值，避免 enum 次序變更後誤分類。

目前 ModelCapabilities 以少量 bool 表達能力，不足以描述部分 FHIRPath 或 slicing 支援。搭配詳細覆蓋報告，不因支援少數 invariants 就設定容易被解讀成完整支援的能力宣告。

### 10.3 執行結果

現有 Evaluate 只回傳問題，無法完整表示規則因缺乏情境而未執行。P1 可先只產生保證可本地執行的規則，以報告揭露未產生項目；進入 P3 前應定義 runtime 的 Passed／Failed／NotEvaluated 等狀態，或等價的結果契約。

ValidationResult.IsValid 目前是 Issues.Count == 0，不等於「沒有 Error」，也不代表完整驗證已完成。保留既有行為，若新增 HasErrors／IsComplete 等資訊，需明確文件化並驗證相容性。

## 11. 程式修改清單

| 位置 | 修改目的 |
| --- | --- |
| [ElementDefinitionDto.cs](../../CodeGen/Definitions/ElementDefinitionDto.cs) | 補齊規則欄位的讀取與型別化表示。 |
| [ModelIrBuilder.cs](../../CodeGen/Ir/ModelIrBuilder.cs) | 定義驗證、來源與能力診斷。 |
| [ModelValidationMetadataIr.cs](../../CodeGen/Ir/ModelValidationMetadataIr.cs) | 保存新限制與型別化值。 |
| [ModelMetadataIrBatch.cs](../../CodeGen/Metadata/ModelMetadataIrBatch.cs) | 新規則 IR 及必要參數。 |
| [ModelMetadataIrBuilder.cs](../../CodeGen/Metadata/ModelMetadataIrBuilder.cs) | 規則選擇、一般／external 路徑、覆蓋處置。 |
| [ValidationCompositionRenderer.cs](../../CodeGen/Rendering/ValidationCompositionRenderer.cs) | 新規則登錄及安全的 C# literal。 |
| [ModelMetadataGenerationPipeline.cs](../../CodeGen/Generation/ModelMetadataGenerationPipeline.cs) | 覆蓋報告與產生流程整合。 |
| [ModelRules.cs](../../ModelSupport/ModelRules.cs) | 共用規則演算法。 |
| [ValidationProviderDefinition.cs](../../ModelContracts/ValidationProviderDefinition.cs) | 必要的中立契約與 issue code。 |
| [R4 ValidationComposition.cs](../../Models/R4/ValidationComposition.cs)、[R5 ValidationComposition.cs](../../Models/R5/ValidationComposition.cs) | 模型組件中的新規則橋接。 |
| [FhirModelContext.cs](../../ModelMetadata/FhirModelContext.cs) | 適用規則組合、issue 映射及後續情境轉接。 |
| R4／R5 capability policy 與其測試 | 同步能力、範圍及預期產物。 |

Generated 下的檔案應由產生器重新建立，不直接手改作為功能實作。

## 12. 測試與驗收

### 12.1 必要測試矩陣

| 類別 | 至少涵蓋 |
| --- | --- |
| Cardinality | 0..0、0..1、1..1、2..5、1..*；少一項、等於邊界、多一項。 |
| 元素存在 | 缺值、空字串、空物件、Extension-only primitive、null item；確認各規則責任。 |
| Choice | 零、一、多個選項；ordinary choice 與 open type；禁止型別。 |
| maxLength | 小於／等於／大於上限，中文、非 BMP 字元及可選缺值。 |
| Fixed | 相符／不符、型別不同、可選缺少、額外 id／Extension、字面值 escaping。 |
| Pattern／值域 | 不同精度、無法比較、集合重複匹配與額外內容。 |
| Invariants | true／false／空結果、引擎錯誤、未支援函式、根情境。 |
| 繼承與 Profile | 基底只執行一次、衍生限制生效、未選取 Profile 不執行。 |
| R4／R5 | 各自產生、編譯與執行，跨版本 Context 拒絕不相容模型。 |
| 覆蓋報告 | 每候選有處置、無默默遺漏、穩定排序、相同輸入相同輸出。 |
| 並行使用 | 同一 provider 可重複且並行驗證，不共享可變狀態。 |

測試應包含合成定義的端到端流程：載入 → IR → 產生 C# → 編譯 → provider → FhirValidator → 檢查實際問題。Golden output 只能驗證產物，不足以證明執行語意。

可以使用可本地執行的官方測試案例或參考驗證器進行語意比對；記錄版本與設定，不將網路服務作為 CI 測試前提。

### 12.2 既有測試入口

- [CodeGen metadata tests](../../Tests/CodeGen/Metadata/ModelMetadataIrBuilderTests.cs)。
- [CodeGen generation tests](../../Tests/CodeGen/Generation/ModelMetadataGenerationPipelineTests.cs)。
- [Generated runtime tests](../../Tests/CodeGen/Runtime/ModelMetadataGeneratedRuntimeTests.cs)。
- [Validation rule tests](../../Tests/Validation/Rules)。
- [Profile tests](../../Tests/Validation/Profiles)。

以下為開發完成後的建議檢查命令，從儲存庫根目錄執行；本文件建立時未執行這些功能測試：

```powershell
dotnet test Tests/CodeGen/MyFhirSdk.CodeGen.Tests.csproj --no-restore
dotnet test Tests/Validation/MyFhirSdk.Validation.Tests.csproj --no-restore
dotnet test Tests/Architecture/MyFhirSdk.Architecture.Tests.csproj --no-restore
```

以上假設本地已有對應 SDK 與已還原的套件；未備齊時先完成建置環境準備。變更 Profile 或多版本契約時，再加入相關的既有測試專案。

### 12.3 P1 完成條件

- [ ] R4／R5 政策與支援型別清單已更新。
- [ ] 一般模型及 external metadata 路徑均可產生新規則。
- [ ] 完整 cardinality、choice、maxLength、核准的 primitive fixed 可正確執行。
- [ ] 欄位未出現與值不合法的問題不混淆，沒有等價規則重複回報。
- [ ] 來源、RuleId、問題路徑與 severity 可追溯。
- [ ] 未支援的內容會出現在覆蓋報告，政策要求的能力缺失會在 render 前失敗。
- [ ] 產物可編譯，規則在實際 SDK 組合中可達並執行。
- [ ] 離線執行不發起網路存取，也不修改模型內容。
- [ ] 測試矩陣中屬於 P1 的案例通過，並確認產生結果穩定。
- [ ] 對外說明只宣告實際支援範圍，不宣稱完整 FHIR／Profile 驗證。

## 13. 官方參考資料

- [FHIR R5 Validation](https://www.hl7.org/fhir/validation.html)：驗證範圍與能力限制。
- [FHIR R4 Validation](https://www.hl7.org/fhir/R4/validation.html)：R4 對照。
- [ElementDefinition](https://www.hl7.org/fhir/elementdefinition-definitions.html)：元素限制的正式欄位定義。
- [Profiling FHIR](https://www.hl7.org/fhir/profiling.html)：Profile、slicing、繼承與 Must Support。
- [FHIRPath in FHIR](https://www.hl7.org/fhir/fhirpath.html)：FHIR 邏輯模型與 FHIRPath 的整合。
- [FHIRPath specification](https://hl7.org/fhirpath/)：語言、集合與運算語意；實作時固定相容版本。
- [FHIR Datatypes](https://www.hl7.org/fhir/datatypes.html)：型別格式、精度及值域。
- [FHIR Terminology](https://www.hl7.org/fhir/terminologies.html)：binding 與術語要求。
- [FHIR JSON](https://www.hl7.org/fhir/json.html)：解析層與物件層的責任邊界。

各版本行為應回到該版本的固定規範與套件核對；本指引的階段、命名與程式拆分屬於專案實作建議。
