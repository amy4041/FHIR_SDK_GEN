# MyFhirSdk CodeGen primitive `.tgz` input 實作指引

Version 0.3

- 狀態：P3接線與必要的P1/P4變更已在開發分支實作；Decision的正式acceptance與跨平台CI仍需確認
- 實作方案：A（`.tgz` preferred、directory compatible、`--policy` required）
- Baseline：Tool/CodeGen `1.0.0`、primitive policy `1.1.0`、manifest schema v2
- 目標版本：Tool/CodeGen `1.1.0`、primitive manifest維持schema v2
- 決策文件：`docs/gen/MyFhirSdk_CodeGen_Primitive_Tgz_Input_Decision.md`
- 後續階段：`docs/gen/MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md`

## 1. 目標

讓installed local tool在repository-independent目錄直接從FHIR package產生primitive wrappers：

```powershell
dotnet myfhir-codegen `
  --mode primitive `
  --input .\hl7.fhir.r5.core-5.0.0.tgz `
  --policy .\primitive-generation-policy.json `
  --output .\primitives `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
```

既有directory input與required explicit`--policy`保持支援。

## 2. 非目標

- 不將`--policy`改為optional，也不自動解析package-owned primitive policy。
- 不修改primitive wrapper public API或Runtime behavior。
- 不修改13-symbol Runtime contract shape或進行Runtime kernel extraction。
- 不產生complex datatype、Resource或Profile。
- 不新增URL input、network download、NuGet registry resolution或cache fallback。
- 不移除directory input。
- 不修改primitive/model manifest schema。
- 不將FHIR`.tgz`、generated DLL或temporary output提交Git。

## 3. Entry baseline與固定預期

P0必須由current main clean checkout重建並提交文字baseline：

```text
docs/gen/baselines/primitive-tgz-input/
├─ README.md
├─ cli-help-1.0.0.txt
├─ directory-artifact-inventory-1.0.0.txt
└─ directory-artifact-hashes-1.0.0.txt
```

基準至少固定：

- 20個supported generated wrapper sources；
- 1個`PrimitiveRegistry.Composition.g.cs`；
- 1個`primitive-generation-manifest.json`；
- 21個manifest artifact entries，不包含manifest本身；
- wrapper、registry與manifest精確SHA-256；
- primitive decisions、public API與registry ordering；
- Runtime contract、policy與compiler reference provenance。

數量必須由實際pipeline/assertions取得，不能只複製本文件。baseline scripts只提交文字與hash，
不得提交generated DLL或temporary output。

## 4. 工作包

### P0：固定directory與package equivalence baseline

1. 從immutable `hl7.fhir.r5.core#5.0.0` package建立package input fixture/reference。
2. 確認既有directory definitions對應package中的primitive specialization set。
3. 記錄directory mode的完整output inventory/hash、manifest與CLI help。
4. 建立可由test重新產生的baseline helper，不寫machine path或timestamp。
5. 記錄package archive SHA-256於test fixture contract；不加入manifest schema。

Exit gate：現有production code、CLI與generated output不變；`git diff --check`及Phase D gates通過。

P0實作與重現方式見[entry baseline](baselines/primitive-tgz-input/README.md)。
已建立directory/package fixture等價測試、完整產物hash與manifest、CLI help及archive fixture
contract；本機Windows驗證已通過，跨平台CI gate仍由既有workflow確認。Decision維持Proposed，
本次沒有啟用production primitive `.tgz` input。

### P1：抽出共用primitive definition input contract

1. 讓primitive inventory pipeline接受明確input abstraction，而不是只接受directory path。
2. `.tgz` path使用既有`FileDefinitionPackageInput`與`DefinitionPackageLoader`。
3. directory path繼續使用既有flat-directory loader或等價adapter。
4. input classification規則集中在單一元件：existing directory或`.tgz`file；其他輸入立即失敗。
5. 不以exception message、current directory內容或archive content猜測模式。

Exit gate：package loader沒有第二份tar/gzip實作；directory現有正負測試全部保持。

P3接線時補齊`PrimitiveDefinitionInput`與`PrimitiveDefinitionInputKind`：分類集中於`Resolve`，
existing directory優先，其次為existing `.tgz` file；CLI與直接呼叫pipeline都沿用此規則。
`PrimitiveGenerationOptions.InputKind`記錄CLI判定的模式，pipeline再次驗證其與實際input一致。
Coverage pipeline接受typed input；directory沿用原loader，archive重用package loader與P2 selector。

### P2：建立package primitive selector

從validated`LoadedDefinitionPackage`選取primitive specializations，並驗證：

- `resourceType`、`kind`、`derivation`、FHIR version；
- id/type/url/name/baseDefinition；
- snapshot與differential；
- duplicate FHIR type與canonical；
- deterministic ordinal ordering。

