# MyFhirSdk CodeGen primitive `.tgz` input 決策

Version 0.2

- 狀態：Proposed；核准與實作完成前，現有 primitive CLI contract 不變
- 決策日期：2026-09-15
- 決策 owner：CodeGen + Package/Compatibility maintainers
- 適用基準：Phase D handoff、Tool/CodeGen `1.0.0`、FHIR R5 `5.0.0`、.NET 9
- 採用方案：A（新增 `.tgz` input；`--policy` 繼續 required）
- 實作指引：`docs/gen/MyFhirSdk_CodeGen_Primitive_Tgz_Input_Implementation_Guide.md`
- 上位文件：
  - `docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_D0_Decisions.md`
  - `docs/gen/MyFhirSdk_R5_Models_Generation_Phase_D_Handoff.md`
  - `docs/gen/MyFhirSdk_Runtime_R5_Models_CodeGen_Boundaries.md`

## 1. Context

目前 model mode 可直接讀取 versioned FHIR package archive：

```powershell
dotnet myfhir-codegen `
  --mode model `
  --input .\hl7.fhir.r5.core-5.0.0.tgz `
  --output .\models `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
```

primitive mode 則要求使用者先準備只含 primitive StructureDefinitions 的 flat directory，並
明確傳入 primitive policy：

```powershell
dotnet myfhir-codegen `
  --mode primitive `
  --input .\primitive-definitions `
  --policy .\primitive-generation-policy.json `
  --output .\primitives `
  --fhir-version 5.0.0 `
  --package-id hl7.fhir.r5.core `
  --package-version 5.0.0
```

本變更只統一 primitive/model 的 FHIR definition package input，不同時改變 policy resolution、
Runtime assembly boundary 或 manifest schema。

## 2. Decision drivers

- primitive generation 可直接消費與 model mode 相同的 official FHIR `.tgz`。
- 不再要求使用者先從 package 手工整理 primitive-only definitions directory。
- 保留既有 directory input、explicit policy與asset override contract。
- 重用既有 package identity、安全及tar/gzip loader，不建立第二套解析器。
- `.tgz` 與 directory 模式產生相同 primitive source、manifest與Runtime behavior。
- 不引入網路下載、repository/current-directory或`bin/obj` fallback。

## 3. Decision

### 3.1 採用方案 A

primitive mode 增加以下preferred production command：

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

`--policy <file>`維持required。即使tool package已攜帶canonical primitive policy，primitive
mode仍不自動使用package-owned policy；這個既有CLI/asset decision不在本次變更範圍。

### 3.2 Input modes

`--input`支援兩種明確模式：

| Input | 狀態 | 用途 |
| --- | --- | --- |
| FHIR package `.tgz` file | preferred production mode | 直接消費official package與驗證identity |
| flat directory of primitive StructureDefinition JSON | supported compatibility mode | Phase B tests與自訂離線definition set |

本階段不移除directory mode。一般檔案若不是`.tgz`、不存在或無法讀取，必須以穩定diagnostic
失敗；不得猜測zip、URL或其他格式。directory mode只讀取該目錄第一層的`*.json`，維持既有
契約。

### 3.3 Package identity與primitive selection

`.tgz` mode重用model mode的`IDefinitionPackageInput`、`FileDefinitionPackageInput`與
`DefinitionPackageLoader`。不得建立第二套tar/gzip parser。

loader必須先驗證`package/package.json`：

- package ID與`--package-id` exact ordinal match；
- package version與`--package-version` exact ordinal match；
- package type符合既有FHIR package contract；
- `fhirVersions`包含`--fhir-version`；
- archive只有一份canonical `package/package.json`。

載入完整package後，primitive inventory只選取：

```text
resourceType == StructureDefinition
kind == primitive-type
derivation == specialization
version == requested FHIR version
```

其他合法StructureDefinitions由selector忽略，不產生unsupported diagnostic。已選primitive若
缺少required identity、snapshot、differential或baseDefinition則失敗。duplicate FHIR type或
canonical必須在render/write前失敗。

所有package entry、primitive inventory與diagnostic使用logical package entry name及ordinal
排序，不受tar entry order、filesystem enumeration或作業系統影響。

archive entry name必須是canonical relative `package/<file>.json`；rooted path、反斜線、空segment、
`.`/`..` traversal與duplicate logical entry必須拒絕。loader維持stream-only讀取，不將archive
內容解壓至current directory或temporary filesystem。

### 3.4 Policy contract保持不變

primitive mode仍要求：

```text
--policy <primitive-policy-file>
```

該檔案繼續經過既有strict loader、validator與compatibility preflight，包括：

- policy schema/version；
- FHIR version；
- Runtime contract version；
- exact SHA-256；
- supported/unsupported primitive coverage。

`--policy-root`仍只適用model mode。primitive mode不得從tool root、repository、current directory
或environment自動尋找policy。

### 3.5 Generated output equivalence

以相同Tool/CodeGen版本、primitive definitions和policy執行時：

```text
tgz-generated complete output
  byte-for-byte ==
