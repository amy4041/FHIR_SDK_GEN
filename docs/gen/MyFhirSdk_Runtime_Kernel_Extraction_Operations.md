# Runtime kernel migration 與操作

Version 1.0

適用 current Tool/CodeGen `1.2.0`、`runtime-kernel-v2`、FHIR R5 `5.0.0`、`net9.0`。
架構／ownership／後續 owner 見 [handoff](MyFhirSdk_Runtime_Kernel_Extraction_Handoff.md)。

## 1. Consumer migration

所有 consumers 與相依 DLL 一起 clean restore／build／test，產生同一套部署。沒有 type forwarders。
namespace 與大部分 member shape 保持不變，但 14 個 kernel declarations 的 defining assembly
由 `MyFhirSdk` 改為 `MyFhirSdk.Runtime`；舊 IL 的 assembly binding 不因 namespace 相同而自動遷移。

例如 `MyFhirSdk.Core.Resource` 的 assembly-qualified name 改為：

```text
MyFhirSdk.Core.Resource, MyFhirSdk.Runtime, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
```

`Patient` 等 generated models、primitive wrappers 與 `SimpleQuantity` 仍定義於 `MyFhirSdk`。
檢查自訂 reflection、`Type.GetType`、persisted AQN、plugin loading、mapping 與 cache：
只更新搬移型別的 assembly portion，依保存格式建立明確 migration；不把所有 SDK 型別改指 Runtime。
`IPrimitiveValueAccessor` 是 public SPI，primitive codecs／registry／supported format rules 仍屬 SDK。

ProjectReference 消費 `MyFhirSdk.csproj` 可透過相依圖取得 Runtime。Explicit DLL references
須同時提供 SDK 與 Runtime implementation（其餘 dependencies 由實際 consumer 決定）：

```xml
<ItemGroup>
  <Reference Include="MyFhirSdk">
    <HintPath>$(SdkAssemblyPath)</HintPath>
  </Reference>
  <Reference Include="MyFhirSdk.Runtime">
    <HintPath>$(RuntimeAssemblyPath)</HintPath>
  </Reference>
</ItemGroup>
```

部署至少含 `MyFhirSdk.dll`、`MyFhirSdk.Runtime.dll`、重編的 application／相依 library、
該 application 的 `.deps.json`／`.runtimeconfig.json` 與其他 declared dependencies。
K3 fixture 另包含 `RebuiltLibrary.dll`／`RebuiltConsumer.dll`。不得把 compiler reference DLL 部署為 Runtime。
執行命令（repo root、PowerShell 7.2+，每次使用全新 output）：

```powershell
./eng/Test-KernelConsumerRebuild.ps1 -OutputDirectory artifacts/manual-kernel/rebuild
```

此 runner 驗證所有 consumer inventory、七個 test projects 的 strict TRX、重編相依 DLL、
獨立部署及移除 Runtime 的預期失敗。K0 frozen consumer 只對歷史 SDK 驗證。

