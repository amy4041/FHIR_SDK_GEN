# MyFhirSdk CodeGen local tool

`MyFhirSdk.CodeGen.Tool` 是從固定 FHIR R5 package 與 generation policies 產生
deterministic C# source 的 repository-local .NET tool。命令為 `myfhir-codegen`。

## 支援契約

| 項目 | 支援值 |
| --- | --- |
| Tool / CodeGen | `1.1.0` |
| .NET target framework | `net9.0` |
| FHIR package | `hl7.fhir.r5.core#5.0.0` |
| FHIR version | `5.0.0` |
| Runtime contract | `phase-a-v1+c4-primitives-v1` |
| Primitive policy | `1.1.0` |
| Compatibility policy | exact ordinal match |
| Model/primitive manifest schema | `2` |

`CodeGen/Compatibility/GenerationCompatibilityMatrix.cs`、packaged
`Contracts/runtime-contract.json` 與 manifest 中的 `compatibility` object 是可稽核的契約。
不相容組合會在讀取完整 definitions、render 或寫檔前失敗。

## Build、pack 與 restore

在 repository root 執行：

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Restore
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
dotnet tool restore --add-source artifacts/packages --ignore-failed-sources
dotnet myfhir-codegen --help
```

packaging build 先建立 canonical compiler-only `MyFhirSdk.dll`，再明確注入 CodeGen package；
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

## 產生 primitives

primitive mode 建議直接讀取本機FHIR `.tgz`，並保留 required explicit `--policy`：

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
`Tests/CodeGen/Fixtures/StructureDefinitions/Primitives/R5`即可。兩種模式都必須提供`--policy`，
在相同definitions與policy下產生逐位元相同的wrappers、registry與schema v2 manifest。
工具只接受existing directory或本機`.tgz`，不下載URL、不搜尋cache，也不將archive解壓至filesystem。
Package identity必須符合命令中的package ID/version/FHIR version。

## Package assets 與 override 規則

package 內含：

- model 與 primitive generation policies；
- versioned Runtime contract descriptor；
- compiler-only Runtime reference；
- CodeGen executable、Roslyn dependencies 與 tool metadata。

asset precedence 固定為「明確 CLI override > package-owned default」，沒有 environment、
repository 或 current-directory fallback：

- `--policy-root <directory>`：整組 model policies；不可與 packaged model policies 混用；
- `--policy <file>`：primitive mode 的 required policy，或 model mode 的 primitive policy override；
- `--runtime-contract <file>`：Runtime descriptor；
- `--runtime-reference <file>`：可重複的 compiler references。

override 仍必須符合 packaged compatibility matrix、assembly identity、TFM 與 SHA-256。

## Output 與 manifest

writer 拒絕 filesystem root、tool installation、input archive、policy、descriptor、Runtime
reference、rooted artifact、`..` traversal 與 path collision。寫入採 staging、atomic swap 及
rollback；失敗或取消不應破壞既有 output。tool 不推測 repository root，因此在 repository
內操作時必須使用 staging directory 並人工審查變更。

schema v2 manifest 記錄 package/policy hashes、artifact inventory，以及：

- tool package ID/version 與 CodeGen version；
- Runtime descriptor schema/version/SHA-256；
- compiler reference logical identity/SHA-256；
- target framework 與 exact compatibility policy。

manifest 不記錄實體 repository、cache 或 temporary path。

## 升級與 rollback

`1.1.0`新增primitive `.tgz` input。`1.0.0`是immutable upgrade baseline，仍使用directory input。
升級測試由`eng/codegen-tool-upgrade-baseline.json`固定的source revision重建真實舊版package，
不修改目前source版本來模擬舊版。版本提升時：

1. 同步更新 package、CodeGen、compatibility matrix、descriptor 與 local manifest 版本；
2. 更新 Runtime/TFM 時重新建立 compiler reference，並同步 descriptor identity/hash；
3. 保留 `eng/codegen-tool-upgrade-baseline.json` 指向可重建上一版 package 的 immutable commit；
4. 讓 CI 執行真實舊版安裝、`dotnet tool update`、generation 與 manifest 驗證；
5. generation contract 未變時，新舊 generated source 必須 byte-identical。

rollback 只需把 local manifest pin 回上一個可用 tool version，從受信任來源 restore，重新產生
staging output 並驗證；不需要回復 handwritten Runtime/models。若新版本已寫入 output，先保留
或還原版本控制中的 committed generated artifacts，再用上一版 tool 重建，不直接修改 manifest。

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

## Troubleshooting

- `NU1101` 或 tool restore 找不到 package：確認 `artifacts/packages` 含唯一 `.nupkg`，並以
  `--add-source artifacts/packages` restore。
- `FSG0110`–`FSG0119`：Runtime reference 缺失、損壞、identity、TFM 或 hash 不符；重新執行
  packaging build，不要從其他機器或 configuration 複製 DLL。
- `FSG0120`–`FSG0129`：版本相容性失敗；核對上方 matrix、FHIR package 及 policy version/hash。
- `FSG0130`–`FSG0139`：package asset 缺失或 layout 錯誤；重新 pack 並執行 package tests。
- `FSG0011`：output 與 tool/input/contract/policy/reference 重疊，或 artifact path 不安全；改用
  獨立 staging directory。
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

此 package 僅供 repository-local development。公開發布前必須另外核准 license、package
license metadata、signing、SBOM、artifact provenance、feed credentials 與 release promotion；
任何缺項都必須阻擋公開 NuGet push。
