# V5：完整 R4 Models 與 runtime integration

V4 已提交於 `84c61ef`，使用者確認 CI 通過。V5 在同一 branch 建立正式 R4 Models；本機驗收見 `summary.json`，新增 Windows／Ubuntu CI 尚待提交後執行。

Review 修正後的本機 G5 **Passed**：完整 solution 1074 項，1073 通過、0 失敗、1 項外部 Client smoke 因未設定 endpoint 略過；13 項 Python integrity tests 通過。
Checkout 外以 installed tool 新生成來源重新編譯的 81 項 V5 integration cases 全部通過，包含新增的 31 項 R4／R5 lexical 差異、原始 literal 保留與非法值拒絕測試。
Rollback harness 的 16 項檢查通過，包含目前 8 個 assemblies 的實際 TRX 與歷史 7 個 assemblies 的獨立清單驗收。
原始 logs／TRX 在忽略目錄 `artifacts/fhir-versions/v5/review-fixes/verified`，本目錄保留 portable summaries、完整 inventory／hashes 與來源 fingerprint。

## 正式模型與依賴

`Models/R4/MyFhirSdk.R4.Models.csproj` 擁有 `Generated/R4/**/*.cs`、R4 `SimpleQuantity` 及同 assembly 的 composition bindings／`R4ModelProvider`。
只引用 ModelContracts／ModelSupport，經由它們使用 Runtime；沒有 SDK、engines、friend access 或 type forwarders。
SDK 仍只組裝預設 R5。需要 R4 的 consumer 額外引用 R4 Models，以 provider 明確建立不可變 context：

```csharp
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;
using MyFhirSdk.ModelMetadata.R4;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;

var r4 = new FhirModelContext(
    R4ModelProvider.Metadata,
    R4ModelProvider.Primitives,
    R4ModelProvider.Validation);
var patient = new FhirJsonParser(r4)
    .Parse<MyFhirSdk.R4.Resources.Patient>("{\"resourceType\":\"Patient\"}");
var json = new FhirJsonSerializer(r4).Serialize(patient);
var result = new FhirValidator(r4).Validate(patient);
```

R4／R5 model namespace 各自獨立。共用 Runtime 的 Meta／Narrative／Extension 經 context 對應版本內的 Coding、primitive 與 extension value。JSON 本身通常沒有版本資訊，呼叫端必須選擇 context；跨版本拒絕針對 CLR 物件與 provider 組合。

## 完整 scope 與能力

固定官方 `hl7.fhir.r4.core#4.0.1`，archive SHA-256 `b090bf929e1f665cf2c91583720849695bc38d2892a7c5037c56cb00817fb091`。
`scope-inventory.json` 逐筆記錄全部 655 個 StructureDefinitions 的處置、原因及來源 hash，另列 473 個 nested declarations：

| 處置 | 數量 | 說明 |
| --- | ---: | --- |
| Generated models | 182 | 146 resources、36 complex datatypes |
| Generated primitives | 19 | 不包含 R5 integer64 |
| Runtime bootstrap | 7 | Element、BackboneElement、Resource、DomainResource、Extension、Meta、Narrative |
| Constraint auxiliary | 1 | R4 SimpleQuantity |
| Excluded constraints | 440 | Profile 生成與完整驗證 deferred |
| Excluded logical models | 5 | 包含 logical MetadataResource，不是可建立的 JSON resource |
| Unsupported primitive | 1 | XHTML 沒有獨立 wrapper；Narrative.Div 使用 string 並驗證 JSON round trip |

提交 677 個 R4 generated C# 及兩份 manifests；其中 2 個 C# 是 metadata／rules composition、1 個是 primitive registry。
Models assembly 擁有 675 個公開 FHIR 型別及 `R4ModelProvider`；`provider-inventory.json` 固定完整 CLR inventory、146 factories、19 primitive registrations、906 validation rule entries。
API snapshot 位於 `Tests/FhirVersions/V5/ApprovedR4ModelApi.txt`。

