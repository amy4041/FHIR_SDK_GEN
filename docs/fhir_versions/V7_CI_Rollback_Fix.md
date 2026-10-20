# V7 CI：rollback 跨平台比較修正

## 實際原因

[CI run 39365916011](https://github.com/amy4041/FHIR_SDK_GEN/actions/runs/39365916011) 的 Windows／Ubuntu `G7-platform` 都通過，但最後比較失敗於 `Rollback source/count mismatch`。
下載原始 artifacts 後確認：rollback revision、測試計數、assemblies 清單都相同，只有 `archiveSha256` 不同。

| 欄位 | Windows | Ubuntu |
| --- | --- | --- |
| rollback revision | `99017eff8c608aa1e34292ddaf10eab60912bbed` | 相同 |
| source files | 2331 | 2331 |
| tar SHA-256 | `eb9542989fbfb26b8a85fab8b6e2661b2bc44390eb30cde05438113765f3ef39` | `d8a6e07e942e45c9596239b29bdecbe2546a1f22227160923a615e1d126af145` |

這次不只是 tar header 不同：361 個文字檔的來源 hashes 也不同。`git archive` 會套用 checkout conversion；Windows 的 `core.autocrlf=true` 將這些檔案轉成 CRLF，Linux 則保留 LF。
在同一份 repository 中切換 `core.autocrlf=true/false`，可精確重現上述兩個 tar hashes 與兩份逐檔 inventory。FHIR 模型或 rollback 測試沒有因此失敗。

## 修正方式

- rollback 匯出明確使用 `git -c core.autocrlf=false -c core.eol=lf archive --format=tar <V6 pin>`，避免繼承 runner 的文字行尾設定。
- `archiveSha256` 保留為原始封裝證據，跨平台以 `rollback-source-hashes.json` 的實際檔案路徑與 SHA-256 判斷來源一致性，不以 tar 容器 bytes 判斷。
- 每個平台必須提供完整、非空且數量相符的 source inventory；缺少、改名或改變檔案 bytes 均不能通過。
- source revision、source file count、test counts、assemblies 仍須跨平台相符。錯誤會指出具體欄位或前幾個不同的來源路徑。

回歸測試涵蓋不同 tar hash／相同檔案可通過、相同 tar hash／不同檔案仍拒絕、來源清單缺失或損壞，以及在不同 inherited Git 行尾設定下實際匯出相同 LF archive、保持二進位 FHIR package 不變。

## CI 重跑方式

提交修正後，重新執行包含兩個平台 jobs 的完整 CI，讓 Windows／Ubuntu 都重新匯出、編譯及測試固定 LF 的 rollback source。
只重跑最後的 comparison job 會繼續使用舊 artifacts；舊 Windows artifact 仍含 CRLF 來源，應繼續被逐檔比較拒絕。

原有 baseline／CI artifacts 保持不變；本機重現與驗證放在 ignored `artifacts/fhir-versions/v7/`。修正後 G7 完整 CI 通過前，不將交付狀態改為 Passed；外部 server acceptance 也不受此修正影響。
