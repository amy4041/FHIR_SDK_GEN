# FHIR 物件驗證規則與來源

整理日期：2026-11-02。

本文整合 FHIR 物件驗證應涵蓋的規則、規則定義的來源，以及本專案目前的實作範圍，供 SDK 驗證功能設計與後續開發參考。

## 1. 驗證範圍與版本

FHIR 驗證不只是檢查必填欄位與字串格式，還包含結構、cardinality、值域、術語綁定、invariants、Profile、問卷及情境相關的商業規則。官方的 [Validating Resources](https://www.hl7.org/fhir/validation.html) 是範圍概觀，實際規則分散在機器可讀定義與各章節規範中。

本文主要參考 FHIR R5 5.0.0，並對照 [R4 4.0.1 驗證概觀](https://www.hl7.org/fhir/R4/validation.html)。兩版本的驗證分類相近，但實際欄位、型別、約束與套件必須依目標版本載入，不可直接混用。

實作上應區分三種驗證範圍：

| 範圍 | 輸入與責任 |
| --- | --- |
| 原始文件驗證 | 檢查 JSON／XML 語法、欄位表示方式、未知或重複欄位等。 |
| 物件內容驗證 | 對 Resource 及其子物件檢查型別、次數、值域、invariants 與 Profile。 |
| 情境與系統驗證 | 使用術語服務、參照解析器、Questionnaire 或業務資料驗證跨資源與流程要求。 |

單純的 C# 物件驗證無法還原反序列化時已丟棄的資訊。例如解析器若忽略未知欄位，事後的物件驗證便無法判斷原始文件是否包含該欄位。

## 2. 應涵蓋的驗證規則

### 2.1 結構與序列化格式

| 驗證項目 | 內容 |
| --- | --- |
| Resource 類型 | `resourceType` 合法，且與內容及目標版本一致。 |
| 欄位定義 | 欄位屬於該版本的 Resource、Datatype 或適用定義；未知欄位依明確的相容性政策處理。 |
| 欄位型別 | primitive、複合物件、陣列使用正確表示。 |
| JSON 規則 | 名稱大小寫、名稱唯一性、數字／布林／字串型別、重複元素的陣列表示。 |
| Primitive metadata | 值與 `_欄位名` metadata 的組合，以及 primitive 陣列對齊與規範允許的 null 佔位。 |
| XML 規則 | Namespace、元素順序、屬性與元素表示方式。 |
| 空內容 | 空字串、空物件、空陣列及 null 的使用符合該格式規範，保留明定的例外。 |

此類規則需要解析器與物件驗證器共同完成，不能只依賴 CLR 型別。

來源：[FHIR JSON](https://www.hl7.org/fhir/json.html)、[FHIR XML](https://www.hl7.org/fhir/xml.html)。

### 2.2 Cardinality：出現次數

必須支援完整的 `min..max`：

- 最少次數：如 `1..1`、`1..*`，以及大於一的最小值。
- 最多次數：如 `0..1`、`0..3`；`*` 代表未設有限上限。
- 禁止欄位：Profile 將 `max` 設為 `0` 時，欄位不得出現。
- 可選父物件不存在時，不應單憑其必填子欄位缺少而報錯；父物件存在後才檢查內部要求。
- 經 slicing 分類後，每個 slice 仍須分別檢查 min/max。

元素出現次數與 primitive 是否帶有實際值是不同問題。primitive 可能使用 Extension 表達缺值原因；是否允許，須依該元素的型別及額外限制判斷。

來源：[ElementDefinition](https://www.hl7.org/fhir/elementdefinition-definitions.html)。

### 2.3 型別、primitive 格式與值域

| 驗證項目 | 範例 |
| --- | --- |
| 允許型別 | 內容符合基底定義及 Profile 限縮後的型別。 |
| 詞法格式 | `id`、`code`、`uri`、`canonical`、`base64Binary` 等符合各自格式。 |
| 數值範圍 | `positiveInt` 大於零，整數未超出該版本範圍。 |
| 日期時間語意 | 日期實際存在，時間精度及時區符合該型別要求。 |
| 部分精度 | 合法的年或年月表示，不應一律要求完整年月日。 |
| 額外限制 | 套用定義中的 `maxLength`、`minValue[x]`、`maxValue[x]` 等。 |

例如 `2026-02-30` 不能只因字串外觀符合日期格式就通過。詞法與語意檢查應分別考量。

來源：[FHIR Datatypes](https://www.hl7.org/fhir/datatypes.html)、[ElementDefinition](https://www.hl7.org/fhir/elementdefinition-definitions.html)。

### 2.4 Choice element：`[x]`

對 `Observation.value[x]` 等欄位，應檢查允許型別、互斥關係與總 cardinality。

- 不可同時提供互斥的 `valueString`、`valueQuantity` 等選項。
- 是否允許完全不提供值，依 min/max 決定。
- Profile 限縮為 `Quantity` 時，其餘原本在基底中合法的型別也不得使用。

來源：[Profiling FHIR](https://www.hl7.org/fhir/profiling.html)。

### 2.5 Invariants：跨欄位與條件約束

應執行基底型別、Resource 與 Profile 所定義的適用 constraints，包括：

- 欄位共存或互斥。
- 某條件成立時，另一欄位必須存在。
- 日期區間、數值與相關欄位之間的關係。
- 由繼承鏈帶入的約束。

例如 `Period.start` 與 `Period.end` 的先後關係，不能由兩個日期各自的格式檢查取代。通用實作通常需要 FHIRPath 執行能力，並保留 constraint 的 key、human、severity 及來源。

若某規則依賴未提供的解析器或外部資料，應回報未完成驗證的原因，避免默認通過。

來源：[Datatype constraints](https://www.hl7.org/fhir/datatypes.html)、[ElementDefinition.constraint](https://www.hl7.org/fhir/elementdefinition-definitions.html#ElementDefinition.constraint)。

### 2.6 Terminology：代碼與 ValueSet binding

| 驗證項目 | 內容 |
| --- | --- |
| CodeSystem | 依 `system`、`version` 與 `code` 檢查代碼有效性。 |
| ValueSet | 檢查代碼是否屬於該欄位綁定的集合。 |
| Display | 檢查提供的 display 與代碼、語言情境是否相符，並明確定義回報政策。 |
| Required | 須滿足指定 ValueSet 的綁定要求。 |
| Extensible | ValueSet 可表達該概念時應使用其中代碼，不能把所有集合外代碼直接判成錯誤。 |
| Preferred／Example | 不應僅因代碼不在集合內便判定不相容。 |

對 Required binding 的 `CodeableConcept`，通常要求至少一個 coding 滿足綁定，而不是所有 coding 都必須屬於同一 ValueSet。Extensible 是否有合適概念可用，有時需要人工判斷；實際條件也須依被綁定的資料型別解讀。

術語服務不可用、CodeSystem 不受支援或 ValueSet 無法解析，應與「已確認代碼無效」分別回報。

來源：[FHIR Terminology 與 Binding Strength](https://www.hl7.org/fhir/terminologies.html)。

### 2.7 Profile／IG 限制與 slicing

完整 Profile 驗證應包含：

| 規則 | 內容 |
| --- | --- |
| 基底繼承 | 同時符合基底 Resource、父 Profile 及目前 Profile。 |
| 型別與 cardinality | 套用更嚴格的次數及型別要求。 |
| `fixed[x]` | 值符合固定內容。 |
| `pattern[x]` | 符合指定模式，不能一律以完整物件相等比較。 |
| Slicing | 依 discriminator 分類，檢查各 slice 的次數、內容及 open／closed、ordered 等設定。 |
| 額外規則 | Profile 增加的 invariants、binding、Extension 與參照目標限制。 |
| 適用範圍 | 納入呼叫端、交換情境、IG 或 `meta.profile` 要求的 Profile。 |

例如 `Patient.identifier` 可依 system 等條件切成不同 slice；僅驗證「至少一個 identifier」不足以證明符合該 Profile。

`mustSupport = true` 不等於每筆資料必填。必填主要由 cardinality 決定；Must Support 的具體義務須依 IG 對不同角色的定義解讀，部分需要系統行為測試。

來源：[Profiling FHIR](https://www.hl7.org/fhir/profiling.html)、[Must Support](https://www.hl7.org/fhir/profiling.html#mustsupport)。

### 2.8 Extension 與 modifierExtension

- Extension URL、使用位置及內容符合其定義。
- `value[x]` 的型別與 cardinality 合法。
- 同一個 Extension 不可同時具有巢狀 `extension` 與 `value[x]`。
- 套用 Extension 定義中的 binding、invariants 與其他限制。
- 明確處理未知 Extension；無法理解的 modifierExtension 可能改變資料含義，不可無條件忽略。

來源：[FHIR Extensibility](https://www.hl7.org/fhir/extensibility.html)。

### 2.9 Reference 與資源關聯

| 範圍 | 規則 |
| --- | --- |
| 參照本身 | `reference`、`identifier`、`display` 等組合符合 Reference 約束。 |
| 目標型別 | 目標 Resource 型別符合欄位允許範圍。 |
| 目標 Profile | 在可取得目標時，驗證其是否符合要求的 target Profile。 |
| 內部參照 | `#id` 能對應到正確的 contained resource。 |
| Bundle 內參照 | 依 fullUrl 與 Bundle 的參照解析規則尋找目標。 |
| 外部參照 | 在具有 resolver、存取權及適用政策時，確認目標存在並檢查內容。 |

邏輯參照不一定能透過 HTTP 取得目標。連線失敗也不等於已證明目標不存在，應區分不合法與無法驗證。

來源：[Resource References](https://www.hl7.org/fhir/references.html)。

### 2.10 Bundle 與 QuestionnaireResponse 情境規則

Bundle 應依 `type` 檢查 entry 的 resource／request／response、fullUrl 與版本的唯一性規則，以及內部參照。Document Bundle 第一筆須為 Composition；Message Bundle 第一筆須為 MessageHeader。不可將所有 Bundle 類型一律套用相同條件。

QuestionnaireResponse 應對照 Questionnaire 的 linkId、階層、答案型別、重複、選項、必填及啟用條件。完整性要求須考慮回答狀態，不能對仍在填寫中的問卷一律要求完成。

來源：[Bundle constraints](https://www.hl7.org/fhir/bundle.html#constraints)、[QuestionnaireResponse](https://www.hl7.org/fhir/questionnaireresponse.html)。

### 2.11 Narrative XHTML

應檢查 `text.div` 的 XHTML 結構、允許的內容及不安全表示方式。Narrative 是否完整涵蓋重要臨床資訊，通常無法單靠結構規則證明，需要人工或額外流程審查。

來源：[FHIR Narrative](https://www.hl7.org/fhir/narrative.html)。

### 2.12 商業規則與操作情境

依系統與流程要求，可能包含識別碼檢查碼、重複資料、業務唯一性、跨資源病人一致性、工作流程狀態轉換、Create／Update 要求及操作權限。

這些規則應與基礎 FHIR 相容性分別回報。例如「出生日期不可晚於今天」不能只因符合一般業務直覺，就視為所有 FHIR Patient 的基礎規則。

來源：[FHIR Validation：Business Rules](https://www.hl7.org/fhir/validation.html)。

## 3. 規則來源

### 3.1 來源分類

| 來源 | 提供的規則 |
| --- | --- |
| `StructureDefinition` | Resource、Datatype、Extension 與 Profile 的結構、次數、型別、固定值、模式、切片、binding 與 invariants。 |
| `ValueSet` | 欄位可用代碼的集合定義；可能需要展開或術語服務判定成員資格。 |
| `CodeSystem` | 代碼、概念及相關屬性；外部術語未必附完整內容。 |
| `Questionnaire` | 問卷題目、答案型別、必填、重複、選項及啟用條件。 |
| JSON／XML 與其他格式規範 | 欄位表示、元素順序、primitive metadata 等序列化要求。 |
| FHIR／IG 文字說明 | 機器定義未完整表達的語意、Must Support 義務及使用條件。 |
| 交換情境與系統規格 | 適用的 Profile、角色、操作條件與商業規則；CapabilityStatement 也可能提供相容性情境。 |

`validation.html` 是驗證範圍的說明頁，不是包含全部規則的單一規則庫。

來源：[StructureDefinition](https://www.hl7.org/fhir/structuredefinition.html)、[ValueSet](https://www.hl7.org/fhir/valueset.html)、[CodeSystem](https://www.hl7.org/fhir/codesystem.html)、[Questionnaire](https://www.hl7.org/fhir/questionnaire.html)。

### 3.2 StructureDefinition 與 ElementDefinition

`StructureDefinition` 以 `ElementDefinition` 描述各元素。常見對應如下：

| 定義欄位 | 驗證用途 |
| --- | --- |
| `min`、`max` | 元素出現次數。 |
| `type.code` | 允許的資料型別。 |
| `type.profile` | 資料型別需符合的 Profile。 |
| `type.targetProfile` | 參照目標需符合的 Profile。 |
| `fixed[x]`、`pattern[x]` | 固定內容及匹配模式。 |
| `minValue[x]`、`maxValue[x]`、`maxLength` | 值域及長度。 |
| `slicing`、`sliceName` | 切片分類與限制。 |
| `binding.strength`、`binding.valueSet` | 術語綁定強度與集合。 |
| `constraint.key`、`expression`、`human`、`severity` | 約束識別碼、FHIRPath、訊息與嚴重程度。 |
| `mustSupport` | 系統支援義務，須結合 IG 說明，不能直接等同必填。 |

Profile 的 `differential.element` 描述相對於基底的變更；`snapshot.element` 描述整合繼承後的元素定義。驗證器通常使用 snapshot，或先由 differential 與基底產生完整定義，不能只讀 differential 而遺漏繼承規則。

來源：[StructureDefinition](https://www.hl7.org/fhir/structuredefinition.html)、[ElementDefinition](https://www.hl7.org/fhir/elementdefinition-definitions.html)。

### 3.3 套件取得與版本固定

官方及 IG 定義通常透過 FHIR NPM package 取得，例如：

- R4 基礎定義：`hl7.fhir.r4.core#4.0.1`。
- R5 基礎定義：`hl7.fhir.r5.core#5.0.0`。
- IG：使用該 IG 發布的套件版本及其相依套件。

套件通常包含 `StructureDefinition-*.json`、`ValueSet-*.json`、`CodeSystem-*.json` 等資源。應固定 FHIR 版本、IG 套件版本與相依版本，並記錄定義來源，才能重現驗證結果。

套件版本、FHIR 基礎版本與 canonical URL 的版本限定是不同概念，需分別處理。取得套件也不代表具備所有外部術語內容，必要時仍需術語服務。

來源：[FHIR Packages](https://www.hl7.org/fhir/packages.html)。

## 4. 本專案目前的規則來源與執行關係

以下為整理當日依原始碼確認的狀況，並非完整相容性測試的結論。

```text
FHIR 官方／IG 套件中的定義
  → CodeGen 載入與中介資料
  → 目前支援產生的規則與模型提供者
  → FhirModelContext
  → Validation 驗證引擎
  → ValidationResult

另外登錄的 IG 程式規則
  → ProfileValidator
  → 合併基本驗證與 IG 驗證結果
```

| 位置 | 角色 |
| --- | --- |
| [DefinitionPackageLoader.cs](../../CodeGen/Loading/DefinitionPackageLoader.cs) | 載入定義套件。 |
| [ModelValidationMetadataIr.cs](../../CodeGen/Ir/ModelValidationMetadataIr.cs) | 保存 constraints、binding、slicing、fixed、pattern 等中介資料。保存資料不代表已在執行階段執行全部規則。 |
| [ModelMetadataIrBuilder.cs](../../CodeGen/Metadata/ModelMetadataIrBuilder.cs) | 建立目前支援產生的驗證規則 metadata。 |
| [ValidationCompositionRenderer.cs](../../CodeGen/Rendering/ValidationCompositionRenderer.cs) | 將規則 metadata 轉成規則登錄程式碼。 |
| [R4ValidationRules.g.cs](../../Generated/R4/ModelMetadata/R4ValidationRules.g.cs)、[R5ValidationRules.g.cs](../../Generated/R5/ModelMetadata/R5ValidationRules.g.cs) | 各版本產生的型別規則登錄。 |
| [ModelRules.cs](../../ModelSupport/ModelRules.cs) | 模型提供者共用的必填與 choice 規則實作。 |
| [Validation/Rules](../../Validation/Rules) | SDK 驗證引擎使用的基本規則與輔助工具。 |
| [FhirValidator.cs](../../Validation/FhirValidator.cs) | 執行物件巡覽、primitive 與型別規則。 |
| [ProfileValidator.cs](../../Validation/Profiles/ProfileValidator.cs) | 組合基本驗證及已登錄的 IG 規則。 |
| [TwCorePatientRules.cs](../../ImplementationGuides/TwCore/Validation/TwCorePatientRules.cs) | 以程式碼明確實作部分 TW Core Patient 要求。 |

### 4.1 能力與待補項目

| 能力 | 目前狀況 |
| --- | --- |
| 物件巡覽、問題路徑 | 已有。 |
| 必填、清單至少一項 | 已有。 |
| Choice 互斥與恰好一項 | 已有。 |
| Primitive 與 ID 格式 | 已有機制，覆蓋範圍依實際 PrimitiveRegistry。 |
| 通用 min/max | 尚需擴充；目前 CardinalityRule 主要回報 null 清單與 null 項目。 |
| Profile 規則組合 | 已有套件登錄與程式規則執行框架。 |
| 通用 Profile 解讀 | 尚未看到完整執行 fixed、pattern、slicing 等定義的引擎。 |
| 通用 FHIRPath invariants | 尚未看到完整執行機制。 |
| Terminology／ValueSet | 尚未看到通用的 binding 驗證服務。 |
| Reference、Bundle、Questionnaire | 尚需補齊通用情境驗證能力。 |

目前 TW Core Patient 規則包含 identifier 及其 system、value 的必要性檢查；不能以這些規則代表已覆蓋完整 TW Core Profile。

## 5. 實作與結果回報建議

本節為專案設計建議，不是 HL7 規定的類別或介面名稱。

1. 在解析階段保留並回報原始格式問題，避免未知、重複欄位等資訊消失。
2. 優先補齊完整 cardinality、基礎 invariants、術語 binding，以及 StructureDefinition 驅動的 Profile 驗證。
3. 將規則定義、執行引擎與外部服務分開，支援固定套件版本及明確的參照解析政策。
4. 對術語、Profile、Reference 等缺少依賴的情況，記錄未執行或未完成的規則與原因。
5. 保留問題路徑、規則 ID、severity、套件、Profile、版本及來源，便於診斷與重現。
6. 支援轉成 OperationOutcome；不要把 Warning／Information 自動等同必須拒收資料。

結果應能區分「已通過所執行的規則」「已確認違規」與「驗證未完成」。目前 [ValidationResult.IsValid](../../Validation/ValidationResult.cs) 以 `Issues.Count == 0` 判斷，只有 Warning／Information 時也會為 false；如需錯誤層級或完整度判斷，應另外明確定義。

驗證器只能檢查可計算且具備所需定義與依賴的要求。資源靜態驗證不能單獨證明整個系統符合 FHIR，也無法完全取代臨床語意與 IG 文字要求的人工審查。

來源：[FHIR Validation](https://www.hl7.org/fhir/validation.html)、[OperationOutcome](https://www.hl7.org/fhir/operationoutcome.html)。
