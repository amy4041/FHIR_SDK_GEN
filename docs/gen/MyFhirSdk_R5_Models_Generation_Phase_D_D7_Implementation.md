# MyFhirSdk CodeGen Phase D D7 實作紀錄

Version 1.0

- 狀態：Implemented
- 前置階段：D0-D6 completed
- Tool package：`MyFhirSdk.CodeGen.Tool` `1.0.0`
- Tool command：`myfhir-codegen`

## 1. Clean-environment smoke

`eng/Invoke-CodeGenToolSmoke.ps1` 在 repository 外建立一次性工作目錄，並隔離
`DOTNET_CLI_HOME` 與 `NUGET_PACKAGES`。它只以本次 build 的 local package source 還原
manifest 中的精確版本，執行 `--help`、locked offline R5 full-model generation 與 primitive
generation，再 uninstall/reinstall 後重跑。兩次輸出都必須與 committed `Generated/R5`
逐檔比對 SHA-256；full-model source 數固定為 831。

腳本輸出：

- `smoke.log`：tool restore、help、generation 與 uninstall 指令紀錄；
- `smoke-summary.json`：package/version/TFM、artifact counts 與 lifecycle 狀態；
- `artifact-hashes.txt`：可供 Windows/Linux byte-level 比對的排序後 hash inventory。

## 2. Upgrade baseline 與真實升級

目前 `1.0.0` 是第一個 local tool package，沒有版本嚴格較舊的真實 package。因此 D7 不建立
假的 `1.0.1`：`eng/codegen-tool-upgrade-baseline.json` 記錄 `1.0.0` baseline，
`eng/Test-CodeGenToolUpgrade.ps1` 產生 `baseline-established`、`upgradeRequired: false` 與
`inputsValidated: true` 證據；實際 smoke summary 明確記錄 `upgradeExecuted: false`，lifecycle
採同版 uninstall/reinstall。

未來 current version 高於 baseline 時，CI/release 必須提供 baseline 對應的真實 `.nupkg`；
CI 會從 baseline 記錄的 immutable Git source revision 重建該 `.nupkg`，不提交 binary，也不建立
假版本；缺少或無法重建舊版 package 時檢查會 fail-fast。
`eng/Invoke-CodeGenToolSmoke.ps1 -PreviousPackagePath`
會依序安裝舊版、生成、更新 local manifest、restore 目前版、再次生成，驗證 CLI/manifest
version 已更新。舊版 primitive generation 使用舊 package 自帶的 policy；只有新舊 descriptor 的
semantic generation-contract fingerprint 相同時，才要求 model/primitive source hashes 保持一致。
tool version 的排序使用共用 SemVer comparator，支援 stable、prerelease 與 build metadata；baseline
的狀態、source revision 與「版本提升必須提供真實舊版」開關均為 fail-fast policy。

## 3. TFM 升級 contract

`Directory.Build.props` 的 `MyFhirSdkTargetFramework` 是 build/test project 的單一 TFM 設定。
project、pipeline、test staging 與 package inventory 不再寫死 `net9.0`；descriptor 與 compatibility
matrix 仍保留具體 TFM，作為可稽核的 committed contract。

`eng/Test-CodeGenToolchainContract.ps1` 驗證中央 TFM 與下列項目同步：

- `global.json` SDK major；
- CodeGen package ID/version/command；
- Runtime descriptor TFM、logical name、hash 與 tool/CodeGen version；
- `GenerationCompatibilityMatrix` 與 local tool manifest；
- 所有 project 不含個別 TFM literal，package snapshot 使用 `<tfm>` 正規化。

升級 .NET/TFM 時先改中央設定，再重新建立 compiler-only Runtime asset、更新 descriptor hash 與
具體 compatibility contract。任何漏同步會在 build/smoke 前 fail-fast。

## 4. CI evidence

CI 先在 Windows 建立單一 canonical compiler reference asset，Windows/Ubuntu 都使用該 artifact
build、test、pack 及執行 clean-environment smoke，避免比較兩個平台各自編出的 Runtime DLL。
最後由獨立 job 比對 normalized package inventory 與 generated artifact hashes。
`.gitattributes` 將整個 `Generated/R5/**` 固定為 LF，確保 Windows checkout 的 committed
baseline bytes 與 CodeGen 寫出的 deterministic LF bytes 相同；toolchain contract 會防止此規則
被意外移除。

每個平台即使 smoke 失敗也嘗試上傳 `.nupkg`、package inventory、upgrade status、smoke
summary/log/hashes；cross-platform job 另上傳 drift report。FHIR generation 全程使用 repository
中的 locked offline fixtures。
