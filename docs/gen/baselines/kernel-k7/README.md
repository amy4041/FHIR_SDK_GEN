# K7 source rollback evidence

`rollback-summary.json` 是 2026-10-12 Windows 本機演練的可攜摘要；它不取代 K7 remote CI。
K6 push CI 通過／已合併 main 由使用者確認，未提供 run URL。本次未編造 CI 連結。

- Current baseline：K6 merge `64267f05c335361c6e1a3c5abac0423421ffe690`。
- Restored source：K0 pin `1a28f01d8a4c3aeea46c63da875d01594aeee086`，post-D tool 1.1.0。
- Current solution：922 passed、0 failed、1 skipped。
- Restored OLD solution：805 passed、0 failed、1 skipped；七個 test projects 完整執行。
- K0 九個 gates、K7 三個 rollback gates、五個拒絕／failed-evidence probes 通過。
- 兩套 external Client smoke 均未設定 URL，不宣稱對外 FHIR server 測試已執行。
- SDK／compiler reference／descriptor raw hashes 與 K0 pinned inventories 一致；沒有比較 nupkg ZIP bytes。

完整 JSON／TRX／logs／binaries 只存在 ignored artifacts，摘要不包含本機絕對路徑。
這是獨立 source reconstruction／build／run 演練，沒有 main revert、history rewrite 或使用者資料回退。
操作與重新執行命令見 [Runtime operations](../../MyFhirSdk_Runtime_Kernel_Extraction_Operations.md)。