## 2. Canonical CodeGen assets

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Restore
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
dotnet tool restore --add-source artifacts/packages --ignore-failed-sources
dotnet myfhir-codegen --help
```

Pipeline 由 Runtime Release `GetCompilerReferenceAsset` 的 `$(TargetRefPath)` 取得 metadata-only
reference，明確注入 CodeGen build／pack。SDK deployable Runtime 來自正常 implementation build。
只 build `CodeGen.csproj` 不會隱含建置 SDK／Runtime；explicit asset 必須先準備好。
TFM／assembly version 以 `Directory.Build.props` 為準，不複製其他平台/configuration 的 DLL。

Descriptor 與 policies、reference、真實 `SimpleQuantity.cs` auxiliary source 均在 tool package
中封裝。CLI override > package default，沒有 repository／cwd／SDK bin/obj fallback。
`--runtime-reference` 只接受單一 canonical reference；`--runtime-contract` 不能繞過 packaged matrix。
`--compiler-source` 必須符合 SimpleQuantity logical identity／normalized hash。完整 CLI 與
repository 外 local-feed 安裝步驟見 [CodeGen README](../../CodeGen/README.md)。

生成輸出先放 staging，檢查 831 model/composition + 21 primitive sources、兩份 manifests，再
以 `Invoke-CodeGenToolSmoke.ps1 -ExportGeneratedSources` 接入 `Test-GeneratedSdkIntegration.ps1`。
K6 的可重現指令見 [Guide K6](MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md)。
只有 models CLI 成功不足以證明 SDK composition 已完成語意編譯；兩平台 fresh SDK gates 都必須通過。

## 3. Troubleshooting

| 狀況 | 檢查與處置 |
| --- | --- |
| CS0012／搬移型別無法解析 | 重編所有 dependent DLL；explicit reference 同時指向 SDK／Runtime implementation |
| Runtime FileNotFoundException／AQN 仍指 SDK | 核對部署 Runtime、載入路徑與搬移型別 AQN；保留診斷後重新部署完整同批輸出 |
| Reference assembly cannot be loaded | 誤部署 compiler-only DLL；改用正常 Runtime implementation output |
| FSG0110–FSG0119／FSG0120–FSG0129 | 核對 descriptor/matrix/TFM/reference identity/hash，重跑 canonical pipeline，不加入額外 SDK reference |
| FSG0130–FSG0139 | 核對 packaged auxiliary source、UTF-8 無 BOM／LF normalization、identity/hash；使用匹配資產重新 pack |
| Fresh SDK source staging 失敗 | 檢查 hidden/dotfiles、reparse points、source inventory；Get-Item／Copy-Item 必須支援隱藏檔。不得以原 bin/obj 補齊 |
| NuGet restore 失敗 | Fresh SDK cache 必須獨立，提供可用的 NuGet feed／network；不把舊 SDK binaries 複製進 staging |
| Missing-Runtime gate 結果不符 | 讀取 `missing-runtime-run.json` 的 exitCode／stdout／stderr；檢查真實 dependency 與 fallback；不略過此 gate |
| Windows／Ubuntu output 不同 | 先核對相同 canonical package、FHIR／policy／reference provenance，再比較生成 hashes；不要比較各自 implementation DLL bytes |
| External Client smoke skipped | 設定測試 FHIR server 的 `MYFHIRSDK_INTEGRATION_BASE_URL` 再跑 Client tests；未設定時明確記錄未執行 |

保留 `evidence.json`、`consumer-rebuild-evidence.json`、`consumer-inventory.json`、
`current-consumer-run.json`、`deployment-run.json`、`missing-runtime-run.json`、build log 與 TRX。
暫存 source／cache 可以在保存診斷並驗證絕對路徑後清理。`eng/*.ps1` 是保留的 CI/operation tools。

## 4. 發布前 source rollback

Rollback 單位是同一 source revision 的 SDK ownership、seams、consumers／相依 DLL、descriptor、
compiler reference、policies、tool pin 與生成 manifests。單獨降 tool 版本不會還原 Runtime extraction。
固定回復基準在 `eng/kernel-migration-baseline.json`：post-D `1.1.0` source revision
`1a28f01d8a4c3aeea46c63da875d01594aeee086`，monolithic SDK、descriptor schema 1／manifest schema 2。

演練使用隔離 source archive，不改寫 main、目前分支或使用者資料。需要 Git 中保留該歷史 commit、
`global.json` 指定的 .NET SDK、PowerShell 7.2+、NuGet network，以及 tracked R5 fixtures。
在 repo root 使用全新 output（本機 portable pwsh 時先設定 `MYFHIRSDK_TEST_PWSH`）：

```powershell
./eng/Test-KernelSourceRollback.ps1 -OutputDirectory artifacts/manual-kernel/rollback
```

Runner 先以固定 source pin 重建 K0 baseline、比對 SDK/reference/descriptor 與 frozen inventories，
重跑 current regression、primitive tgz/directory equivalence 與 installed old-tool smoke；再額外執行
**restored OLD source 的完整 solution regression**，驗證歷史 consumer + rebuilt monolithic SDK
部署不需要 Runtime DLL。Current／restored TRX 與 counts 分開記錄，不把 K0 的 current regression
誤當作 old-source regression。若已於同一 CI job 完成 full K0 gate，可避免重建一次：

```powershell
./eng/Test-KernelSourceRollback.ps1 `
  -BaselineDirectory artifacts/kernel-k0 `
  -OutputDirectory artifacts/manual-kernel/rollback
```

Reuse 模式仍要求九個 K0 gates 全 passed、正確 pin 與 raw hashes／inventories，並核對 consumer
binary hash。K0 regression 記錄 tracked／未被忽略的 untracked working-tree files 與 integration
endpoint 的 fingerprint；K7 只在 fingerprint 相同時重用 TRX，再以七個 projects 的 strict parser
驗證結果。Fingerprint 缺少或不符時重新測 current solution，記錄 currentSourceRegression gate
與 `currentRegressionOperation`。執行前後 fingerprint 必須一致；不直接信任舊 JSON 的統計。
部署 consumer 必須符合 K0 `oldConsumerSha256`，且輸出預期的 primitive／model/base／JSON
round-trip 成功訊息；只以 exit code 0 不足以通過。這些檢查適用 default 與 reuse 模式。
不同 nupkg ZIP container bytes 不作為 rollback 驗收條件，內部 inventory／identity/hash 仍須一致。

實際需要回退 main 時，先保留當前 source revision 與 CI 證據，在新分支依逆 dependency 順序建立
可 review 的 revert commits／PR。核對每個 merge／squash commit 的範圍；merge revert 需選正確
mainline parent。回復所有受影響 consumer source、project/deployment 設定、generated manifests、
SDK ownership 與 post-D tool/descriptor/reference，從乾淨 build 重編整批輸出；不要只刪 Runtime DLL。
由保留的 K0 delivery harness／controller checkout 重新驗證回復候選，並執行該候選 source 的 tests。
本演練不替使用者執行 main revert 或對外發布。

公開 release 後的 rollback/promotion 另由 Release policy 核准，見 handoff 後續 register。
Directory input 退場與 packaged policy default 同樣另案決策；required `--policy` 維持不變。
