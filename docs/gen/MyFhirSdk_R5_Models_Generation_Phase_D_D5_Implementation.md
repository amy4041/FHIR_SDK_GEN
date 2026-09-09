# MyFhirSdk CodeGen Phase D D5 實作紀錄

Version 1.0

- 狀態：Completed
- 前置基線：D0-D4 completed
- Compatibility schema：`1`
- Version policy：`exact`
- Manifest schema：`2`
- Runtime descriptor SHA-256：`8a715d62f206b47ace1649d4a0898c29594bf71c389a619221f12fabfa3eb863`

## 1. 完成範圍

D5 建立 `GenerationCompatibilityService`，在讀取完整 definitions、建立 inventory、render 或
write 前，依 Runtime descriptor 中的 machine-readable matrix 執行 exact ordinal preflight。
相容性規則不再只存在於文件；`runtime-contract.json` 的 `compatibility` 現在包含：

```json
{
  "schemaVersion": 1,
  "versionPolicy": "exact"
}
```

目前固定的 matrix dimensions 為 tool `1.0.0`、CodeGen `1.0.0`、Runtime contract
`phase-a-v1+c4-primitives-v1`、primitive policy `1.1.0`、FHIR R5/package
`hl7.fhir.r5.core#5.0.0` 與 target framework `net9.0`。model mode 另外驗證五份 model policy
的 normalized SHA-256。換行採 LF normalization，因此 Windows/Ubuntu checkout newline 不會
造成假 mismatch；其餘 bytes 變更都會 fail-fast。

## 2. Diagnostics

D5 使用 D0 保留的 Phase D code ranges：

| Code | Meaning |
| --- | --- |
| `FSG0120` | unsupported target framework |
| `FSG0121` | incompatible tool version |
| `FSG0122` | incompatible CodeGen version |
| `FSG0123` | incompatible Runtime contract version |
| `FSG0124` | incompatible primitive policy identity/hash |
| `FSG0125` | incompatible FHIR/package identity |
| `FSG0126` | incompatible model policy set/hash |
| `FSG0127` | incompatible compatibility schema/version policy |
| `FSG0128` | Runtime reference set was validated against a different descriptor |
| `FSG0130` | required packaged asset missing |
| `FSG0131` | required packaged asset unreadable/corrupt |

diagnostics 以 logical compatibility dimension／asset identity 排序，只包含 ordinal actual 與
expected value，不輸出 repository、tool installation 或 NuGet cache path。所有 D5 diagnostics
映射至 input/preflight exit code `2`。

## 3. Manifest contract v2

primitive 與 model manifest 共用 `GenerationManifestProvenance`，並將 schema version 從 `1`
升為 `2`。新增的 `compatibility` object 包含：

- compatibility schema version 與 `exact` version policy；
- tool package id `MyFhirSdk.CodeGen.Tool` 與版本；
- CodeGen version；
- Runtime descriptor schema/version/SHA-256；
- compiler reference logical assembly identity/SHA-256；
- target framework。

既有 top-level package、policy、scope 與 artifact inventory 欄位保留。provenance model 不提供
任何 path 欄位；artifact path 持續使用 `/` normalization。repository committed primitive/model
manifests 已透過正式 generator 更新：

- primitive manifest SHA-256：`111aeb5baf4ba32b1d57d61e8aea8d5b1a3fc22105bcdfe35b7b5bc0023f2d4e`
- model manifest SHA-256：`77ffb4af1ef2b01bd138908f2dd0d14ac44226c5a736e23e9c704285b0351c53`

目前沒有舊 manifest reader；producer 以明確 schema `2` fail-fast contract 取代隱式相容。
日後增加 reader 時必須依 schema version 提供明確 migration 或拒絕策略。

## 4. 驗收覆蓋

- baseline primitive、selected model 與 full model generation 成功；
- compatibility schema/policy、tool、CodeGen、Runtime contract、TFM、primitive policy、FHIR
  package 與 model policy 各有負向測試；
- selected/full model scope 回傳完全相同的 preflight diagnostics；
- missing model asset 使用 logical asset diagnostic，且不洩漏 physical path；
- descriptor/reference-set mismatch、malformed JSON 與 invalid UTF-8 使用 dedicated logical diagnostics；
- policy newline normalization、two-run output 與 selected canonical ordering 保持 deterministic；
- committed 831-model artifact inventory 除 manifest contract 外維持不變。

## 5. D6 交接

D6 應將目前的 tool identity、policies、descriptor、Runtime compiler reference 與 manifest schema
納入 `.nupkg` content/hash tests，並由實際安裝後的 local tool smoke 驗證相同 provenance。
