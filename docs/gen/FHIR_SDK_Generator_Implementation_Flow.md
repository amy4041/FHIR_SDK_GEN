**FHIR SDK Generator  
實作流程設計文件**

從 FHIR StructureDefinition 到 C# 原始碼

文件重點：流程、責任邊界、階段銜接與 Code Rendering 策略

Version 1.1

- 文件狀態：Draft / Proposed

- 適用範圍：FHIR R5 5.0.0、MyFhirSdk、.NET 9

- 實作階段：MVP 後續的 SDK Model Generator 規劃

# **1. 文件目的**

本文件整理 FHIR SDK Generator
的整體實作流程，說明各階段的輸入、輸出、主要責任，以及不同階段之間如何銜接。文件不包含具體程式碼，重點放在架構分工與實作順序。

Generator 的核心目標，是將 FHIR Package 中的 StructureDefinition JSON
轉換為 SDK 可使用的強型別 C# 類別，同時保留後續
Serializer、Parser、Validator 與 metadata 使用所需的結構資訊。

# **2. 整體流程概觀**

| **FHIR Package / StructureDefinition JSON** |
|---------------------------------------------|
| ↓ 讀取與反序列化                            |
| **FHIR Definition JSON Model**              |
| ↓ DefinitionParser：解析、正規化、映射      |
| **Generator Internal Model**                |
| ↓ Code Renderer：輸出 C# Source             |
| **C# 原始碼（\*.g.cs）**                    |
| ↓ Roslyn 語法與編譯驗證                     |
| **寫入 Generated 目錄並納入 SDK Build**     |

整體流程應維持單向資料流。前一階段只負責把資料整理成下一階段可直接使用的形式，避免
Renderer 再回頭理解 FHIR JSON，也避免 JSON Model 混入 C# 產生策略。

Generator Internal Model 另有一條平行輸出：Validation Metadata Renderer
產生 Base rule registry 與 Profile rule registry。C# model source 負責可編譯與
可序列化的型別結構；validation registry 負責無法只靠 C# 型別表達的 required、
cardinality、choice、slice 與 Profile constraint。

# **3. 階段一：讀取 FHIR Package 與 Definition JSON**

## **3.1 輸入來源**

Generator 的主要輸入是 FHIR Package 內的 StructureDefinition JSON。MVP
階段可先處理 Base FHIR Package 中的 Resource、Complex Datatype 與
Primitive 定義；後續再擴充至 IG
Profile、Extension、ValueSet、CodeSystem、SearchParameter 等資源。

Package Loader 應先讀取 `package.json` 與 `.index.json`，確認 package name、
package version、FHIR version 與 dependencies，再依 `resourceType` 篩選
StructureDefinition。Registry 必須先載入 dependency package，才能解析跨 package
的 baseDefinition、profile 與 targetProfile。若 `.index.json` 不存在或版本不支援，
可由 package 內容重建索引，但不得把 examples 目錄中的資源誤認為正式定義。

## **3.2 JSON Model 的角色**

JSON Model 用來忠實承接官方 JSON 結構，例如
StructureDefinition、snapshot、element、type、min、max 與
baseDefinition。這一層只表達「JSON 裡有什麼」，不負責決定 C#
類別如何產生。

為支援 snapshot、differential 與 slicing，ElementDefinition JSON Model
至少應保留 `id`、`path`、`sliceName`、`base.path`、`base.min`、
`base.max`、`contentReference`、`type.code`、`type.profile`、
`type.targetProfile`、`min` 與 `max`。後續若產生 Profile 驗證資料，還需保留
`constraint`、`binding`、`fixed[x]`、`pattern[x]`、`mustSupport` 與
`isModifier` 等限制資訊。

- 可直接使用 System.Text.Json 進行反序列化。

- 欄位命名與型別應盡量對應官方 JSON，避免過早加入 Generator 決策。

- JSON Model 不應直接交給 Renderer。

## **3.3 Definition Registry**

讀入全部 StructureDefinition 後，應先建立 Definition Registry，以
canonical URL、FHIR type name 或其他穩定識別方式索引定義。後續解析 Base
Type、比較父類別 snapshot、解析 contentReference
或查找目標型別時，都會依賴 Registry。

