# FHIR SDK Generator Golden Testing Guide

## 1. 目的

本文件定義 FHIR SDK Generator 的 Golden File 測試策略，目標是：

- 確保固定的 `StructureDefinition` 輸入會產生固定的 C# source。
- 偵測 using、namespace、class、property、documentation 與格式的非預期變更。
- 讓 Generator 的有意變更可以透過清楚的 source diff 接受人工審查。
- 避免將所有正確性責任放在大量、難以維護的 Golden Files 上。
- 確保一般測試與 CI 不會自動覆寫已審查的預期結果。

Golden File 是本專案對特定 Generator 版本所認可的完整輸出，不是 HL7 FHIR
官方提供的 C# source。FHIR 官方的 `StructureDefinition` 是權威輸入；Golden File
則記錄本 SDK 對 namespace、型別名稱、nullable、collection 與程式碼格式的決策。

## 2. 測試分層

Generator 應使用多層測試，而不是只依賴 Golden File。

### 2.1 Renderer 單元測試

以小型、人工建立的 `FhirTypeModel` 驗證單一 rendering 規則：

- `abstract` 與 `sealed` 決策。
- singleton property 使用 nullable type。
- collection property 使用 `IList<T>` 並初始化。
- ordinal property ordering。
- 固定 using ordering。
- XML documentation escaping。
- LF newline 與 deterministic output。
- 無效參數的失敗行為。

這些規則只需在 `CSharpClassRendererTests` 完整測試，不需要在每一種 FHIR type
重複撰寫相同的細節 assertion。

### 2.2 代表性 Golden tests

Golden test 驗證完整的垂直流程：

```text
固定 StructureDefinition fixture
    -> StructureDefinitionLoader
    -> StructureDefinitionParser
    -> FhirTypeModel
    -> CSharpClassRenderer
    -> Golden File exact comparison
```

它主要保護整體 source shape，例如：

- auto-generated 標記。
- using 與 namespace。
- class declaration 與 base type。
- 所有直接宣告的 properties。
- property type、nullable 與 collection shape。
- XML documentation。
- 空白行、縮排與結尾換行。

Golden File 應挑選能代表不同結構的型別，而不是把同一套 rendering 規則無差別
複製到所有 FHIR types。

### 2.3 全量結構與編譯測試

Generator 擴展到大量 FHIR types 後，應對所有輸出執行自動化檢查：

- 每個支援的 `StructureDefinition` 都能得到 model 與 source。
- class name、base type、property 數量與 mapping 正確。
- 沒有重複 class/property、未解析型別或遺漏輸出。
- 相同輸入重複生成的檔案集合與內容完全一致。
- 所有生成 source 可以由 Roslyn 編譯。

這一層提供廣度，Golden tests 則提供少量但深入、容易人工閱讀的完整輸出基準。

### 2.4 Runtime 行為測試

Golden File 只能證明 source 文字符合預期，不能證明執行行為正確。仍應使用代表性
generated types 驗證：

- JSON serialization 與 parsing。
- primitive value 與 primitive metadata。
- nested complex datatype。
- null singleton 與 empty collection。
- validation rules。

## 3. MVP Golden File 範圍

MVP 僅包含少量 datatype，建議每個目標型別都建立並人工審查 Golden File：

| 型別 | 主要覆蓋情境 |
|---|---|
| `Period` | primitive singleton |
| `Coding` | 多個 primitive properties |
| `HumanName` | primitive collection 與 nested `Period` |
| `Address` | 較多 properties 與 collection |
| `Identifier` | complex datatype reference |

這五份檔案的目的不是重複驗證所有 Renderer 規則，而是確保 MVP 支援的每種主要
property shape 都有一份可人工檢查的完整輸出。

## 4. 目錄與命名

建議維持以下結構：

```text
Tests/CodeGen/
|-- Fixtures/
|   `-- StructureDefinitions/
|       `-- Valid/
|           |-- StructureDefinition-HumanName.json
|           `-- StructureDefinition-Period.json
|-- GoldenFiles/
|   |-- TypeGoldenFileTests.cs
|   `-- R5/
|       `-- Types/
|           |-- HumanName.golden.cs.txt
|           `-- Period.golden.cs.txt
`-- Rendering/
    `-- CSharpClassRendererTests.cs
```

使用 `.golden.cs.txt` 而不是 `.cs`，避免 test project 將 Golden File 當成 C# source
編譯。FHIR major version 必須存在於路徑中，避免未來 R4、R4B 與 R5 的預期輸出
互相覆蓋。

