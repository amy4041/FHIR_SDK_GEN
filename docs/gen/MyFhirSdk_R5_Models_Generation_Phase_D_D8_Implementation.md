# MyFhirSdk CodeGen Phase D D8 cleanup 與 handoff

Version 1.0

- 狀態：Implemented；等待 PR CI 驗收
- 分枝：`feat/phase-d8-tool-cleanup-handoff`

## 1. Cleanup

- 移除 `OutputSafetyContext` 的 development repository root/protected-path adapter，以及 writer
  中對 repository source tree 的條件分支。
- test composition 不再傳入或推測 repository root 來改變 production safety behavior。
- 保留 universal output protection：filesystem root、tool directory、inputs、policies、descriptor、
  Runtime references、unsafe artifact path、collision、staging、atomic swap 與 rollback。
- 根 SDK project 排除 `artifacts/**` sources，避免 D7 local smoke/baseline checkout 汙染後續 build。
- toolchain contract 掃描 production CodeGen，禁止 SDK `ProjectReference`、repository/solution/
  current-directory discovery、loaded assembly location 及 `bin/obj` lookup 回歸。
- architecture tests 固定 `OutputSafetyContext` 只保留 universal protection inputs。

## 2. Operations documentation

`CodeGen/README.md` 是 tool 操作基準，包含：

- build、pack、local manifest restore 與 help；
- full/selected model 與 primitive generation；
- package-owned assets、override precedence 與 output safety；
- compatibility matrix、manifest schema v2；
- 真實版本 upgrade、package rollback 與 troubleshooting；
- public release license/signing/SBOM/provenance gates。

## 3. Architecture 與 handoff

`MyFhirSdk_R5_Models_Generation_Phase_D_Handoff.md` 更新為 D0–D8 實際狀態，記錄目前單一
`MyFhirSdk` physical assembly、CodeGen logical dependency seam、packaging asset flow、handwritten
Runtime foundation ownership，以及每一項後續 debt 的 owner、理由和退出條件。

後續工作明確分為公開 release、Runtime/Models physical split、Profile generation、deferred
validation、新 FHIR 版本、新 TFM 與 contract-only reference；均不在 D8 內提前實作。

## 4. 驗收

- production cleanup/toolchain scan 通過；
- CodeGen、architecture 與完整 solution tests 通過；
- package layout、pack、repository-external local tool smoke 與 committed 831-source baseline 通過；
- `Generated/R5` 無非預期 diff，`git diff --check` 通過；
- merge 前由 Windows/Linux CI 完成 clean-environment 與 cross-platform drift gate。
