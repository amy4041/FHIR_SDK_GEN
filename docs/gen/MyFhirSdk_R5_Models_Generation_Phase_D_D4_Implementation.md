# MyFhirSdk CodeGen Phase D D4 實作紀錄

Version 1.0

- 狀態：Completed
- 前置基線：D0-D3 completed
- Runtime contract：`phase-a-v1+c4-primitives-v1`
- Target framework：`net9.0`

## 1. 完成範圍

D4 移除 packaged CodeGen host 對 repository root、solution marker 與 repository build output
位置的假設。`Program.Main` 現在只以 `AppContext.BaseDirectory` 作為 tool installation root，
不再向上搜尋 `MyFhirSdk.sln`，也不以目前工作目錄作為 packaged asset fallback。

production `RepositoryRootLocator` 與舊的 `PrimitiveGenerationPolicyDefaults` 已移除；需要比對
committed output 的測試自行明確解析並注入 development repository root，不參與 production
asset precedence。

## 2. ToolAssetResolver 與 CLI precedence

`ToolAssetResolver` 集中解析以下 package-owned layout：

```text
<tool-root>/Policy/*.json
<tool-root>/Assets/RuntimeReferences/<contract-tfm>/<assembly-name>.dll
```

CLI 增加 D0 已固定的選項：

- `--policy-root <directory>`：model policy set 的整體 override；
- `--runtime-contract <file>`：Runtime descriptor override；
- `--runtime-reference <file>`：可重複的 explicit compiler reference input。

每一類 asset 都採「CLI 明確值優先，否則 package-owned default」。`--policy-root` 會讓所有
model policy 從同一個 root 解析，不會混入 packaged model policy；model mode 原有
`--policy <file>` 仍只覆寫 primitive policy，primitive mode 的 `--policy` 仍為 required。
explicit runtime references 會完整交給 D3 reference service；logical identity ordering、duplicate、
TFM、assembly identity 與 hash 驗證仍由 D3 service 負責，因此 CLI 輸入順序不影響最終 reference set。

`GeneratorCommandLineParser` 必須由 host 明確注入 `ToolAssetResolver`，因此 parser 與 pipeline
不會自行尋找 repository、environment variable、`bin` 或 `obj` fallback。

## 3. Output safety composition

`GeneratedFileWriter` 現在接受 `OutputSafetyContext`，不再接受 repository root。packaged host
建立的 universal safety context 保護：

- filesystem root；
- tool installation directory；
- input package／definitions；
- Runtime contract 與 compiler references；
- generation policies。

writer 同時保留 rooted artifact、`..` traversal、duplicate/colliding artifact、atomic staging、
swap、rollback 與 cancellation 行為。protected asset 與 output 任一方向重疊時都會在寫入前
拒絕，避免 output transaction 取代 input 或 tool assets。

repository protection 是可選的 development composition。測試透過
`OutputSafetyContext.WithDevelopmentRepository(...)` 明確加入 repository root 與 SDK source
trees；writer 本身不再知道 SDK repository layout。正式 `Program.Main` 不加入這些規則。

## 4. 驗收結果

- `Program` production source 不含 `RepositoryRootLocator` 或 `MyFhirSdk.sln` 搜尋；
- 從任意空白 current directory 執行有效命令，仍可載入 package-owned contract/reference；
- package policy/reference default 不受 current directory 影響；
- CLI asset override precedence、repeatable reference ordering 與 primitive/model option semantics
  有測試覆蓋；
- repo-free writer 可完成 atomic output，tool/input overlap 會被拒絕；
- development repository root 與既有 SDK source trees 仍受保護；
- CodeGen tests 338/338 通過，committed full model output baseline 不變。

## 5. D5 交接

D5 應在目前明確的 asset composition 上建立 compatibility matrix 與 manifest provenance，並為
missing/corrupt package asset、tool/Runtime/policy/FHIR version mismatch 配置穩定且不洩漏實體
installation path 的 diagnostics。D4 不增加 repository 或 current-directory fallback。
