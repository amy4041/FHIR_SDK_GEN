# V1 selected closure experiment

日期：2026-10-14（code review 修正後重驗）。G1 本機 Passed：19 個 V1 cases 通過；完整 solution regression 為
**941 passed／0 failed／1 skipped**（含原有 R5 tests 與 V1）。實際 counts、source fingerprint
與 TRX hashes 見 [summary.json](summary.json)。
V0 revision 為 `5958d92dd6457a09002662440244ce06de8432bb`，使用者已確認 commit 與 CI 完成。
依使用者要求，同一 `feature/fhir-r4-v0` branch 接續，暫不合併 main；穩定 main 還原點為
`ca0c27f81adb729657da8ddab61c71c70e113984`。沒有把本機證據當成 V1 CI／code review 通過。

## 實驗實作與 scope

R4 實驗模型放在 `Tests/FhirVersions/V1/Models/Generated`，獨立編譯為
`MyFhirSdk.R4.V1.Models.dll`，只引用 Runtime／framework。Namespace 為 `MyFhirSdk.R4.Primitives`、
`MyFhirSdk.R4.Types`、`MyFhirSdk.R4.Resources`，未建立 production Models package。
R5 使用目前 SDK 的完整 provider，這次 cases 僅驗證對應 selected resources 與 dependencies。

`eng/Generate-FhirR4V1Experiment.py` 是隔離 prototype，不是 production tool 1.2.0 的 R4 支援。
從鎖定的官方 R4 `4.0.1` archive 以 `Patient`／`Observation`／`Attachment` 為 roots，沿 baseDefinition、
全部 snapshot element types 與實際 profile-narrowed types（SimpleQuantity）計算 closure，bootstrap
的 Extension choices 亦完整展開。Reference.targetProfile 是 canonical metadata，不擴張為被引用
resource 的 CLR containment；contained resource factory 僅接受本次 scope 的 Patient／Observation。

| Closure disposition | Definitions |
| --- | ---: |
| Generated R4 resources／datatypes | 32 |
| Common primitive prototypes | 19 |
| Shared Runtime bootstrap | 7 |
| Version-specific SimpleQuantity auxiliary | 1 |
| XHTML via shared Narrative.Div | 1 |
| Total definitions | 60 |

生成 63 個 `.cs`（19 wrappers、43 model/backbone declarations、1 composition），另有 closure manifest。
完整 definition hashes、dependency edges、primitive templates／policy hashes、abstract bridge adaptations
與 generated source hashes 保存在 [closure.json](../../../../Tests/FhirVersions/V1/closure.json)。
`--check` 重跑計算後比較 committed bytes，拒絕 drift／額外 prototype source，沒有隱含網路下載。

## 暫時 adaptations 與限制

- 版本模型由官方 R4 fields 生成。R4 `Attachment.size` 是 FhirUnsignedInt／JSON number；R5 是
  FhirInteger64／JSON string。測試覆蓋最大 R4 integer、較大的 R5 值、錯誤 JSON token 與非法值。
- Common primitive wrappers／codecs／validators 使用顯式 pinned R5 templates 與現有 implementations；
  R4 支援集合由官方 R4 closure 決定，不包括 integer64。這是實驗 reuse，V4 必須另固定 R4 lexical
  policy 並驗證差異。XHTML 沒有 wrapper，Narrative.Div 仍是 raw string。
- R4 datatypes 的官方 Element base 透過現有 Runtime.DataType bridge；Dosage／Timing 等以官方
  BackboneElement 為 base 的 datatypes 透過 Runtime.BackboneType bridge，取得 Extension value marker。
  這些新增抽象階層在 manifest 明列，不能聲稱 CLR hierarchy 等於官方 definitions 的直接 hierarchy。
- SimpleQuantity 是 R4 Quantity 的獨立 auxiliary subclass，保留 profile-narrowed property shape。
  Comparator constraint／其他 constraint profiles 未實作通用 executor，不宣稱 profile conformance。
- Required／choice entries 從 R4 snapshots 生成，以現有 executor 執行；collection structure、primitive
  lexical 規則共用現有流程。Terminology／FHIRPath／fixed／pattern／targetProfile enforcement deferred。
- Composition／context／guard 位於 Architecture test assembly，透過已存在的 friend access 注入真實
  SDK engines；R4 Models assembly 沒有 SDK／engines 相依。這不是公開 SPI，也不是 production context。

## Runtime 結論與 V2 阻塞

共用 Runtime 對此 selected closure 可行，不需修改 Runtime public API 或 canonical compiler reference。
Meta.Tag／Security 由各版本 metadata 固定為各自 Coding；Extension、Narrative、primitive metadata、
backbone 與 contained resources 在兩個 context 下完成 round-trip／validation。
Context 私有 ownership guard 檢查 root 及巢狀 CLR identities，含 shared bootstrap 的內容，誤用在
serializer／validator 執行前失敗；版本由指定 context 決定，不從 JSON 字串自動判版。

已實測 production gap：現有 R5 serializer 對空的 foreign Coding 會靜默略過，對帶 foreign primitive
的 Coding 則可能到 registry lookup 才丟 KeyNotFoundException。V1 boundary 以 FhirSdkException
提早拒絕，測試固定兩種行為；V2 必須把完整 nested ownership 檢查與一致 diagnostics 加入正式 engines。