合法的complex/resource/profile definitions由selector忽略。primitive-shaped entry若缺required
shape則失敗，不得因selector filter而靜默消失。需要時保留entry的raw classification facts，
避免validation前丟失診斷資訊。

Exit gate：同一package entry順序正反排列得到相同inventory與diagnostics。

P2先行實作採用`PackagePrimitiveSelector.Select(LoadedDefinitionPackage, expectedFhirVersion)`，
回傳`GenerationResult<PrimitiveDefinitionInventory?>`；P2提交時未連接CLI，現由P3接入。
Selector沿用primitive inventory builder的identity與duplicate驗證，補齊id、derivation、
abstract、snapshot/differential與FHIR version檢查，保留`xhtml`供policy判定unsupported。
合法complex/resource及primitive constraint profiles忽略；primitive specialization錯誤不以filter吞掉。
只有已知的non-primitive kind（complex-type/resource/logical）可直接略過；未知kind必須回報，
包含baseDefinition為Element的xhtml。Primitive constraint略過前檢查id/url/type/baseDefinition
非空，不套用specialization的version/snapshot/differential要求。
Loader保留`kind=primitive-type`或`baseDefinition=.../PrimitiveType`的entry，即使resourceType
缺少或錯誤也交由驗證回報；JSON型別錯誤由既有package read diagnostic回報。
沒有primitive時使用`FSG0019`，其他檢查沿用`FSG0002`–`FSG0005`、`FSG0019`–`FSG0020`及
`FSG0026`–`FSG0027`，沒有新增或重編diagnostic code。
`PackagePrimitiveSelectorTests`涵蓋official package/directory inventory一致、缺漏/錯誤欄位、
duplicate type/canonical與archive entry正反順序的inventory/diagnostic一致性。
以下驗證結果為P2提交時的歷史紀錄；P1接點與P3–P4更新見各階段說明。
Decision仍維持Proposed，開發結果不替代決策文件的acceptance gates。

本機Windows驗證（含review修正）：Release build零warning/error；完整solution tests 697 passed、1個既有
external-server integration smoke skipped，其中CodeGen tests 402 passed。
新增regression tests先在修正前重現未知kind與constraint缺漏identity的失敗案例，再驗證修正後
正反archive順序的diagnostic一致；合法profile仍不受specialization shape要求影響。
P0 baseline、canonical pack、toolchain contract及`git diff --check`通過；
Windows/Linux CI仍須由既有workflow確認。

### P3：接入primitive generation pipeline與CLI

1. `--input`接受`.tgz`file或existing directory；help將兩種模式明確列出。
2. `--policy`解析與required validation完全保持，不修改`ToolAssetResolver`default policy行為。
3. `PrimitiveGenerationOptions`攜帶明確definition input kind，不以散落boolean或extension check
   重複判斷。
4. package/directory inventory進入同一coverage、policy join、model builder與renderer流程。
5. output safety context保護實際input archive/directory及explicit policy。
6. writer繼續使用staging、atomic swap與rollback。

Exit gate：`.tgz`與directory產生byte-identical wrappers、registry和schema v2 manifest。

已接入`PrimitiveGenerationPipeline`及CLI help；兩種輸入經過相同policy preflight、coverage、
model builders、renderers與writer。`--policy`仍required，`ToolAssetResolver`的policy precedence未改。
Writer保留staging/atomic swap/rollback，且pipeline會將本次input與explicit policy加入protected
paths，因此直接呼叫pipeline也不能把output設為包含input/policy的目錄。

共用package loader在串流讀取時拒絕rooted/traversal/backslash/empty-segment paths、duplicate
logical entries及links/special files。官方archive含`package/other`、`package/openapi`等合法
附屬檔案，允許其canonical relative paths；沒有新增第二套tar/gzip parser或filesystem extraction。
重複entry即使其中一份JSON損壞，正反順序亦回傳相同diagnostics。

測試覆蓋CLI雙模式與required policy、input分類、實際archive identity mismatch、empty primitive
selection、損壞archive、policy missing/corrupt/hash/version、output保護、repeat generation及完整
output equivalence。Installed-tool smoke新增`.tgz`/directory byte-equivalence與真實舊版升級驗證。

### P4：版本與compatibility contract升級

以一個原子變更將實際功能版本設為`1.1.0`：

- `MyFhirSdk.CodeGen.Tool`package/local manifest；
- CodeGen version；
- `GenerationCompatibilityMatrix`；
- Runtime descriptor compatible tool/CodeGen identity與descriptor SHA-256；
- primitive/model manifest tool/CodeGen provenance；
- package content/hash snapshots。

Runtime assembly identity、TFM、Runtime symbol contract與compiler reference DLL若content未變，不得
為了版本方便任意變動。如果reference hash變化，先判斷是否為真正source/assembly變更；不可只
更新expected hash掩蓋non-determinism。

