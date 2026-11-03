# MyFhirSdk CodeGen local tool

`MyFhirSdk.CodeGen.Tool` 是從固定 FHIR R4／R5 package 與 generation policies 產生
deterministic C# source 的 repository-local .NET tool。命令為 `myfhir-codegen`。

## 支援契約

| 項目 | 支援值 |
| --- | --- |
| Tool / CodeGen | `1.3.1` |
| .NET target framework | `net9.0` |
| FHIR package | `hl7.fhir.r4.core#4.0.1`／`hl7.fhir.r5.core#5.0.0` |
| FHIR version | `4.0.1`／`5.0.0` |
| Runtime contract | `runtime-kernel-v2` |
| Primitive policy | R4 `1.0.1`／R5 `1.1.0` |
| Compatibility policy | exact ordinal match |
| Model/primitive manifest schema | `3` |

`CodeGen/Compatibility/GenerationCompatibilityMatrix.cs`、packaged
`Contracts/runtime-contract.json` 與 manifest 中的 `compatibility` object 是可稽核的契約。
不相容組合在寫檔前失敗；archive 先保留既有 loader／結構 diagnostic，再驗證鎖定的 package hash。

## Build、pack 與 restore

在 repository root 執行：

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Restore
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
dotnet tool restore --add-source artifacts/packages --ignore-failed-sources
dotnet myfhir-codegen --help
```

packaging build 先建立 canonical compiler-only `MyFhirSdk.Runtime.dll`，再明確注入 CodeGen package；
CodeGen project 本身沒有 SDK `ProjectReference`。已安裝的 tool 從 package installation root
解析資產，不搜尋 solution、repository、目前工作目錄或 `bin/obj`。

工具的 runtimeconfig 啟用 `System.IO.Compression.UseStrictValidation`，使尾端截斷的
gzip archive 回報 `FSG0026` 並保留既有輸出。此設定須在程序第一次使用壓縮串流前生效；
直接呼叫 pipeline 的測試 host 也使用相同設定。Loader 仍使用 .NET tar/gzip 串流，
並在 tar 結束後讀完 gzip，以完成 trailer 驗證。

## 產生完整 R5 models

建議一律先輸出到 staging directory，驗證 diff 後才更新 committed output：

```powershell
dotnet myfhir-codegen `
  --mode model `
  --input Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz `
  --output artifacts/manual-generation `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
```

省略 `--canonical` 代表完整 scope；重複傳入 `--canonical <url>` 可產生 selected closure。
完整 model batch 包含 831 個 model source artifacts，加上一份 manifest。

K0–K6 已完成最小 Runtime extraction：production Runtime assembly 與 SDK 分開；current
package 使用 canonical Runtime compiler reference 與 SimpleQuantity auxiliary source。
SDK consumer migration、defining assembly/AQN、部署與整套 source rollback 見
[Runtime operations](../docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Operations.md)。
Hashes 與歷史基準界線見 [kernel handoff](../docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Handoff.md)
及 [accessor contract evidence](../docs/gen/baselines/kernel-accessor/README.md)。公開 release 仍未核准。

SDK migration 採所有 consumers／相依 DLL 全面重編，不提供 type forwarders。K3 runner
`eng/Test-KernelConsumerRebuild.ps1` 驗證 clean solution regression、重編相依 DLL 鏈、獨立部署
與缺少 Runtime 的失敗；不改 CodeGen metadata-only 邊界或 K4 compiler asset 遷移。
操作見 [migration consumers](../Tests/KernelMigration/README.md)。

K1 已將驗證責任拆分：CLI 以同一 input／policy 新生成的 primitive wrappers 與 embedded 真實
`SimpleQuantity.cs` 編譯 models；metadata／validation composition 做 IR、mapping 與結構檢查。
CLI 成功的完整整合保證須再由 SDK build／CI 編譯此次全部生成結果，並執行 runtime tests。
K1 compiler-only reference 暫時維持既有 hash／manifest；部署的 SDK 已移除 generator friend，
K2 已建立 Runtime assembly，reference／auxiliary provenance 遷移仍在 K4。詳見
[K1 實作與證據](../docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md#k1-實作進度與-reference-surface-證據2026-10-07)。

## 產生 primitives

primitive mode 建議直接讀取本機 FHIR `.tgz`；可明確指定 `--policy`，省略時使用 packaged release policy：

```powershell
dotnet myfhir-codegen `
  --mode primitive `
  --input Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz `
  --policy CodeGen/Policy/primitive-generation-policy.json `
  --output artifacts/manual-primitives `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
```

