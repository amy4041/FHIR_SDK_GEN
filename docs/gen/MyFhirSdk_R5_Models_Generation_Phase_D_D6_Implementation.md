# MyFhirSdk CodeGen Phase D D6 實作紀錄

Version 1.0

- 狀態：Completed
- 前置基線：D0-D5 completed
- Package ID：`MyFhirSdk.CodeGen.Tool`
- Tool command：`myfhir-codegen`
- Package version：`1.0.0`
- Target framework：`net9.0`

## 1. 完成範圍

D6 將 CodeGen 封裝成 repository-local .NET tool。`CodeGen/MyFhirSdk.CodeGen.csproj`
現在使用 `PackAsTool`，並固定 package/tool identity、authors、description、repository、readme
及 deterministic/CI build metadata。由於團隊尚未核准正式 license，本階段刻意不宣告 license
metadata，也不加入暫時授權文字。公開 NuGet 發布、正式授權、簽章與 SBOM 仍依 D0 保留到
release phase，不由 D6 隱式決定。

CLI help 直接顯示 package ID、tool version 與 command name，並使用
`dotnet myfhir-codegen` 語法。repository root 的 `.config/dotnet-tools.json` 將版本固定為
`1.0.0`。既有 public `GeneratorCommandLineParser.Usage` 保持 compile-time `const` contract。

## 2. Package-owned assets

package 依 D0 layout 攜帶：

```text
tools/net9.0/any/
├─ MyFhirSdk.CodeGen.dll
├─ DotnetToolSettings.xml
├─ Policy/
│  ├─ primitive-generation-policy.json
│  ├─ r5-model-ownership-policy.json
│  ├─ r5-model-naming-policy.json
│  ├─ r5-backbone-policy.json
│  ├─ r5-choice-open-type-policy.json
│  └─ r5-validation-capability-policy.json
├─ Contracts/
│  └─ runtime-contract.json
└─ Assets/RuntimeReferences/net9.0/
   └─ MyFhirSdk.dll
```

`MyFhirSdk.dll` 只作 Roslyn compiler reference，不是 CodeGen production assembly dependency，
也不由 packaged host 載入或掃描 model inventory。Runtime descriptor 的 production default 已由
暫時的 `Policy/runtime-contract.json` 校正為 D0 固定的 `Contracts/runtime-contract.json`；CLI
override precedence 維持「explicit override > package-owned default」，不增加 repository、目前
目錄或環境 fallback。

## 3. Packaging pipeline 與 local manifest

`eng/MyFhirSdk.CodeGen.Build.proj` 的 `Pack` target 先以同一次 Release pipeline 建立 compiler
reference asset，再明確傳入 CodeGen pack：

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
dotnet tool restore --add-source artifacts/packages --ignore-failed-sources
dotnet myfhir-codegen --help
```

預設 package output 是 `artifacts/packages`。package test 使用兩個隔離 output directory，local
manifest smoke 另外使用乾淨的 `DOTNET_CLI_HOME`、`NUGET_PACKAGES` 與只包含本次 build package
的 NuGet source，避免 global tool、公開 feed或使用者 cache 掩蓋 package 漏檔。

## 4. Package gates

D6 新增以下自動驗證：

- normalized ordinal `.nupkg` inventory snapshot；
- NuGet metadata、`DotnetToolSettings.xml`、local manifest 與 CLI identity 一致；
- pack-time gate 拒絕覆寫 `PackageId`、`PackageVersion`、`ToolCommandName`、`PackAsTool` 或
  target framework 所造成的 contract drift；
- 六份 policy、Runtime descriptor 與 compiler reference 全部存在且沒有重複 layout；
- packaged descriptor/policies/reference 的 SHA-256 符合 D5 manifest provenance 與 descriptor；
- package 不含 FHIR fixtures、test/source、`bin/obj` 路徑或額外 SDK implementation assembly；
- package payload 不含本機 repository absolute path；
- 同一輸入 pack 兩次後，所有產品 payload path/hash 相同；
- 從 repository 外目錄 restore local manifest，執行 `--help`、invalid arguments、primitive mode
  與 selected model mode。

Packaging tests 不沿用 Debug test configuration；compiler reference 與被封裝的 CodeGen 一律由
Release build 產生，避免混合 Debug CodeGen 與 Release Runtime reference 的假通過。

NuGet container 自動產生的 `[Content_Types].xml`、`_rels/.rels` 與
`package/services/metadata/core-properties/<generated>.psmdcp` 包含 container relationship／隨機
entry name，因此 two-pack byte gate 明確排除這三類 container metadata；它們仍須出現在
normalized inventory。tool binaries、runtimeconfig/deps、policies、descriptor、reference、readme、
nuspec 與 tool settings 全部納入 payload hash 比較。

## 5. D7 邊界

D6 完成單次 build 的 package content 與 package-only execution gate。Windows/Ubuntu CI artifact
上傳、完整 831-model clean-environment generation、舊版升級/rollback，以及 `.NET`/TFM 升級後
重新 staging compiler reference 與更新 descriptor/hash 的流程仍由 D7 負責。

任何 public NuGet publish 前必須另有 fail-fast release gate，確認團隊已核准正式 license、
NuGet license metadata 與決策一致，且必要 license/notice assets 已包含於 package；任一條件缺少
都不得發布。目前產生的 `.nupkg` 只供 repository-local development 使用。