| 驗證能力 | 狀態 |
| --- | --- |
| Primitive lexical／JSON token、numeric representation | R4 registry 選擇版本規則；共用 codec 與 ModelSupport lexical／semantic executor |
| Required scalar／collection、ordinary choice、required open type | 執行生成的規則；issue 保留來源 RuleId |
| Null list／null item | 共用 walker 的 cardinality 檢查 |
| Maximum cardinality | scalar／IList 的 CLR 結構限制；沒有有限集合上限的獨立通用執行器 |
| FHIRPath invariants、terminology、reference target profiles | Deferred，不能宣稱已驗證 |
| Constraint／IG profiles、slicing、fixed／pattern value | 完整支援 deferred；不自動套用 R5 IG 規則 |

`CompleteResourceSet=true` 表示完整基礎 resource factory inventory，不表示完整 FHIR validation。Capability flags 中 InvariantValidation／ProfileValidation 為 false。

## 工具與相容性修正

Tool／CodeGen 從 1.3.0 升為 **1.3.1**：R4 choice／validation policies 原本保留了 R5 的統計值，改成以 locked R4 input 的 differential direct elements／snapshot effective elements 重算，並由 G5 核對。
同步更新 descriptors／manifest provenance；沒有改動模型 render、Runtime compiler reference 或 public SPI。全部 852 個 R5 generated C# 維持原始 bytes。
新工具 fixture oracles 在 `tool-1.3.1/`；V0–V4 歷史 evidence 不覆寫。
共用 SDK RuleAdapter 在 issue 未提供 RuleId 時保留 provider 的 RuleId，讓兩版本的生成規則都能追溯。

Review 修正另外將 R4 primitive policy 升為 `1.0.1`，移除從 R5 沿用的 decimal 位數上限常數；本次 V5 的 Tool `1.3.1` 尚未提交，不另增加工具版本。
R4 Models 的 internal lexical bindings 使用官方 R4 base64Binary／code／dateTime／decimal／instant／time patterns。
ModelSupport 新增不含版本分支的 `PrimitiveValidationPolicies.WithPattern`，可組合完整字串 lexical 檢查與共用 semantic validator；只有驗證副本會移除 base64 whitespace 或縮短時間小數秒，原始 wire literal 保持不變。
R5 維持原 validators；current ModelSupport API snapshot 新增這個通用 API，R4 API snapshot 只移除三個錯誤的 decimal 上限常數，Runtime／ModelContracts 與既有 R5 API 不變。

Rollback 驗收明確區分目前 source 的 8 個 assemblies 與歷史 K0 source 的 7 個 assemblies。
G5 會執行 K7 harness，驗證兩種清單的正向／負面案例，並讓目前完整 solution 的實際 TRX 通過同一個 rollback regression checker。

## 重現 G5

需 pinned .NET SDK 9.0.317、Python 3.13、PowerShell 7.2+，以及包含 V2 pin 的 Git history：

```powershell
dotnet restore MyFhirSdk.sln
python eng/Test-FhirV5Integration.py --output artifacts/fhir-versions/v5/run
```

Runner 先執行 G4／G3／G2 的完整 solution regression、Python integrity tests、R5 API／source guards 與 checkout 外 installed-tool 生成。
接著在另一個 checkout 外乾淨目錄中，排除 committed R4／R5 generated sources 及所有 bin／obj，以 installed tool 的當次輸出建立正式兩版本 Models、SDK 與 V5 consumer tests。
核對實際測試 assembly／案例與 TRX counters、完整 source hashes、API、provider／rule inventory、build 前後生成來源不變，以及來源 fingerprint。
NuGet dependency restore 使用環境的套件快取；installed tool 的 package feed／cache 仍依 G4 隔離。

V5 測試涵蓋全數 resource factory JSON round trip、19 primitives、R4／R5 lexical 差異與錯誤 token、primitive metadata array alignment、choice／required／cardinality、open type、Extension、contained／unknown resources、跨版本誤用與並行 R4／R5 consumer。
外部 Client smoke 未設定 endpoint 時明列 skipped；R4 Client 屬 V6，部署／缺少相依 DLL／rollback 的正式 R4 handoff 屬 V7。