Exit gate：每個compatibility dimension有positive/negative tests，所有committed descriptor與
manifest hash由canonical pipeline產生。

P3與必要P4 identity更新在同一變更交付：Tool/CodeGen、local manifest及descriptor compatibility
升為`1.1.0`。Runtime assembly、TFM、symbol contract及compiler-reference SHA-256保持不變。
Descriptor SHA-256為`128ba716806fa276186586ec735bb30525a8d0ddfaf00f9525f48b68fd8cad5a`。
兩份committed generation manifests由canonical pipeline重建，852個generated C# sources保持
byte-identical。`baselines/primitive-tgz-input/`保留所有`1.0.0`證據並新增`1.1.0`證據；
測試只允許歷史manifest的tool/CodeGen版本與descriptor hash變動，其餘內容仍須一致。
舊版package從`eng/codegen-tool-upgrade-baseline.json`的immutable revision重建，未修改現行
source版本來模擬`1.0.0`。

P4補齊的compatibility驗證（版本沿用P3的`1.1.0`）：

| Dimension | Positive / negative evidence |
| --- | --- |
| Tool / CodeGen version | 兩種模式的current provenance；舊版、patch與prerelease不符；descriptor與request同為舊版仍不可繞過current matrix |
| Compatibility schema / version policy | current schema與exact policy成功；不支援的schema與range policy失敗 |
| Runtime contract / descriptor binding | current contract成功；contract version不符、reference set綁定不同descriptor失敗 |
| FHIR package ID / package version / FHIR version | current identity成功；大小寫不同ID與錯誤版本失敗 |
| Primitive policy | 兩種模式驗證version、FHIR version、Runtime contract與hash；僅LF/CRLF差異仍成功 |
| Model policies | 五份policy正確時成功；各自hash不符、遺漏或未知logical name失敗 |
| Runtime DLL identity / TFM / hash | `RuntimeReferenceServiceTests`既有正負測試；不更動assembly identity或reference bytes |
| Descriptor / generated manifest / package provenance | `RuntimeContractLoaderTests`、committed generation、primitive baseline與package tests驗證canonical hashes與產物一致性 |

主要測試為`GenerationCompatibilityServiceTests`。P4不再次變更版本、descriptor或generated
manifests；其canonical provenance已於P3升級。本階段不取代P5的CLI/pipeline錯誤矩陣。
Contract與invocation負向案例同時比對diagnostic code與精確的`<compatibility:dimension>`
source identity，避免共用diagnostic code的其他維度掩蓋遺漏的檢查。
本機P4驗證：CodeGen tests 465 passed（較P3增加23 cases，含既有canonical generation與package
tests）；toolchain contract與`git diff --check`通過。Runtime reference仍為
`7c945def6e2414e7944367d0df0ac33aa6386e4cdbedce0d05922ae5023176c9`。
跨平台CI須在本次變更push後確認。

### P5：CLI與pipeline test matrix

至少新增：

| Case | Expected |
| --- | --- |
| `.tgz` + valid explicit policy | 成功，preferred command |
| directory + valid explicit policy | 保持既有成功行為 |
| `.tgz`缺少`--policy` | required-option failure |
| directory缺少`--policy` | required-option failure |
| package與CLI ID/version/FHIR不符 | preflight失敗 |
| malformed/truncated/non-gzip archive | 穩定read diagnostic |
| missing/duplicate `package/package.json` | 失敗 |
| rooted/traversal/backslash/duplicate archive entry | 失敗且不寫入filesystem |
| package不含primitive | 失敗 |
| duplicate primitive type/canonical | 失敗且ordinal diagnostic |
| invalid primitive shape | 失敗，不被filter吞掉 |
| explicit policy missing/corrupt/hash/version不符 | preflight失敗 |
| repeated generation | byte-identical complete output |
| tgz vs directory | wrappers、registry、manifest全部相同 |
| output與input/policy/tool asset重疊 | safety failure，不破壞既有output |

Exit gate：CodeGen unit/integration、committed generation與全部SDK Runtime behavior regression通過。

P5測試落點：

- `PrimitiveCliFailureMatrixTests`：實際執行`GeneratorCli.RunAsync`，覆蓋兩種input缺少explicit
  policy、invocation identity不符、policy missing/corrupt/hash/version、損壞與截斷archive、
  metadata missing/duplicate/corrupt、危險entry path、duplicate entry、沒有primitive、損壞
  primitive shape、duplicate type/canonical及tool/asset output overlap。
- 每個CLI失敗案例驗證exit code、特定diagnostic、沒有成功生成訊息，並逐byte比較暫存工作區
  既有檔案、檔案清單與staging清理；duplicate identity另驗證正反archive順序及ordinal diagnostics。
- `PrimitivePackageGenerationTests`保留package實際identity驗證與input/policy overlap的直接
  pipeline測試；成功案例比較兩種input與兩者重複生成的完整22個產物。
