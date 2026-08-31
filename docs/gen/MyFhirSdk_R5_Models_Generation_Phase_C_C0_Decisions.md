# MyFhirSdk R5 Models Generation Phase C0 Baseline 與決策

Version 0.1

- 文件狀態：In Progress
- 適用範圍：FHIR R5 `5.0.0`、`hl7.fhir.r5.core#5.0.0`、MyFhirSdk、.NET 9
- 工作分支：`feat/phase-c0-model-generation-baseline`
- 起始 commit：`d0cbf44425b5a8a5dc4180258aee6bf1613e2554`
- 實作指引：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Implementation_Guide.md`
- Phase C 交接契約：`docs/gen/MyFhirSdk_R5_Models_Generation_Phase_C_Handoff.md`

## 1. 文件目的

本文件記錄 Phase C0 的 baseline、已核准決策、待決事項、驗證方式與退出條件。C0 不改變
production behavior；後續 C1-C9 必須以本文件中 Accepted 的決策為實作輸入。

## 2. Baseline 狀態

| Gate | 狀態 | 證據 |
|---|---|---|
| Phase B primitive regeneration | Passed | `Generated/R5/Primitives` regeneration 無 git diff |
| Official R5 package identity | Passed | C0-001 lock 與離線 identity tests |
| Release solution tests | Passed | 500 passed、0 failed、1 skipped |
| R5 model public API snapshot | Pending | C0 後續工作 |
| Deterministic inventory reconnaissance | Pending | C0 後續工作 |

## 3. 決策摘要

| ID | 決策 | 狀態 |
|---|---|---|
| C0-001 | Official R5 package input、identity 與 offline CI policy | Accepted |
| C0-002 | R5 model public API baseline 與 disposition | Pending |
| C0-003 | Assembly、base 與 bootstrap ownership | Pending |
| C0-004 | Namespace、filename 與 collision naming | Pending |
| C0-005 | Backbone naming 與 public placement | Pending |
| C0-006 | Choice/open type public representation | Pending |
| C0-007 | Validation capability matrix | Pending |

## 4. C0-001：Official R5 package input

- 狀態：Accepted
- Package ID：`hl7.fhir.r5.core`
- Package version：`5.0.0`
- FHIR version：`5.0.0`
- Package type：`Core`
- License：`CC0-1.0`
- 官方來源：`https://www.hl7.org/fhir/hl7.fhir.r5.core.tgz`
- Archive size：`17057450` bytes
- SHA-256：`74b27cd1bfce9e80eaceac431edf230b0945a443564fbf5512f82e5fa50a80d4`
- Repository path：
  `Tests/CodeGen/Fixtures/FhirPackages/R5/hl7.fhir.r5.core-5.0.0.tgz`
- Lock path：`CodeGen/Policy/r5-package-lock.json`
- Archive root：`package`
- CI input policy：offline

### 4.1 決策

1. Phase C 的正式 full-batch 輸入固定為上述 package bytes，不以 URL 或檔名單獨判定身分。
2. Package archive 納入 repository test fixtures，讓 CI 與本機測試不需網路即可使用相同
   bytes。
3. Package ID、package version、FHIR version、package type、license 與 SHA-256 由 versioned
   lock 固定。
4. Hash 或 `package/package.json` identity 不符時，必須在 inventory construction 前失敗。
5. Production generation 不得在執行期間自動下載或改用最新版 package。
6. 若未來更換 package bytes、來源或保存政策，必須在同一個 change set 更新 lock、測試與
   本決策紀錄。

### 4.2 理由與影響

- 固定 package bytes 可避免遠端內容、快取或列舉差異破壞 deterministic generation。
- Repository fixture 提供可重現的 offline CI input，但會增加約 17 MB repository 大小。
- URL 保留作 provenance；SHA-256 才是正式 artifact identity。
- C1 package loader 與 C7 manifest 必須消費相同 identity，不能建立另一份 package
  version/hash 常數。

### 4.3 驗證與退出條件

`R5CorePackageFixtureTests` 必須離線驗證：

- archive bytes 符合 lock 中的 SHA-256；
- archive 含 `package/package.json`；
- package name、version、type、license 與 FHIR versions 符合 lock；
- test project 從 committed fixture 複製 archive 與 lock，不進行網路存取。

C0-001 在上述測試通過且完整 solution tests 無回歸後完成。

## 5. 待決事項

C0-002 至 C0-007 必須根據 model API snapshot、official package reconnaissance 與 Runtime
capability evidence 逐項核准。未核准前，C1-C7 不得自行推導 public API、base/bootstrap、
Backbone、choice/open type 或 validation disposition。
