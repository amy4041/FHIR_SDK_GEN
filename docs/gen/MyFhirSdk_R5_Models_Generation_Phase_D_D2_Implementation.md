# MyFhirSdk CodeGen Phase D D2 實作紀錄

Version 1.0

- 狀態：Completed
- 前置基線：D0 decisions Version 1.0、D1 Runtime contract schema 1
- Runtime contract：`phase-a-v1+c4-primitives-v1`
- Target framework：`net9.0`

## 1. 完成範圍

D2 已移除 production CodeGen 對完整 `MyFhirSdk` 專案與 CLR types 的編譯期依賴：

- `MyFhirSdk.CodeGen.csproj` 不再參考 `MyFhirSdk.csproj`；
- `ModelMetadataIrBuilder` 由建構子接受已驗證的 `RuntimeContractView`；
- external bootstrap type、datatype inheritance 與 declared datatype slot 均由 descriptor 的
  symbols、roles 與 declared slots 決定；
- production metadata 路徑不再使用 `typeof(FhirObject).Assembly`、`typeof(DataType)`、
  `Assembly.GetType` 或 SDK reflection；
- `RoslynCompilationValidator` 由建構子接受 `RuntimeReferenceSet`，不再自行尋找 SDK assembly
  或認識 SDK CLR types；
- primitive、model、metadata、complex datatype 與 resource/backbone pipelines 均顯式注入同一個
  compilation validator；
- SDK runtime behavior 僅由 test project 的 `CodeGenTestRuntime` composition root 提供。

Renderer 仍可從 IR 輸出 `MyFhirSdk.Core`、`MyFhirSdk.Types` 與
`MyFhirSdk.Resources` namespace/type name 字串；這些字串是生成結果的 public identity，並非
CodeGen 對 concrete SDK models 的編譯或反射依賴。

## 2. Runtime contract 驅動的 metadata 行為

`RuntimeContractView` 提供三個只讀查詢：

- `TryGetSymbol`：以完整 CLR type name 解析 descriptor symbol；
- `GetRequiredRole`：取得 validator 已保證唯一的必要 role；
- `IsAssignableTo`：沿 descriptor 的 `baseClrType` graph 判斷繼承關係。

因此 concrete datatype metadata 不再依賴 CLR `Type.IsAssignableFrom`；`Meta.Security` 與
`Meta.Tag` 也不再反射 property element type，而是由 role 為 `declared-datatype` 的 slots
明確允許。找不到 external bootstrap symbol 時仍產生既有 deterministic conflict diagnostic。

## 3. Compilation dependency seam

`RuntimeReferenceSet` 現階段包含 target framework 與 ordered reference paths。
`Net9RuntimeReferenceSetFactory` 組合已安裝的 .NET 9 reference assemblies 與呼叫端提供的單一
Runtime compiler assembly path。production host 僅依 descriptor 的 assembly name，在工具目錄中
組成該 asset path；不搜尋 repository、`bin` 或 `obj`。

D3 將擴充這個 seam，加入 logical assembly identity、contract/reference hash、package-owned asset
解析、完整驗證與 stable diagnostics。D2 不加入 repository fallback，也不把 SDK ProjectReference
加回 production graph。

## 4. Architecture gates

新增 architecture tests 固定以下條件：

- CodeGen assembly references 不含 `MyFhirSdk` implementation assembly；
- `ModelMetadataIrBuilder` 必須要求 `RuntimeContractView`；
- `RoslynCompilationValidator` 必須要求 `RuntimeReferenceSet`。

Tests 可參考 SDK 以驗證真實 runtime behavior，但該相依不會傳回 production CodeGen project。

## 5. 驗收結果

- CodeGen 可獨立 clean/build，且 output 不含 `MyFhirSdk.dll`；
- CodeGen project-to-project reference 清單為空；
- CodeGen tests 全數通過；
- solution Release build 與完整 tests 通過；
- committed model generation drift test 通過，Phase C generated artifacts 無變更；
- production dependency/static architecture scans 通過；
- `git diff --check` 通過。

## 6. D3 交接

D3 可直接從 `RuntimeReferenceSet` 與 `Net9RuntimeReferenceSetFactory` 的 boundary 開始，完成
package-owned compiler asset 的 identity/hash 驗證與 deterministic diagnostics。D3 不應重新引入
SDK ProjectReference、CLR type probing 或 repository build-output fallback。
