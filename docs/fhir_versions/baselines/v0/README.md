# V0 baseline evidence

日期：2026-10-13。G0 本機 **Passed**：R5 regression 為 **922 passed／0 failed／1 skipped**，
七個預期 test assemblies 均有 TRX。完整測試數以 [summary.json](summary.json) 為準。
這是 Windows baseline 證據；尚未宣稱 R4 production 支援、V1 Runtime 可行性或 CI／跨平台通過。
Architecture decisions 見 [V0 Decisions](../../V0_Decisions.md)。

## 固定來源與 artifacts

基準 Git revision 為 `ca0c27f81adb729657da8ddab61c71c70e113984`，branch 為 `feature/fhir-r4-v0`。
runner 執行於含 V0 新增檔案的 working tree；不能只用 HEAD 代替這次來源。
[source-inventory.json](source-inventory.json) 固定每個來源的 path、bytes 與 SHA-256，summary
固定 run 前後一致的 source fingerprint，以及 runner／decisions hashes。Published baseline 目錄排除
於 fingerprint，避免摘要自我引用；其他 V0 files 均包含在來源盤點。

| 證據 | 用途 |
| --- | --- |
| [summary.json](summary.json) | SDK／TFM／OS／PowerShell／temp 設定、source／tool／reference hashes、七個 assembly 的 TRX hashes／test counts／skip reason |
| [policy-inventory.json](policy-inventory.json) | Descriptor、R4／R5 locks、現有 R5 policies 的來源 hashes |
| [r5-generated-inventory.json](r5-generated-inventory.json) | 全部 committed R5 generated sources／manifests，逐檔 hash 與 bytes |
| [approved-api-inventory.json](approved-api-inventory.json) | 既有 public／R5 model API snapshots 的固定 hash；實測由 regression 核對 |
| [public-types.txt](public-types.txt) | 實際 SDK／Runtime defining assemblies、exported types、base／interface graph |
| [project-inventory.json](project-inventory.json) | Evaluated Compile、ProjectReference、DLL Reference／HintPath；包含 current 與歷史 consumers |
| [consumer-findings.json](consumer-findings.json) | R5 defaults、Client concrete resources、reflection／AQN 與 SimpleQuantity 的 source locations |
| [r4-definitions.json](r4-definitions.json) | 官方 R4 metadata、655 個 definitions 的 hash、scope disposition／reason、metadata occurrences 與 SimpleQuantity usage |
| [r5-definitions.json](r5-definitions.json) | 同方法盤點固定 R5 package，供 V1 實際版本差異比較 |
| [evidence-files.json](evidence-files.json) | 上述 portable inventories／summary 的逐檔 hashes |

本次量測 SDK／Runtime 合計 908 個 exported types、R5 committed generated `.cs` 共 852 個。
這些是目前 R5 基準，不作為 R4 的預期數量。Portable evidence 約 1.65 MB，另有 4.53 MB R4 fixture。

完整 compiled public API（含 members／nullability，約 4.98 MB）留在 ignored run directory 的
`public-api.txt`；summary 固定 SHA-256，runner 重複生成確認 deterministic。
Repository 已有 `Tests/Architecture/ApprovedPublicApi.txt` 與 `ApprovedR5ModelApi.txt`，這裡固定 hashes
並提交 defining assembly／base／interface inventory，不再複製完整 API members。
正式 CI artifacts 應保存整個 run directory；本機 TRX／logs／DLL／nupkg 未提交。

## R4 input 與能力