既有flat-directory input仍受支援：將`--input`改為
`Tests/CodeGen/Fixtures/StructureDefinitions/Primitives/R5`即可。兩種 input 都可明確指定`--policy`，
在相同definitions與policy下產生逐位元相同的wrappers、registry與schema v3 manifest。
工具只接受existing directory或本機`.tgz`，不下載URL、不搜尋cache，也不將archive解壓至filesystem。
Package identity必須符合命令中的package ID/version/FHIR version。

## Package assets 與 override 規則

### 在 repository 外操作

準備 .NET 9 SDK、一份已驗證的 `MyFhirSdk.CodeGen.Tool.1.3.1.nupkg`、
`hl7.fhir.r5.core-5.0.0.tgz`，以及同一版本 source 的
[`primitive-generation-policy.json`](Policy/primitive-generation-policy.json)。
交付時將 policy 原樣複製成獨立檔案；primitive mode 即使安裝包內有 policy，仍要求
`--policy`。不可自行刪減 policy entries 或只改 version，因為工具也驗證內容 hash。

在新的工作目錄準備以下配置（`feed` 僅放本次核准的 package）：

```text
work/
  NuGet.Config
  feed/MyFhirSdk.CodeGen.Tool.1.3.1.nupkg
  inputs/hl7.fhir.r5.core-5.0.0.tgz
  inputs/primitive-generation-policy.json
```

將以下內容存為 `work/NuGet.Config`，清除繼承的 package sources，只使用本機 `feed`。
`./feed` 相對於此設定檔所在目錄解析：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-tool" value="./feed" />
  </packageSources>
</configuration>
```

於 `work` 執行下列 PowerShell 指令；工具不會替使用者下載 FHIR archive。
使用獨立的 NuGet packages 目錄，避免重用其他來源已快取的同版本 package；
後續 restore 也應在設定此環境變數的 shell 執行：

```powershell
dotnet new tool-manifest
$env:NUGET_PACKAGES = Join-Path (Get-Location).Path '.nuget/packages'
dotnet tool install MyFhirSdk.CodeGen.Tool --local --version 1.2.1 --configfile ./NuGet.Config
dotnet myfhir-codegen --help
dotnet myfhir-codegen `
  --mode primitive `
  --input ./inputs/hl7.fhir.r5.core-5.0.0.tgz `
  --policy ./inputs/primitive-generation-policy.json `
  --output ./generated-primitives `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
```

預期產物為 20 個 wrappers、1 個 registry composition 與 1 份 manifest，共 22 個檔案；
`xhtml` 維持 policy 明確列出的 unsupported primitive。再次執行應得到相同 bytes。
已有 pin 為 `1.2.1` 的 manifest 時，省略建立 manifest 與 install，改用
`dotnet tool restore --configfile ./NuGet.Config`。若仍 pin 舊版，先依下方升級流程更新版本。

### 資產解析

package 內含：

- model 與 primitive generation policies；
- versioned Runtime contract descriptor；
- compiler-only Runtime reference（Release `$(TargetRefPath)`，只讀 PE metadata）；
- 真實 SDK-owned `SimpleQuantity.cs` auxiliary source（`Assets/CompilerSources/SimpleQuantity.cs`）；
- CodeGen executable、Roslyn dependencies 與 tool metadata。

asset precedence 固定為「明確 CLI override > package-owned default」，沒有 environment、
repository 或 current-directory fallback：

- `--policy-root <directory>`：整組 model policies；不可與 packaged model policies 混用；
- `--policy <file>`：primitive mode 的 required policy，或 model mode 的 primitive policy override；
- `--runtime-contract <file>`：Runtime descriptor；
- `--runtime-reference <file>`：單一 Runtime compiler reference；重複 identity 會拒絕；
- `--compiler-source <file>`：model compilation 的 `SimpleQuantity.cs` source override。