Registry 的主要鍵應為 `(canonical URL, version)`；FHIR type name 只能作為
Base Package 內的輔助索引。Registry 必須定義 dependency precedence、重複
canonical、缺少 baseDefinition、無法解析的 contentReference 與循環依賴的診斷方式。

# **4. 階段二：DefinitionParser**

## **4.1 DefinitionParser 的目標**

DefinitionParser 的目標，是把「忠實反映 FHIR StructureDefinition 的 JSON
Model」轉換成「Renderer 可以直接輸出 C# 的 Generator Internal Model」。

換句話說，DefinitionParser 負責理解 FHIR
定義，完成分類、繼承、元素篩選、型別、名稱、基數與巢狀結構等解析工作。完成後，Renderer
不需要再知道 snapshot、max="\*"、deceased\[x\] 或 BackboneElement 的原始
FHIR 表達方式。

## **4.2 DefinitionParser 的主要處理項目**

### **4.2.1 判斷 Definition 類別**

根據 StructureDefinition 的 kind、type、derivation、abstract
等資訊，判斷目前定義屬於 Resource、Complex
Datatype、Primitive，或其他需特殊處理的種類。分類結果會影響類別基底、輸出形式與後續處理策略。

### **4.2.2 解析 Base Type 與繼承關係**

由 baseDefinition 找出父型別，並透過 Definition Registry
取得對應定義。解析結果應轉換成 Generator Internal Model 所使用的 C# Base
Type，而不是只保留 canonical URL。

此階段同時建立明確的繼承鏈，讓後續元素篩選與產生順序能依賴父型別資訊。

### **4.2.3 正規化 snapshot 與 differential**

snapshot 是合併 baseDefinition 後的完整結構，可獨立解讀；differential
只描述相對於 baseDefinition 的新增或限制。Generator 不應單純以父子 snapshot
的 path 差集判斷直接宣告欄位，因為同一路徑可能包含型別限縮、cardinality
變更、slicing、binding、fixed/pattern 或其他限制。

Base FHIR 的 specialization 定義可使用 differential 判斷目前型別新增或覆寫的
元素，再以 snapshot 取得解析後的完整型別與基數。若輸入只有 differential，
Generator 必須先透過獨立的 Snapshot Generator 與完整 dependency chain
產生 snapshot；第一版若不實作 Snapshot Generator，則應明確拒絕缺少 snapshot
的輸入，而不是猜測繼承結果。

DefinitionParser 應輸出三類結果：

- 新增元素：需要生成新的 C# property 或 BackboneElement type。

- 基底元素的模型覆寫：只有 specialization 確實需要重新表達 C# API 時才生成。

- Constraint/Profile 限制：轉成 Profile validation metadata，不在衍生 C#
  class 中重複宣告 inherited property。

### **4.2.4 建立元素階層**

StructureDefinition 的 element 以 path 表示階層。DefinitionParser
應先將平坦的 element
清單整理成可辨識父子關係的元素樹，供直接屬性、BackboneElement、巢狀子元素與
contentReference 後續處理。

元素的主要識別必須使用 `ElementDefinition.id`，不能只使用 `path`。在 Profile
或 slicing 中，多個 element 可以具有相同 path；`id`、`sliceName` 與父元素 id
共同決定其位置。`path` 用來表達 FHIR 邏輯階層，`id` 用來建立可唯一定位的解析樹。

第一版 Base Model Generator 可以保留 slice 資訊但不生成 slice 專用 C# property。
Profile 中的 slicing discriminator、ordered、rules、sliceName 與各 slice
cardinality 應輸出為 Profile validation metadata。若遇到無法完整保留的 slicing
規則，Generator 必須回報明確診斷，不能把多個 slice 合併成一個看似無限制的欄位。

### **4.2.5 處理 BackboneElement**

遇到只存在於特定 Resource 內部的巢狀結構時，DefinitionParser
應建立對應的 BackboneElement Type
Model，並把子元素歸入該巢狀類別。父類別屬性則引用生成的 Component 類別。

Internal Model 中仍保留其 owner resource、element id、path 與父子關係，但
「FHIR 結構上巢狀」不代表「C# 語法上使用 nested class」。Renderer 應將
BackboneElement 輸出為具有 owner 前綴的頂層型別，例如
`Patient.contact` 對應 `PatientContact`、`Claim.item.detail` 對應
`ClaimDetail`，並繼承 `BackboneElement`。