來源為 [HL7 R4 官方下載](https://hl7.org/fhir/R4/downloads.html) 的 core package；
[lock](../../../../CodeGen/Policy/r4-package-lock.json) 與 committed fixture 固定 archive
`hl7.fhir.r4.core#4.0.1`，bytes `4531911`，SHA-256：

```text
b090bf929e1f665cf2c91583720849695bc38d2892a7c5037c56cb00817fb091
```

| R4 definitions | 量測結果 |
| --- | ---: |
| Primitive specializations | 20 |
| Complex-type specializations | 40 |
| Resource specializations | 147 |
| 全部 StructureDefinitions | 655 |
| 待 V4 policy 的 model generation candidates | 202 |
| 待 V1 的 Runtime bootstrap candidates | 7 |
| SimpleQuantity version auxiliary | 1 |
| 排除 base model generation 並保留理由 | 445 |

以上是官方 definitions／候選 scope，尚非 CLR source、factory 或已執行規則數。
Bootstrap 分類與 `Runtime/KernelCompileItems.props` 的實際 ownership 對照；R4 `Element`／`Resource`
雖未標記 specialization，仍列入 V1 candidates。R5 的 11 個 bootstrap definitions 亦與既有 ownership
policy 完全一致，包含 `Base`／`DataType`／`BackboneType`／`PrimitiveType`。
必要 capability inventory 見 [V0 Decisions 的能力表](../../V0_Decisions.md#r4-scope-與-validation-能力)：
R4 models／JSON／validation 現況均為尚未支援，terminology／FHIRPath 等為 deferred。
R4 `Attachment.size` input 是 `unsignedInt`，R5 input 是 `integer64`，inventory 保留實際值供 V1 測試。

## 執行與重現

前置條件：Git、Python 3.10+、SDK 9.0.317、PowerShell 7.2+。本次使用 Python 3.13.13／PowerShell 7.5.0。
先完成 dependencies restore，再使用 fresh output directory：

```powershell
dotnet restore MyFhirSdk.sln
python -m unittest discover -s Tests/FhirVersions -v
python eng/Export-FhirVersionBaseline.py --output artifacts/fhir-versions/v0/new-run
```

本機缺少 PowerShell 7 時可在 ignored tree 安裝；此版本使用現有 .NET 9，不升級 SDK／TFM：

```powershell
dotnet tool install PowerShell --version 7.5.0 --tool-path artifacts/fhir-versions/tools
$env:PATH = (Join-Path (Get-Location) 'artifacts/fhir-versions/tools') + [IO.Path]::PathSeparator + $env:PATH
New-Item -ItemType Directory -Force artifacts/fhir-versions/temp | Out-Null
$env:TEMP = Join-Path (Get-Location) 'artifacts/fhir-versions/temp'
$env:TMP = $env:TEMP
python eng/Export-FhirVersionBaseline.py --output artifacts/fhir-versions/v0/new-run
```

`--inventory-only` 不執行 regression，G0 永遠 Pending。Runner 拒絕既有 output、artifacts 外 output、
錯誤 hash／metadata、canonical reference 不符、API 不 deterministic、缺少預期測試 assembly、TRX
counter 不一致及執行期間 source 改變。9 個 Python integrity tests 驗證 input tampering、版本錯配、
missing test evidence、skipped counter、bootstrap ownership 與 consumer DLL references；
不計入既有 923 個 R5 solution tests。

## Code review 修正

兩項 P2 findings 已修正並重新產生本目錄 evidence：

- Bootstrap 依各版本官方 definitions 與 Runtime compile ownership 分類，根型別不再因缺少 derivation
  被排除；每個 disposition 使用對應的理由。
- Consumer evaluation 提供明確 `SdkAssemblyPath`／`RuntimeAssemblyPath`，記錄 portable evaluation
  設定、有效 DLL HintPath 與條件式 Runtime Reference；缺少 implementation HintPath 時明確失敗。
  `Reference.FullPath` 是 MSBuild item path，盤點改以 HintPath 表達實際 DLL。

`project-inventory.json` 的歷史 fixture entry 是使用目前 Release implementation paths 的相依盤點，
不代替歷史 binary compatibility run，也沒有修改 fixture sources。新的 raw run 位於
`artifacts/fhir-versions/v0/review-fixes/`；舊 runs 保留於 ignored artifacts，不能重用為修正後的 gate 證據。

## 初次執行診斷與限制

最初 solution regression 為 917 passed／5 failed／1 skipped；5 個失敗是找不到 `pwsh`。
安裝 7.4.6 後發現缺少其所需 .NET 8 runtime，改用與現有 .NET 9 相符的 7.5.0。
下一次完整 runner 為 921 passed／1 failed／1 skipped，失敗是既有 writer test 在系統 TEMP 的 staging
目錄遇到 Access denied；[prior-failed-run.json](prior-failed-run.json) 保留該次 diagnostic summary，
不能當作通過 gate 或重用為最終證據。最後一次使用 ignored project temp directory；實際結果見 summary。

外部 R5 Client smoke 使用 `MYFHIRSDK_INTEGRATION_BASE_URL`，token 由
`MYFHIRSDK_INTEGRATION_BEARER_TOKEN` 提供。未設定 endpoint 時記錄 skipped；本次未驗證外部 server
identity／FHIR version。受控 Client HTTP tests 不代替外部整合。R4 Client／IG 仍未實作。

V0 完成後的下一個工作包為 V1 selected closure 實驗；合併前仍需 code review 與本分支 CI。
K0–K7 歷史基準未覆寫。