directory-generated complete output
```

比較範圍包含：

- generated primitive wrappers；
- `PrimitiveRegistry.Composition.g.cs`；
- `primitive-generation-manifest.json`；
- primitive decisions及artifact inventory/hash。

source filename、namespace、public API、registry ordering與Runtime behavior不得因input mode不同而
改變。

### 3.6 Manifest contract保持schema v2

本變更不增加input path/mode/hash欄位，primitive manifest維持schema v2。既有manifest已記錄
FHIR package ID/version、FHIR version、policy/runtime/tool provenance與artifact hashes；不得為了
顯示`.tgz`實體路徑而加入machine-specific資料。

如果未來要加入archive exact-bytes SHA-256或區分directory/archive provenance，必須另立manifest
schema decision並升版；不與本次input adapter混合。

### 3.7 Tool與CodeGen version

`.tgz`是新增且backward-compatible的CLI capability。實作release將Tool/CodeGen從`1.0.0`提升為
`1.1.0`，並同步更新：

- package version與local tool manifest；
- compatibility matrix；
- runtime descriptor中的compatible tool/CodeGen identity；
- primitive/model manifests的tool/CodeGen provenance；
- clean install與真實`1.0.0 → 1.1.0`upgrade test。

舊版package必須從`eng/codegen-tool-upgrade-baseline.json`指定的immutable baseline重建或取自
受信任artifact；不得透過修改目前source版本號製造假`1.0.0`。

Runtime public shape、Runtime contract symbol set、compiler reference identity與bytes若未修改，
不因本功能任意改變。Descriptor只更新必要的compatible tool/CodeGen identity；更新後的
descriptor bytes/hash與manifest provenance必須同步。

### 3.8 D0 decision關係

本決策是D0-006的additive input extension，不取代下列既有規則：

> primitive mode的`--policy <file>`保持required且語意不變。

D0的asset precedence、repository-independent host、reference validation與output safety決策全部
保持有效。

## 4. Alternatives considered

### 4.1 方案 B：`.tgz`加packaged policy default

Deferred。把`--policy`改為optional可改善clean UX，但會同時改變D0-006 asset contract，增加
CLI、resolver、compatibility與negative-test範圍。本階段先只解決definition package input。

### 4.2 移除directory mode

Rejected。這會不必要地破壞Phase B fixture、自訂離線definitions與既有script；保留明確雙模式
不會削弱`.tgz`的preferred production status。

### 4.3 執行時從網路下載FHIR package

Rejected。會破壞deterministic、offline與supply-chain boundary；`--input`仍必須是local file
或compatibility directory。

### 4.4 修改primitive manifest schema

Deferred。archive provenance值得另行評估，但不是讀取`.tgz`的必要條件。

## 5. Consequences

### 5.1 Positive

- 使用者不需手工建立primitive-only definition directory。
- primitive/model modes共用FHIR package identity、安全與讀取機制。
- directory mode與required policy contract維持相容。
- 變更範圍集中在input adapter、selector、CLI文件與tests。

### 5.2 Cost and risk

- clean directory仍需另外提供primitive policy檔案。
- package loader需要將「忽略非primitive」與「拒絕損壞primitive」清楚分離。
- Tool/CodeGen/descriptor/compatibility identities需要原子更新。
- manifest v2不記錄input archive exact hash；此限制必須在handoff列為後續decision，而不是暗中
  擴充schema。

## 6. Acceptance gates

本決策標為Accepted前必須確認：

- Product/CodeGen owner接受`.tgz`為preferred且directory保留；
- Product/CodeGen owner確認`--policy`維持required；
- Release owner接受真實`1.1.0`版本與`1.0.0 → 1.1.0`upgrade baseline；
- `.tgz`/directory完整output equivalence gate已定義；
- archive安全、identity與invalid primitive diagnostics已有test plan；
- Runtime kernel extraction K0以完成後的`1.1.0`行為作baseline，不與本功能平行搬移assembly。

## 7. Rollback

在公開release前，以整個功能變更為rollback單位：input adapter、selector、versions、descriptor與
CI一起回復。既有directory/required-policy路徑必須一直可用，因此rollback不需要轉換primitive
definitions或policy。

如果`1.1.0`已發布，local manifest可pin回真實`1.0.0`package；`1.0.0`繼續使用directory input。
已由`.tgz`產生的sources若與equivalence contract一致，可由舊版directory模式重現；仍應在
staging output比較後再更新committed artifacts。
