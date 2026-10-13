# V0 設計決策

日期：2026-10-13。狀態：V0 baseline 決策固定；Runtime 可行性與 SPI signature 待 V1／V2 驗證。
本文件沒有宣稱 R4 production 支援。基準 revision、hashes 與測試數見 [V0 evidence](baselines/v0/README.md)。

## 型別、assembly 與 facade

| 責任 | 決策 | 實作階段 |
| --- | --- | --- |
| R5 Models | `MyFhirSdk.R5.Models.dll`；保留 `MyFhirSdk.Primitives`、`MyFhirSdk.Types`、`MyFhirSdk.Resources` namespaces 與 members | V3 |
| R4 Models | `MyFhirSdk.R4.Models.dll`；使用 `MyFhirSdk.R4.Primitives`、`MyFhirSdk.R4.Types`、`MyFhirSdk.R4.Resources` | V1 實驗，V5 production |
| 版本 composition | 各版本 Models 擁有 metadata／factory／primitive definitions／validation entries；不依賴 SDK／engines | V2–V5 |
| SDK facade | 保留 `MyFhirSdk.dll` 與現有 R5 default constructors；組裝 R5 provider 交給 engines。R4 入口必須指定 context | V2–V3 |
| Runtime | 保留 `MyFhirSdk.Runtime.dll`、`MyFhirSdk.Core` 與 net9.0；共用 bootstrap 是 V1 假設，尚未驗證 | V1 |
| 中立 SPI owner | 採獨立 `MyFhirSdk.ModelContracts` assembly，僅引用 Runtime；metadata／factory、primitive definitions、validation provider 與能力 identity 的必要契約放此處 | V2 |
| Engines | 一個共用 engines assembly 是依賴解耦的候選方案；physical owner 在 V2 簽章盤點後固定。Models 禁止引用 engines／SDK | V2 |

獨立 ModelContracts 避免將版本 composition 與 executor 所需 SPI 全部加入已凍結的最小 Runtime compiler
contract。V1 如證明另有更小且無循環的方案，先修訂本決策再實作。V0 不先新增 public interface。
既有 `IModelMetadataProvider`／metadata DTO、`IValidationRuleProvider`、primitive registry／codec 與
validation rule signature 仍有 internal／SDK ownership；V2 必須盤點完整參數、回傳型別及連帶依賴。
通用 executor 留在 engines；必要的中立 rule description／diagnostic 契約不能反向引用 engines。

## Compile ownership 與遷移

目前 evaluated ownership 見 `project-inventory.json`：root SDK recursive Compile 仍編譯 R5 generated
sources、`Types/SimpleQuantity.cs`、SDK metadata／primitive composition、engines、Client 與 IG；Runtime
只編譯 `Runtime/KernelCompileItems.props` 的 14 個來源，Generator 不引用 SDK／Runtime implementation。
Consumer inventory 以明確的 Release SDK／Runtime implementation paths 評估，保留 evaluation 設定與
實際 HintPath；條件式 Runtime Reference 必須包含在盤點內。歷史 fixture 的來源不改寫，該項 evaluation
僅描述目前 implementation 的依賴，不代替歷史 binary compatibility 證據。

V3 root project 必須排除新 Models／contracts／engines 專案來源。每個 declaration 只有一個 compile owner；
generated primitive registry partial 與版本 provider 不可留在 SDK 跨 assembly 延伸。
所有 test projects、KernelMigration consumer／RebuiltLibrary／RebuiltConsumer、明確 DLL Reference 與
assembly-based reflection 設定都需重編。`consumer-findings.json` 保留 source locations，`public-api.txt`
固定 base／interface graph、signature 與 defining assembly。

R5 Models 搬移會改變 AQN／defining assembly。保留全面重編、無 type forwarders 的既有策略；不是 binary
drop-in 相容承諾。歷史 KernelMigration fixture 保持原意，不改寫 K0–K7 pins。rollback 為還原完整 source
revision 與鎖定 inputs 後重建，不能只替換 Models DLL。

## R4 scope 與 validation 能力