| 最小必要 SPI | V2 收斂工作 |
| --- | --- |
| Release／package／policy／model identity | 不可變 context，完整 provider 組合一致性，禁止 global current version |
| Resource metadata／factory | 同名 FHIR resource 對應各自 Type／factory；不能 assembly scan fallback |
| Declared datatype／Extension／open-type metadata | 中立公開 DTO 與 signatures；保留版本 lookup 與 CLR type identity |
| Primitive definitions／codec／validator | 版本 registry composition 隔離，共用適用 codec；不公開整個 internal registry |
| Validation provider／rule description／diagnostics | 必要中立契約與 executor 分離，Models 不反向依賴 SDK／engines |
| Ownership／capabilities inventory | root／nested／bootstrap contents 的拒絕與能力狀態；cache 不只用 FHIR 名稱 |

Full generation 前仍需 V2 production SPI／guard、V3 R5 Models 分離、V4 精確版本 policies／CLI config。
不能以本次 experiment 宣稱全部 R4 resources、R4 Client／IG、完整 validation 或公開 release。

## 測試與重現

19 個 V1 cases 覆蓋雙版本 factories、bootstrap／primitive metadata alignment、Extension datatypes／
SimpleQuantity、required／choice／cardinality／primitive lexical、實際 size 差異、root／contained／
Meta.Tag／Security／Extension datatype／primitive 誤用、mixed primitive provider 及 40 次並行交錯操作。
13 個 Python integrity tests 包含既有 V0 tests、closure／source determinism、嚴格 skip policy 與實測 assembly hashes；不計入 solution test counts。
七個 solution test assemblies 的完整 regression、V1 cases 與原有 R5 results 由 runner 實際盤點。
Windows／Ubuntu 的獨立 V1 CI job 已加入 workflow，會驗證 committed prototype bytes、完整 closure
與 regression，並上傳 raw evidence；本次僅宣稱 Windows 本機結果，新 CI 尚待 push 後執行。

```powershell
dotnet restore MyFhirSdk.sln
python eng/Generate-FhirR4V1Experiment.py --check
python -m unittest discover -s Tests/FhirVersions -v
python eng/Test-FhirV1Experiment.py --output artifacts/fhir-versions/v1/new-run
```

需 SDK 9.0.317、Python 3.10+、PowerShell 7.2+；本機沿用 V0 的 project-local pwsh 7.5.0 與 ignored
`artifacts/fhir-versions/temp`（PATH／TEMP／TMP 設定見 V0 README）。外部 Client endpoint 未設定仍
skipped；沒有 R4 Client 外部整合證據。原始 logs／TRX／DLL 留在 ignored artifacts／CI uploads。
Portable evidence 包含 [source inventory](source-inventory.json)、[V1 cases](v1-test-cases.json) 與
[file hashes](evidence-files.json)；K0–K7／V0 歷史 baseline 未覆寫。

## Code review 修正

- K3 consumer inventory 明確分類 `MyFhirSdk.R4.V1.Models.csproj` 為 `experiment-r4-models`，重建由
  Architecture tests 的 ProjectReference 負責。Evaluated ProjectReference 只允許目前 Runtime project，
  DLL／package reference 禁止；harness 加入正常案例與 SDK／DLL／package 反向相依拒絕案例。
- R4 Models project 加入 solution configuration，避免 solution Release build 對未列入 solution 的
  dependency 使用 Debug。G1 核對 Architecture test output 內實際使用的 Models DLL 與被量測的
  Release Models DLL hashes 相同，拒絕 stale／不同 configuration 的 binary 證據。
- G1 對全部七個 assemblies 檢查 skips，不只檢查 V1 cases。唯一例外為 Client assembly 的
  `PatientCrudSearchSmokeFlow`，且 endpoint 未設定、reason 精確符合既有訊息；其他 skips、錯誤 reason、
  重複 smoke skip 或已設定 endpoint 卻 skip 都明確失敗。Evidence 記錄 endpoint 是否設定與其 hash，
  不記錄 endpoint／token 明文。

完整 K3 rebuild／deployment／missing-Runtime 與 18 項 harness 結果見
[consumer rebuild evidence](consumer-rebuild-summary.json)。修正後 G1 原始 logs／TRX 位於
`artifacts/fhir-versions/v1/review-fixes-verified-final/`，本目錄 summary／source inventory／hashes 已重建，
先前的本機 runs 保留在 ignored artifacts，不能代替修正後 gate 證據。
Release 修正後的 K3 regression 曾遇到既有 RuntimePhysicalOwnershipTests 的 60 秒 MSBuild
evaluation timeout，失敗原始證據保留於 `review-fixes-k3-release`／`review-fixes-k3-final`。
關閉 build servers 未能解決；原因是 root SDK 在 default globs 先枚舉非 SDK trees，再以 Compile
Remove 排除，累積的 test artifacts 造成昂貴遍歷。SDK 現以 DefaultItemExcludes 提早排除相同的
artifacts／Tests／CodeGen／Runtime trees；修正前後 922 個 Compile items 與 ProjectReferences 完全一致，
本機 evaluation 降至約 1.2 秒。沒有增加 timeout 或略過測試。最後 raw K3 run 為 `review-fixes-k3-complete`。
最終本機 run 另停用 MSBuild server／node reuse；G1 summary 記錄這些 environment settings。
第一輪修正後 G1 的 CodeGen selected-scope commit 測試遇到 Windows staging Access denied，
該次失敗保留於 `review-fixes-verified`。K3 同一案例已通過，G1 最終另使用新的隔離 temp 目錄重跑。