生成檔案依 Resource 分目錄存放，例如：

```text
Generated/R5/Resources
|-- Patient
|   |-- Patient.g.cs
|   |-- PatientContact.g.cs
|   `-- PatientCommunication.g.cs
`-- Claim
    |-- Claim.g.cs
    |-- ClaimItem.g.cs
    |-- ClaimDetail.g.cs
    `-- ClaimSubDetail.g.cs
```

實體目錄不改變 namespace；上述類別仍使用 `MyFhirSdk.Resources`。命名器必須以
完整 owner path 處理同名 component，並在名稱仍衝突時產生診斷或使用穩定的消歧規則。

### **4.2.6 FHIR 型別映射**

將 ElementDefinition.type.code 轉換為 SDK 使用的 C# 型別名稱。Primitive
通常映射至 SDK wrapper；Complex Datatype 與 Resource
通常映射至對應模型類別。Reference、canonical、code
與其他特殊型別可由專門規則處理。

### **4.2.7 C# 名稱轉換**

把 FHIR type name、element name 與 choice type 名稱轉換成符合 C#
慣例的類別名稱與屬性名稱。此階段應集中處理 PascalCase、\[x\]
移除、保留字、特殊字元與命名衝突，避免各 Renderer 或 Builder 各自轉換。

### **4.2.8 Cardinality 映射**

將 min 與 max 轉換成 Internal Model
可直接使用的基數資訊，例如是否為集合、是否必填、最大數量是否無上限。

- 0..1：單值且可省略。

- 1..1：單值且規範上必須存在。

- 0..\*：集合，可為空。

- 1..\*：集合，至少一筆；「至少一筆」由 Validator 驗證。

Renderer 根據已整理好的 IsCollection、IsRequired 與 CSharpType
產生屬性；不應自行重新解讀 min/max。

### **4.2.9 處理 Choice Type**

對於 value\[x\]、deceased\[x\] 等元素，DefinitionParser
應依允許型別展開成對應的 C# 屬性，並在 Internal Model 保留 Choice Group
metadata。Renderer 使用 metadata 產生正確的 C# property 與 FHIR JSON 名稱；
Validator 串接層則用它產生 choice validation rule。

Choice Type 的互斥規則不由 C# 型別本身保證，應在 metadata 與 Validator
中維持。現有 Serializer 與 Parser 不會直接讀取 Generator Internal Model，
因此不能只把 metadata 留在生成期間而期待 runtime 自動套用。

### **4.2.10 建立 Generator Internal Model**

完成上述解析後，DefinitionParser 輸出
FhirTypeModel、FhirPropertyModel、FhirNestedTypeModel、Cardinality
Model、Choice metadata 等 Internal Model。Internal Model 應表達
Generator 已完成的決策，而不是保留待 Renderer 判斷的原始 FHIR 規則。

## **4.3 DefinitionParser 的內部分工建議**

DefinitionParser
可作為流程協調者，但不建議把所有規則放在單一大型類別中。可將功能拆分為下列元件：

| **元件**                  | **主要責任**                                        |
|---------------------------|-----------------------------------------------------|
| Type Kind Resolver        | 判斷 Resource、Complex Datatype、Primitive 等類別。 |
| Base Type Resolver        | 解析 baseDefinition 與 C# 父型別。                  |
| Declared Element Selector | 找出目前型別直接宣告或需重新表達的元素。            |
| Element Tree Builder      | 將 snapshot element 清單整理成父子階層。            |
| BackboneElement Builder   | 建立巢狀 Component 類別模型。                       |
| FHIR Type Mapper          | 將 FHIR 型別映射至 SDK C# 型別。                    |
| C# Name Converter         | 集中處理類別與屬性命名。                            |
| Cardinality Mapper        | 將 min/max 轉成集合與必填資訊。                     |
| Choice Type Builder       | 展開 \[x\] 並建立 choice group metadata。           |

# **5. 階段三：Generator Internal Model**

Internal Model 是 DefinitionParser 與 Code Renderer
之間的契約。它應完全脫離原始 JSON 結構，並直接表達將要生成的 C#
類別結構。

## **5.1 Internal Model 應包含的資訊**

- FHIR 名稱與 C# 名稱。

- 型別分類與是否為 abstract。

- C# Base Type。

- 目前類別直接宣告的屬性。

- 每個屬性的 C# 型別、集合性與必填性。

- FHIR element name 與必要 metadata。

- Choice group 資訊。

- BackboneElement 的 owner resource、element id、path、C# 頂層型別名稱與結構。

- Slice、constraint、binding 與其他 Profile validation metadata；這些資料不應
  被誤轉成 Base Model property。

- 必要的說明文字、順序與生成選項。

## **5.2 責任邊界**

Internal Model 不應包含 System.Text.Json 物件、JsonElement 或需要
Renderer 再次解析的 StructureDefinition path。反過來，Renderer
也不應直接接收 StructureDefinitionJsonModel。

# **6. 階段四：Code Rendering**

## **6.1 Code Renderer 的目標**

Code Renderer 將 Generator Internal Model 轉換成合法且格式一致的 C#
原始碼。它負責 C# 表現方式，例如 namespace、using、class
宣告、繼承、attribute、property、巢狀類別、註解、縮排與換行。

Renderer 不負責判斷 max="\*"、FHIR primitive mapping、choice 展開或
snapshot 繼承差異；這些決策應已由 DefinitionParser 完成。

## **6.2 可選工具比較**

| **工具**             | **產生方式**                                 | **主要優點**                                           | **主要限制**                                                  |
|----------------------|----------------------------------------------|--------------------------------------------------------|---------------------------------------------------------------|
| StringBuilder        | 以字串逐段組合 C# 原始碼。                   | 簡單、快速、容易除錯、無額外工具依賴、適合 MVP。       | 語法安全性較低；複雜語法與縮排規則增加後較難維護。            |
| Roslyn SyntaxFactory | 以 C# Syntax Node 建立語法樹，再輸出原始碼。 | 結構化、語法節點較安全，適合複雜類別、方法與泛型語法。 | API 冗長、學習成本高，簡單屬性也需要較多程式碼。              |
| T4 Template          | 以 .tt 文字模板混合 C# 控制邏輯產生檔案。    | 輸出模板直觀，適合固定且大型的程式碼骨架。             | 工具與 IDE 相依較強；跨平台、CI、測試與複雜條件維護較不方便。 |

## **6.3 第一版預定採用的實作策略**

第一版規劃採用「StringBuilder 負責產生 + Roslyn 負責驗證」的混合方式。

| **Generator Internal Model**                     |
|--------------------------------------------------|
| ↓                                                |
| **StringBuilder Renderer 產生 C# Source String** |
| ↓                                                |
| **Roslyn Parse / Compilation Validation**        |
| ↓ 驗證成功                                       |
| **寫入 \*.g.cs**                                 |

選擇此方式的原因，是先以較低成本完成 Generator 規則與輸出格式，同時利用
Roslyn 補足純文字產生的語法風險。等未來需要大量產生
constructor、method、generic constraint、interface implementation
或更複雜語法時，再評估將 Renderer 替換為 SyntaxFactory 實作。

## **6.4 Renderer 的輸出責任**

- 加入 auto-generated 標記與 nullable 設定。

- 輸出 namespace 與必要 using。

- 輸出 class、abstract、partial 與 inheritance。

- 輸出屬性、attributes、集合初始化與 nullable 表達。

- 將 BackboneElement 輸出為 owner-prefixed 的頂層 class，並依 Resource
  分目錄寫入。

- 維持固定排序、縮排、空行與換行規則。

- 產生一個 FHIR type 對應一個 .g.cs，或依專案規則分檔。

## **6.5 Profile 輸出策略**

StructureDefinition 的 `derivation` 決定輸出種類：

- `specialization`：可建立新的 SDK model type，輸出 C# class 與必要的 Base
  FHIR validation metadata。

- `constraint`：預設不建立繼承自 base resource 的新 C# class。Profile
  通常仍描述相同的 FHIR `type`，其 cardinality、slice、binding、
  fixed/pattern、mustSupport 與 invariant 應輸出為 Profile validation
  metadata。

Profile metadata 至少應保留 package id/version、profile canonical/version、
baseDefinition、element id/path、sliceName、限制內容與診斷來源。它由
Implementation Guide/Profile validation 層載入，而不是由 Base Model Renderer
轉成重複的 C# property。

若未來需要 strongly typed profile facade，應另設 Profile Renderer 與 API
設計，不得讓第一版 Base Model Renderer 暗中把 constraint 當成 specialization。

# **7. 與現有 Serializer、Parser、Validator 的串接**

## **7.1 Serializer**

現有 `FhirJsonSerializer` 以 reflection 讀取 public property，預設將 C#
property name 轉為 lower camel case，並對 `PrimitiveType<T>`、primitive
metadata 與 collection 執行專用處理。因此生成模型若遵守下列契約，不需要讓
Serializer 直接讀取 Generator Internal Model：

- Resource 提供正確的 `ResourceType` override。

- C# property 名稱可穩定轉成 FHIR JSON 名稱；無法由命名規則正確推導時，生成
  `JsonPropertyName`。

- Primitive 使用現有 `PrimitiveType<T>` wrapper，不直接使用 CLR primitive。

- 重複欄位使用現有 Serializer 可辨識的 collection contract。

這表示 Generator 的 choice、cardinality 與 slice metadata 不會自動被 Serializer
消費；Renderer 必須先把會影響 JSON property name 與型別的決策落實到生成的
C# API。

## **7.2 Parser**

現有 `FhirJsonParser` 會掃描 SDK assembly 中具公開無參數 constructor 的 concrete
Resource 與 DataType，因此編譯進同一 assembly 的生成頂層型別可被既有掃描流程
發現。生成型別仍必須符合 public、concrete、可建立 instance 與正確繼承關係等
條件。

但現有 Parser 對 Extension value type 與部分抽象 DataType 的解析仍依賴專用
mapping 或形狀推斷。Generator 新增這類型別時，必須同步產生明確的 parser type
registry，或擴充現有 registry；不能只生成 model class。長期應以生成的穩定
registry 取代容易產生歧義的形狀推斷。

## **7.3 Validator**

現有 `FhirValidator` 可以透過 reflection 走訪新生成的 object graph，但 required、
cardinality 與 choice 規則由 `ResourceRuleRegistry` 明確註冊，不會因為生成了
model property 或 Internal Model metadata 就自動生效。這是 Generator 與現有
Validator 之間最主要的接點缺口。

Generator 應從已正規化的 Base Definition 產生 validation descriptors，至少包含：

- 必填 singleton 與必填 collection。

- collection 最小與最大數量。

- choice group 的 AtMostOne 或 ExactlyOne。

- 可由現有 primitive wrapper 處理的 primitive format rule 關聯。

現有 `ResourceRuleRegistry.CreateDefault()` 應增加合併 generated rule registry
的接點；手寫規則只保留 Generator 無法表達的 SDK 或業務規則。Profile constraint、
slicing、binding、fixed/pattern 與 invariant 則交給 Profile Validator 的 generated
profile registry，不應混入 Base FHIR rule registry。

現有 `CardinalityRule` 只處理 collection 為 null 或包含 null item，尚未提供通用的
`min`/`max` 數量規則；接入 generated descriptors 時必須擴充這一層。現有
`ProfileValidator` 也只會執行 `IImplementationGuidePackage` 提供的明確規則，
因此 generated profile registry 必須透過 package adapter 實作該契約，或為
ProfileValidator 增加可載入 generated metadata 的正式介面。

在上述接點完成前，即使生成的 C# model 可以正常編譯、序列化與解析，也只能代表
「模型可使用」，不能宣稱已完整支援 StructureDefinition 的 validation semantics。

# **8. 階段五：Roslyn 驗證與檔案輸出**

## **8.1 語法驗證**

StringBuilder 產生原始碼後，先交由 Roslyn Parse
檢查語法錯誤，例如括號、分號、型別宣告與語法結構問題。

## **8.2 編譯驗證**

在可取得 SDK 參考組件與生成型別集合的情況下，可進一步建立 Roslyn
Compilation，檢查找不到型別、重複成員、錯誤繼承或命名衝突等編譯問題。

## **8.3 寫入策略**

只有通過驗證的原始碼才寫入 Generated
目錄。輸出流程應具備決定性：相同輸入應產生相同檔名、順序與內容，避免每次生成造成無意義的
Git 差異。

- 先寫入暫存位置，整批成功後再替換正式 Generated 目錄。

- 任何一個必要型別失敗時，應輸出清楚的 definition、element path
  與診斷訊息。

- 生成前可清理舊檔，但避免在失敗時留下不完整產物。

# **9. 階段六：生成結果驗證**

除了 Roslyn 語法與編譯驗證，Generator 還應建立針對輸出內容的測試。

- Golden file：比較代表性 Resource、Datatype 與 Primitive 的完整輸出。

- 結構測試：確認 Base Type、Property、Choice Type 與 BackboneElement
  結果。

- 全 Package smoke test：確認所有預期 StructureDefinition 都能成功生成。

- 決定性測試：相同輸入重跑後輸出完全一致。

- 生成 SDK build test：將全部 .g.cs 納入實際 SDK 專案編譯。

- Runtime integration test：以生成模型執行 Serializer、Parser、Base Validator
  與 Profile Validator 測試。

# **10. 各階段銜接與責任摘要**

| **階段**                   | **輸入**                 | **輸出**                     | **責任**                                     |
|----------------------------|--------------------------|------------------------------|----------------------------------------------|
| JSON Reader / Deserializer | StructureDefinition JSON | StructureDefinitionJsonModel | 忠實讀取官方 JSON，不做 C# 生成決策。        |
| Definition Registry        | 全部 JsonModel           | 可查詢的 Definition 集合     | 提供 baseDefinition、型別與 canonical 查找。 |
| DefinitionParser           | JsonModel + Registry     | Generator Internal Model     | 理解 FHIR 定義並完成所有生成決策。           |
| Code Renderer              | Generator Internal Model | C# source string             | 只處理 C# 表現與格式。                       |
| Validation Metadata Renderer | Generator Internal Model | Base/Profile validation registry | 產生 required、cardinality、choice、slice 與 Profile 限制資料。 |
| Roslyn Validator           | C# source string         | Diagnostics / valid source   | 檢查語法與必要的編譯正確性。                 |
| File Writer                | 通過驗證的 source        | \*.g.cs                      | 以決定性、安全方式寫入 Generated 目錄。      |

# **11. 建議實作順序**

1.  建立最小 StructureDefinition JSON Model 與 System.Text.Json
    讀取流程。

2.  載入 Base FHIR Package 並建立 Definition Registry。

3.  建立最小 Generator Internal Model。

4.  先完成 Resource / Complex Datatype / Primitive 的分類與 Base Type
    解析。

5.  完成 snapshot/differential 正規化，並以 ElementDefinition.id 建立支援
    slicing 的元素樹。

6.  完成 specialization 的直接宣告元素篩選、FHIR 型別映射、名稱轉換與
    Cardinality 映射。

7.  加入 Choice Type 展開。

8.  加入 BackboneElement 處理，輸出 owner-prefixed 頂層型別並依 Resource
    分目錄。

9.  以 StringBuilder 實作第一版 C# Renderer。

10. 產生 Base validation registry，接上現有 Serializer、Parser 與 Validator。

11. 將 constraint Profile 輸出為 Profile validation metadata，加入 slicing
    與其他限制測試。

12. 加入 Roslyn syntax/compilation validation、Golden file、全 Package smoke
    test、generated SDK build 與 runtime integration test。

# **12. 最終架構原則**

- JSON Model 忠實反映 FHIR Definition JSON。

- DefinitionParser 負責所有 FHIR 到 Generator Model 的理解與轉換。

- Internal Model 是穩定的中介契約，也是未來多種輸出格式的基礎。

- Renderer 不直接依賴 FHIR JSON DTO，也不重新判斷 FHIR 規則。

- specialization 產生 Base SDK model；constraint Profile 預設產生 validation
  metadata，而不是重複的 C# model class。

- ElementDefinition.id 是元素與 slice 的主要識別；path 只表達 FHIR 邏輯階層。

- BackboneElement 在 Internal Model 保留巢狀語意，但 C# 輸出使用頂層型別並依
  Resource 分目錄。

- 生成模型必須同時提供現有 Serializer、Parser 與 Validator 所需的 runtime
  contract 或 generated registry，不能只通過 C# 編譯就視為完成。

- 第一版以 StringBuilder 快速實作，以 Roslyn 驗證確保輸出品質。

- 後續若生成語法複雜度提高，可替換 Renderer，不影響前段解析流程。