- `PrimitiveTgzInputBaselineTests`與committed generation tests持續驗證wrappers、registry、
  schema v2 manifests及歷史baseline。沒有修改production generation或更新expected hashes。

Unknown primitive kind沿用既有`UnsupportedDefinition`優先序，CLI exit code為3；其他本次
archive/identity/policy失敗為2、缺少required option為1、unsafe output為5。

本機P5驗證：完整solution tests 798 passed、1個既有external-server integration smoke skipped；
其中CodeGen 503 passed（本階段新增38個CLI cases）。最後調整duplicate type/canonical fixture
以隔離兩種collision後，兩個案例重跑通過。Release build無warning/error、`git diff --check`
通過；本次變更的Windows/Linux CI待push後確認。

### P6：local tool package、upgrade與cross-platform CI

1. Pack後在隔離目錄放置`.nupkg`、FHIR`.tgz`和explicit primitive policy。
2. 建立新local manifest，install/restore`1.1.0`並執行`.tgz` primitive command。
3. 驗證工具不讀repository、current directory fallback、SDK`bin/obj`或loaded assembly。
4. 從immutable真實`1.0.0`package安裝，執行`dotnet tool update`至`1.1.0`；不得改目前source
   版本模擬舊版。
5. Windows與Ubuntu使用同一canonical package/runtime asset、FHIR archive和policy，compare
   normalized generated output與package inventory。
6. canceled/failed generation保留原output並清除staging。

Exit gate：build、test、pack、clean smoke、upgrade與Windows/Linux drift jobs全部綠燈。

### P7：操作文件與handoff

實作完成後更新：

- `CodeGen/README.md`：以`.tgz`和required`--policy`作recommended primitive example；
- CLI help與新的baseline snapshot；
- Phase D handoff：記錄post-D CodeGen enhancement與`1.1.0`contract；
- Runtime/Models/CodeGen boundary：primitive input改為versioned package；
- Runtime kernel extraction guide：K0以本功能完成後的版本與hash作baseline；
- upgrade/rollback/troubleshooting：說明`1.0.0`directory-only與`1.1.0`雙input模式。

Exit gate：使用者不需整理primitive-only definitions directory；required policy來源與命令有清楚
文件，沒有新debt只留在PR描述。

## 5. Diagnostic與安全規則

- package read/identity錯誤沿用definition package diagnostic owner。
- primitive inventory/coverage錯誤沿用primitive diagnostic owner。
- explicit policy錯誤沿用既有policy/compatibility diagnostic。
- 新diagnostic code必須先更新allocation文件與排序測試，不重編既有code。
- 診斷使用archive logical entry；不得輸出NuGet cache或temporary extraction path。
- loader串流讀取tar/gzip，不將entry path寫入filesystem，不新增extract-to-current-directory。
- input archive、explicit policy、runtime descriptor/reference與tool installation列入protected paths。

## 6. Required verification commands

實作期間至少執行：

```powershell
dotnet restore MyFhirSdk.sln
dotnet build MyFhirSdk.sln -c Release --no-restore
dotnet test MyFhirSdk.sln -c Release --no-build --no-restore
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
pwsh ./eng/Test-CodeGenToolchainContract.ps1
git diff --check
```

另需由P6 smoke script在repository外temporary root執行`.tgz` primitive command；不能用
`dotnet run --project CodeGen`取代installed tool驗證。

## 7. PR sequence

| PR | Scope | Production behavior |
| --- | --- | --- |
| P0 | decision、guide、baseline與equivalence harness | 不變 |
| P1-P2 | package input abstraction與primitive selector | directory保持；`.tgz`可先由internal test使用 |
| P3-P4 | CLI `.tgz`切換與真實`1.1.0`compatibility identities | 原子啟用新capability |
| P5-P6 | full tests、clean package與upgrade CI | 固定新contract |
| P7 | operations與handoff | 不變 |

若P3-P4無法在main保持每個中間commit可pack，應合併成一個原子PR或使用internal feature seam；
不得讓可發布的中間狀態出現新CLI capability但錯誤compatibility identity。

## 8. Definition of done

- decision已Accepted；D0-006 required-policy規則保持有效。
- primitive`.tgz`是documented preferred input，directory仍通過相容測試。
- 兩種input都必須明確提供`--policy`。
- `.tgz`與directory產生byte-identical wrappers、registry和schema v2 manifest。
- Tool/CodeGen真實版本為`1.1.0`，可從immutable`1.0.0`完成upgrade。
- repository-independent smoke只需tool package、FHIR`.tgz`、explicit policy和output location。
- Windows/Ubuntu build/test/pack/smoke/upgrade/drift全部通過。
- Runtime kernel extraction尚未開始，後續K0使用此完成狀態作baseline。