Test project 必須將 fixture 與 Golden File 複製到輸出目錄：

```xml
<ItemGroup>
  <None Update="Fixtures\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  <None Update="GoldenFiles\**\*.txt">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

測試應以 `AppContext.BaseDirectory` 尋找資源，不能依賴執行測試時的 current working
directory。

## 5. 共用參數化測試

每個型別需要一份經審查的 `.golden.cs.txt`，但不應複製一整個測試類別。建議使用
xUnit `TheoryData` 顯式列出已審查案例：

```csharp
public sealed record GoldenFileCase(
    string TypeName,
    string FhirVersion,
    string[] PreviewTypeNames);

public static TheoryData<GoldenFileCase> TypeCases =>
    new()
    {
        new GoldenFileCase("HumanName", "5.0.0", ["HumanName"]),
        new GoldenFileCase("Period", "5.0.0", ["Period"])
    };

[Theory]
[MemberData(nameof(TypeCases))]
public async Task Type_FromFixture_MatchesReviewedGoldenFile(
    GoldenFileCase testCase)
{
    var fixturePath = GetOutputPath(
        "Fixtures",
        "StructureDefinitions",
        "Valid",
        $"StructureDefinition-{testCase.TypeName}.json");
    var goldenFilePath = GetOutputPath(
        "GoldenFiles",
        "R5",
        "Types",
        $"{testCase.TypeName}.golden.cs.txt");

    var loadResult = await new StructureDefinitionLoader().LoadAsync(
        fixturePath,
        testCase.FhirVersion);
    Assert.True(loadResult.IsSuccess);
    Assert.Empty(loadResult.Diagnostics);

    var loadedDefinition = Assert.Single(loadResult.Value);
    var previewTypes = new HashSet<string>(
        testCase.PreviewTypeNames,
        StringComparer.Ordinal);
    var parseResult = new StructureDefinitionParser().Parse(
        loadedDefinition,
        "MyFhirSdk.GeneratorFixtures.Types",
        previewTypes);
    Assert.True(parseResult.IsSuccess);
    Assert.Empty(parseResult.Diagnostics);

    var model = Assert.IsType<FhirTypeModel>(parseResult.Value);
    var renderer = new CSharpClassRenderer();
    var actualSource = renderer.Render(model);
    var repeatedSource = renderer.Render(model);
    var expectedSource = await File.ReadAllTextAsync(goldenFilePath);

    Assert.Equal(actualSource, repeatedSource);
    AssertMatchesGolden(
        NormalizeNewlines(expectedSource),
        NormalizeNewlines(actualSource));
}
```

案例清單應保持顯式，不建議由測試自動掃描所有 fixtures。顯式清單可清楚表達哪些
輸出已經過人工審查，也能讓 preview type dependencies 成為測試案例的一部分。

## 6. 比較規則

### 6.1 LF 正規化

比較前只將 CRLF 或 CR 正規化為 LF：

```csharp
private static string NormalizeNewlines(string value)
{
    return value.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n');
}
```

除了換行格式，不得 trim、忽略空白、重新排序或格式化 source。下列內容都必須參與
exact comparison：

- 每一個空白與縮排。
- 空白行。
- using 順序。
- property 順序。
- XML documentation。
- 檔案結尾換行。

### 6.2 可讀的失敗訊息

Golden mismatch 至少應顯示：

- 第一個不同的行號。
- Expected line。
- Actual line。
- 任一邊提早結束時顯示 `<end of file>`。

案例較多時，測試名稱或錯誤訊息也必須包含 FHIR type 與版本，讓 CI failure 能直接
定位檔案。

## 7. Golden File 建立流程

第一次建立 Golden File 時，使用以下流程：

1. 固定官方來源與 FHIR version 的 `StructureDefinition` fixture。
2. 使用目前的 Loader、Parser、Renderer 產生候選 source。
3. 將候選 source 寫到暫存目錄，不直接覆寫正式 Golden File。
4. 人工檢查候選 source。
5. 確認後複製為 `.golden.cs.txt`。
6. 執行 Golden test 與完整 solution tests。
7. 查看 Git diff，將 fixture、Golden File、案例清單與必要的實作變更一起 review。

候選 source 應由 Generator 產生，不建議從零手寫；但 Generator 產生的結果不能在未
審查前直接視為正確答案。

## 8. 人工審查清單

每份新建或更新的 Golden File 至少檢查：

- [ ] `// <auto-generated />` 存在。
- [ ] UTF-8 without BOM。
- [ ] 僅使用 LF newline。
- [ ] using 完整且順序固定。
- [ ] namespace 符合測試設定。
- [ ] class name 與 FHIR type 對應。
- [ ] base type 正確。
- [ ] `abstract` 或 `sealed` 決策正確。
- [ ] 未遺漏 fixture 中應直接宣告的 element。
- [ ] property 順序與 Internal Model 的 `Order` 一致。
- [ ] singleton property 為 nullable。
- [ ] collection property 的型別與初始化正確。
- [ ] primitive 與 complex type mapping 正確。
- [ ] documentation 來自預期欄位且 XML escaping 正確。
- [ ] source 能通過後續 generated-source compilation test。