核准的生成候選為官方 core package 的 primitive／complex-type／resource specializations，含 abstract
base、backbone dependency closure。bootstrap candidates 必須在 V1 比較 hierarchy／JSON／validation；
每個 definition 的 disposition、reason、hash 與 metadata occurrences 見 `r4-definitions.json`。
Bootstrap 分類由 Runtime compile items 與各版本 definitions 交集取得，不要求 `derivation=specialization`；
R4 `Element`／`Resource` 與 R5 `Base` 等根型別仍屬 Runtime candidates，R5 `DataType`／`BackboneType`
沿用已固定 Runtime ownership。這是 V1 必須比較的 shape 清單，不能視為 R4 相容性已通過。
Counts 是 definitions 與 snapshot occurrences，不是預期 CLR 檔案數、factory 數或已執行規則數。

| 能力 | R4 V0 狀態 | 後續目標 |
| --- | --- | --- |
| Primitive wrappers／lexical／JSON／metadata alignment | 尚未支援 | V1 selected closure；V5 完整 scope |
| Datatypes／resources／factory／choice／open type／contained | 尚未支援 | V1 selected closure；V5 正負案例 |
| Primitive format／collection structure／required／cardinality／choice rules | 尚未支援 | V4 固定 R4 policy；V5 驗證實際 executor |
| FHIRPath invariants／terminology bindings／fixed／pattern／targetProfile 等 | Deferred；僅盤點官方 metadata | 不從 R5 policy 推論 R4 executor |
| Constraint profiles | 不納入通用 base model generation | 必要 SimpleQuantity 為版本專屬 auxiliary |
| Logical models／其他無 specialization 的 definitions | 排除 base models，逐筆保留理由；Runtime-owned 根型別另列 bootstrap candidates | 另案 |
| R4 Client | 尚未支援 | V6；受控 HTTP 與外部 smoke 分開 |
| R4 IG／TW Core、R4B、R4/R5 轉換 | 本階段排除 | 另案 |

`SimpleQuantity` 的 R4 definition 是 Quantity constraint，不能按 `type == SimpleQuantity` 搜尋。
V1／V4 由官方 definition 建立 R4 auxiliary，base 必須是 R4 Quantity；目前 R5 手寫 asset 及其
`runtime-contract.json` normalized hash 保持不變。兩版本 usage 在 definition inventory 與 consumer
findings 保留；不能退回 Quantity 或跨版本借用同一 CLR type。

## Input 與 generated source 儲存

R4 官方 archive 約 4.53 MB，依既有 R5 fixture 模式提交到 `Tests/CodeGen/Fixtures/FhirPackages/R4/`。
`CodeGen/Policy/r4-package-lock.json` 固定來源、package metadata、SHA-256 與 offline CI 策略。
官方 R4 metadata 的 `type` 是 `fhir.core`，R5 是 `Core`，lock 分開記錄原始 metadata type 與邏輯
package type。此 lock 不加入現行 tool 相容表；tool 1.2.0 仍只接受 R5。

R4 generated sources 延續提交模式，使用 `Generated/R4/` 獨立 tree；V1／V5 實際生成後再量測檔案
數與 bytes，V4 以受控變更加入 `.gitignore` 白名單，V0 不預填 R5 的檔案數作為 R4 預估。
R4 policies／manifest／snapshot／tests 各自獨立。build-time generation、公開 NuGet release 均另案。

## V0 runner 與證據限制

使用 Python 3.10+ 標準庫、Git、SDK 9.0.317；既有 regression 另需 PowerShell 7.2+ (`pwsh`)。
Python runner 不改 production 實作；共用既有 Inventory 工具、MSBuild Pack pipeline 與 solution regression。
原始 logs／TRX／DLL／nupkg 留在 `artifacts/`。摘要及 inventories 可人工 review 後提交；runner 不會
自動覆寫歷史 baseline。`--inventory-only` 永遠保留 G0 Pending。

G0 runner 驗證 package hash／metadata、canonical compiler reference、API inventory determinism、
七個預期 test assemblies、TRX counters 與 run 前後 source fingerprint。Published baseline 目錄從
fingerprint 排除，避免摘要自我引用；其他 source 與 runner／decisions hashes 都被固定。
V0 只建立 Windows 本機證據；code review、CI 與 V7 跨平台 gates 仍需各階段獨立執行。