override 仍必須符合 packaged compatibility matrix、assembly identity、TFM 與 SHA-256。
K4 matrix 固定 MyFhirSdk.Runtime 的 name/version/public key token 與 canonical reference SHA-256；
--runtime-contract 不能藉由一起改寫 descriptor identity/hash，重新接受舊 SDK DLL。

## Output 與 manifest

writer 拒絕 filesystem root、tool installation、input archive、policy、descriptor、Runtime
reference、rooted artifact、`..` traversal 與 path collision。寫入採 staging、atomic swap 及
rollback；失敗或取消不應破壞既有 output。tool 不推測 repository root，因此在 repository
內操作時必須使用 staging directory 並人工審查變更。

schema v3 manifest 記錄 package/policy hashes、artifact inventory，以及：

- tool package ID/version 與 CodeGen version；
- Runtime descriptor schema/version/SHA-256；
- compiler reference logical identity/SHA-256；
- compiler source logical name、version、CLR type、UTF-8/LF normalization 與 SHA-256；
- target framework 與 exact compatibility policy。

manifest 不記錄實體 repository、cache 或 temporary path。
primitive manifest 也不記錄 input mode 或 archive exact-bytes SHA-256；上述 equivalence
是同一工具版本下的完整產物相等。跨 `1.0.0`／`1.1.0` 時 sources 相同，但 manifest 的
tool/CodeGen version 與 Runtime descriptor hash 不同，不應要求整份 manifest 相同。

## 升級與 rollback

`1.2.0` 將 compiler reference 切換為 Runtime，descriptor schema 升為 2，manifest schema 升為 3；
公開生成 C# API 維持一致。Auxiliary source hash 採 UTF-8 無 BOM、換行正規化為 LF；
檔案遺失、UTF-8 損壞或 hash 不符會以 FSG0130/FSG0131 失敗並保留既有 output。

`1.1.0`新增primitive `.tgz` input。`1.0.0`是immutable upgrade baseline，仍使用directory input。
升級測試由`eng/codegen-tool-upgrade-baseline.json`固定的source revision重建真實舊版package，
不修改目前source版本來模擬舊版。版本提升時：

1. 同步更新 package、CodeGen、compatibility matrix、descriptor 與 local manifest 版本；
2. 更新 Runtime/TFM 時重新建立 compiler reference，並同步 descriptor identity/hash；
3. 保留 `eng/codegen-tool-upgrade-baseline.json` 指向可重建上一版 package 的 immutable commit；
4. 讓 CI 執行真實舊版安裝、`dotnet tool update`、generation 與 manifest 驗證；
5. generation contract 未變時，新舊 generated source 必須 byte-identical。

