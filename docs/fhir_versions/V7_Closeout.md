# V7 結案紀錄

日期：2026-10-20。基礎 R4／R5 多版本開發 V0–V7 已完成；本次整理交付文件、外部範例與本機交付包，不合併 main，不發布 NuGet。

## 已確認的來源與自動驗收

- V7：`20bc1005bf4de2020e445c87eafd5c9edf742b9c`。
- 跨平台 rollback 修正：`a4e0dd9bd4383267c9648eeba86938ca476294d4`。
- 共用完整性測試不再依賴 V6 歷史物件：`dd2c93b85a5fb4c5234749ec8de2d9f449ee1916`；[CI 39376091324](https://github.com/amy4041/FHIR_SDK_GEN/actions/runs/39376091324) 成功。
- R5 SimpleQuantity 搬移：`c7680b73d32f41fbf66ae5c28bf47849996cd650`；[CI 39399916928](https://github.com/amy4041/FHIR_SDK_GEN/actions/runs/39399916928) 成功。R4、R5 SimpleQuantity 都由各自 Models 專案擁有，namespace／API 不變。

已驗收 Windows／Ubuntu 的 canonical generation、SDK／HTTP regression、三種獨立部署、缺少 DLL 的負面案例、V6 source rollback 與跨平台產物比較。歷史 `baselines/v7/` 保留當時 Pending 狀態與原始來源身分，不以新結案結果覆寫；目前結論以本文件及上述 CI 為準。

本次新加入的交付包 exporter 與外部範例屬後續交付整理，驗證結果另記；上述 commit 的 CI 不冒充已測過這些新檔案。提交整理後仍需由新 CI 驗證。

## 本次交付整理的本機驗證

- Windows Release solution regression：九個 test assemblies，1171 項，1170 通過、0 失敗、1 項既有外部 Client smoke 因未設定 endpoint 略過。TRX 位於 `artifacts/delivery-validation/regression/`。
- Python integrity suite：37 項通過。
- 新交付包在 repository 外、空 NuGet cache 驗證：R4／R5／Dual 各自 publish 後執行成功；兩版 models／primitives 各生成兩次，677／852 個 C# 與完整 manifests 的重複輸出一致。此處 external read 未執行；外部實測見下列使用者紀錄。
- 交付驗證器、exporter 與模板已接入 V7 Windows／Ubuntu CI；本次新增部分的遠端驗收須待提交後完成。

原始交付目錄以本次工具執行輸出為準，每包含 `delivery-manifest.json`，驗證結果另存 `verification/summary.json`。不將開發中 dirty checkout 的匯出誤標成乾淨 commit 產物。

## 使用者手動驗收

使用者於本次結案對話確認：R5 SimpleQuantity 搬移後，已重新完成 generator 與 SDK 的手動 smoke，涵蓋 R4、R5，SDK 實際連線 FHIR server，使用簡單的非真實資料。

使用者指定的測試目錄：`D:\projects\SDK_DEMO`。只讀檢查找到：

- `SdkDemo/Program.cs`：兩版的 Parse、Serialize、Validate、CreateAsync 與依建立結果 ID 執行 ReadAsync。
- `CodeGenDemo/out/r4/Generated/R4/`、`out/r5/Generated/R5/`：兩版 model 與 primitive generation manifests。

結果標記為 **使用者回報通過**。這次整理未代為重新連外、未擷取 server／病人資料或憑證；程式碼存在不等於每個分支都有原始執行紀錄。未回報 update／search／delete，故不把手動驗收擴張為完整 CRUD。CI 因未設定 endpoint 而記錄的 external smoke Skipped 也保持原狀，與此次手動測試分開保存。

讀取到的測試來源與四份 generation manifests 的 SHA-256 保存於 [手動驗收紀錄](V7_Manual_Acceptance.json)，不複製測試原始碼、endpoint 或憑證。

## 交付範圍與後續

交付包含 .NET 9 SDK implementation DLL、R4／R5 Models、CodeGen Tool `1.3.1`、固定 core inputs、R4／R5／Dual 外部範例及檔案 hashes。操作見 [交付指南](V7_Delivery_Guide.md)。

支援範圍仍以各版本 provider／policy 為準；未包含公開或私人 NuGet 發布、R4 IG／TW Core、完整 terminology／FHIRPath validation、R4／R5 資料互轉。外部 read 範例不執行寫入；需要完整 CRUD 回歸時，使用既有 V6 smoke 搭配明確的測試 endpoint。

後續優先事項為 SDK／Models 等套件的 NuGet 打包與私人 feed；版本、授權、signing／provenance 與發布政策另行規劃。是否合併 main 仍由使用者決定。