## 9. 更新流程

Golden test 失敗時，不應立即接受 Actual output。先判斷變更類型：

```text
Golden mismatch
    |-- 非預期變更 -> 修正 Loader、Parser、Mapper 或 Renderer
    `-- 有意變更   -> 人工審查完整 diff，再更新 Golden File
```

合法更新原因通常包括：

- Renderer 格式契約有意調整。
- Internal Model 或 type mapping 的設計有意調整。
- 固定 fixture 升級到新的 FHIR version。
- 修正原本 Golden File 中已確認的錯誤。

更新後必須在 commit 或 pull request 說明：

- 為什麼預期輸出改變。
- 哪些 Golden Files 受到影響。
- 變更是否影響 public SDK API 或 serialization behavior。

大量 Golden diff 應依規則分類檢查，不能只因「全部由 Generator 產生」就整批接受。

## 10. 禁止一般測試自動更新

一般 `dotnet test` 與 CI 必須是唯讀的。測試中禁止：

```csharp
File.WriteAllText(goldenFilePath, actualSource);
```

否則錯誤的 Generator output 可能先覆寫預期結果，再讓 comparison 通過。

若未來需要 Golden update tool，應符合以下條件：

- 與一般 test command 分離。
- 必須使用明確的 update command 或 option。
- 預設輸出到暫存/preview 目錄。
- 更新後仍要求人工檢查 Git diff。
- CI 不執行 update command。

## 11. CI 建議

CI 至少執行：

1. Renderer unit tests。
2. Golden exact comparison tests。
3. 全量生成的 deterministic test。
4. Generated source compilation test。
5. 代表性 serialization、parser 與 validation tests。

Golden comparison 失敗時，CI artifact 可另外保存 Actual source 或 unified diff，方便
檢查；但 artifact 不得回寫 repository。

## 12. 常見反模式

### 12.1 每個型別複製一份測試類別

問題：pipeline 與 helper 重複，未來修改比較規則時容易不一致。

做法：共用參數化 `TypeGoldenFileTests`，每個型別只新增 fixture、Golden File 與一筆
案例資料。

### 12.2 Golden File 取代語意測試

問題：文字看似正確不代表 source 能編譯、序列化或執行 validation。

做法：保留 mapping、compilation 與 runtime tests。

### 12.3 自動掃描 fixture 並視為已審查

問題：加入 fixture 會隱式擴大 Golden 範圍，且無法清楚描述 preview dependencies。

做法：以顯式案例清單記錄已審查範圍。

### 12.4 比較前格式化或忽略空白

問題：可能隱藏 Renderer 的 formatting regression。

做法：只正規化跨平台 newline，其他內容做 ordinal exact comparison。

### 12.5 測試自動覆寫 Golden File

問題：Actual output 會成為自己的 Expected output，使測試失去保護作用。

做法：一般測試永遠唯讀，更新工具與 CI 分離。

## 13. 專案導入順序

目前建議依下列順序演進：

1. 保留 `CSharpClassRendererTests` 作為 rendering 規則單元測試。
2. 以 HumanName 完成第一條 fixture-to-source Golden pipeline。
3. 加入第二個 datatype 前，將 HumanName 測試重構為參數化
   `TypeGoldenFileTests`。
4. 依序加入 Period、Coding、Address、Identifier fixtures 與 Golden Files。
5. MVP 五種 datatype 完成後加入 generated-source compilation test。
6. 擴大類型範圍時，改以代表性 Golden Files 搭配全量結構與編譯測試，避免 Golden
   File 數量無限制成長。