Tool-only rollback 將 local manifest pin 回上一個可用版本，從受信任來源 restore，使用該版本
匹配的 descriptor/reference/policies 與 SDK source 產生 staging output 並驗證。若新版本已寫入
output，先保留或還原對應版本的 generated artifacts，不直接手改 manifest provenance。
跨 `1.2.0` → `1.1.0` 的 extraction rollback 必須同時回復 SDK ownership、consumer source／
相依 DLL、project/deployment 設定及整組契約，再全面重編；降 tool pin 不會回復 Runtime
assembly boundary。完整程序與隔離演練見 [source rollback](../docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Operations.md#4-發布前-source-rollback)。

```powershell
dotnet tool uninstall MyFhirSdk.CodeGen.Tool --local
dotnet tool install MyFhirSdk.CodeGen.Tool --local `
  --version <previous-version> `
  --add-source <trusted-package-source>
dotnet myfhir-codegen --help
```

正常升級使用：

```powershell
dotnet tool update MyFhirSdk.CodeGen.Tool --local `
  --version <new-version> `
  --add-source <trusted-package-source>
```

CI 的 upgrade smoke 會使用真實前版 package 驗證相同 lifecycle。

降回 `1.0.0` 後必須將 primitive `--input` 改回 flat directory，且仍提供 `--policy`。
回退前先準備與 archive 對應的 primitive definitions；repository 的
`Tests/CodeGen/Fixtures/StructureDefinitions/Primitives/R5` 可供本專案重現。
不要將完整 package 解壓目錄直接當成 primitive-only directory。

## Troubleshooting

- 缺少 `--policy`（exit code 1）：兩種 primitive input 都必須明確提供 policy 檔案；
  `--policy-root` 不能代替它。
- `FSG0026`：archive 無法讀取、損壞、截斷或含不安全／重複 entry；核對
  [`r5-package-lock.json`](Policy/r5-package-lock.json) 的 archive SHA-256，重新取得原始檔案。
- `FSG0027`：package metadata 與 CLI identity 不符；核對 ID、package version、FHIR version。
- `FSG0013`／`FSG0124`：policy 路徑不可讀或契約不符；提供對應版本的原始 policy。
- `FSG0019`／`FSG0020`：沒有可用 primitive、shape 損壞或 identity 重複；檢查診斷指向的
  logical entry。合法 profiles 會被忽略，損壞的 primitive specialization 不會被靜默略過。

- `NU1101` 或 tool restore 找不到 package：確認 `artifacts/packages` 含唯一 `.nupkg`，並以
  `--add-source artifacts/packages` restore。
- `FSG0110`–`FSG0119`：Runtime reference 缺失、損壞、identity、TFM 或 hash 不符；重新執行
  packaging build，不要從其他機器或 configuration 複製 DLL。
- `FSG0120`–`FSG0129`：版本相容性失敗；核對上方 matrix、FHIR package 及 policy version/hash。
- `FSG0130`–`FSG0139`：package asset 缺失或 layout 錯誤；重新 pack 並執行 package tests。
- `FSG0011`：output 與 tool/input/contract/policy/reference 重疊，或 artifact path 不安全；改用
  獨立 staging directory。
- `FSG0011` 含 `rollback failed`：取消後未能還原原 output。先停止其他寫入 output 的程序，
  保留診斷列出的 backup 與目前 output，確認兩者內容後將目前 output 移至另一個保留位置，
  再把 backup 移回原 output 路徑。不要直接刪除 backup 或覆蓋同時寫入的檔案。
- clean environment 與本機結果不同：先清除該測試專用 NuGet/tool cache，再確認使用同一
  `.nupkg`、FHIR fixture 與 CLI arguments。

## 驗證與發布限制

```powershell
dotnet test MyFhirSdk.sln -c Release
pwsh ./eng/Test-CodeGenToolchainContract.ps1
pwsh ./eng/Invoke-CodeGenToolSmoke.ps1 <required arguments>
```

CI 在 Windows/Linux 使用同一 canonical Runtime asset，執行 build、test、pack、isolated tool
smoke、upgrade contract 與 cross-platform generated hash gate。

K6 canonical smoke 使用 `-ExportGeneratedSources`，將這次 installed tool 的完整輸出保留在
smoke output 的 `generated/Generated/R5`。`eng/Test-GeneratedSdkIntegration.ps1` 接收
`-SmokeDirectory`、`-PackagePath`、`-RuntimeReferenceAssetPath`，在 checkout 外的全新 SDK
source tree／NuGet cache 中，以這份輸出執行完整 SDK regression、consumer 重編與隔離部署。
證據分別記錄 models CLI、SDK composition 與 external Client smoke；Windows／Ubuntu
都通過且 provenance／生成檔 hashes 相同，才通過 cross-platform gate。

此 package 僅供 repository-local development。公開發布前必須另外核准 license、package
license metadata、signing、SBOM、artifact provenance、feed credentials 與 release promotion；
任何缺項都必須阻擋公開 NuGet push。

CodeGen packaging tests 會呼叫 PowerShell 7.2+ 執行真實 package inventory script，
驗證 LF/CRLF source 可用，以及 UTF-8 BOM、UTF-16、損壞 UTF-8 會被拒絕。
CI 使用 PATH 中的 pwsh；若本機使用 portable PowerShell，可設定
MYFHIRSDK_TEST_PWSH 為 pwsh 執行檔的絕對路徑後再執行 dotnet test。

V3 將 R5 model ownership 移至 `MyFhirSdk.R5.Models`，共用 primitive codecs／rule implementations 位於 `MyFhirSdk.ModelSupport`。
Tool 1.2.1 同步 ownership policy／descriptor provenance，並排除 host 的 Models／support assemblies 作為 TPA compiler references。
Runtime API 與 canonical compiler reference bytes 不變；852 個 generated C# files 不變，兩份 manifests 更新版本與 hashes。
遷移與驗收見 [V3 baseline](../docs/fhir_versions/baselines/v3/README.md)。

## V5 完整 R4 Models（Tool 1.3.1）

V4 `1.3.0` 已提交並通過 CI。`1.3.1` 修正 R4 choice／validation policies 的統計盤點，
更新對應 descriptor／manifest hashes，維持既有生成 CLI 與 model renderer 契約。
V5 review 修正將 R4 primitive policy 升為 `1.0.1`，移除不適用於 R4 的 decimal 位數上限常數；
正式 R4 provider 使用 R4 lexical bindings，共用執行器由 ModelSupport 提供，原始 wire literals 保持不變。
正式 R4 Models、明確 context 用法與完整來源／能力盤點見
[V5 integration](../docs/fhir_versions/baselines/v5/README.md)。新工具 oracles 位於
`docs/fhir_versions/baselines/v5/tool-1.3.1`；以下 V4 紀錄保留其歷史版本。

## V4 雙版本生成（Tool 1.3.0）

沿用既有 `--fhir-version`、`--package-id`、`--package-version`，不新增隱含 current version。
R4 固定 `4.0.1`／`hl7.fhir.r4.core`；R5 固定 `5.0.0`／`hl7.fhir.r5.core`，拒絕 R4B 與其他版本。
模型與 primitive mode 都可省略 `--policy`，預設使用安裝工具內對應 release 的 policies。
既有明確指定 policy 的 R5 命令仍有效；`--policy-root` 仍僅用於 model mode，指定的是該 release 的 policy 目錄。

```powershell
dotnet myfhir-codegen --mode model --input <r4-package.tgz> --output <output-root> --fhir-version 4.0.1 --package-id hl7.fhir.r4.core --package-version 4.0.1
dotnet myfhir-codegen --mode primitive --input <r4-package.tgz> --output <output-root>/Generated/R4/Primitives --fhir-version 4.0.1 --package-id hl7.fhir.r4.core --package-version 4.0.1
```

Model output 位於 `<output-root>/Generated/R4`，primitive output 直接位於指定目錄；R5 model output 仍是 `Generated/R5`。
命令會替換整個 `--output` 目錄。同一棵目錄應先生成 models，再生成巢狀的 primitives；重新生成 models 後要再執行 primitive 命令。R4／R5 使用各自的 output root，避免互相替換。
R4 namespace 為 `MyFhirSdk.R4.Primitives`／`Types`／`Resources`，composition 位於 `MyFhirSdk.ModelMetadata.R4`。
R4 packaged policies 在 `Policy/R4`，descriptor 在 `Contracts/R4/runtime-contract.json`，輔助 source 在 `Assets/CompilerSources/R4/SimpleQuantity.cs`。
R5 的 asset 路徑保持原樣；兩者使用相同 canonical Runtime reference，descriptor／source／policy hashes 各自固定。
使用 `--runtime-contract`、`--runtime-reference` 或 `--compiler-source` override 時仍驗證精確相容性。

1.3.0 更新兩份 R5 manifests 的 tool／descriptor provenance；852 個 R5 generated C# bytes 維持 V3。
Descriptor schema 2、manifest schema 3 與 Runtime public API 不變。歷史 baseline 不覆寫，新的 tool oracle 位於
`docs/fhir_versions/baselines/v4/tool-1.3.0`。生成／model compile 成功不等於完整 R4 SDK 驗收；正式 R4 Models／provider 與完整 runtime cases 屬 V5。

完整 G4 驗證：`python eng/Test-FhirV4Generator.py --output artifacts/fhir-versions/v4/run`。
