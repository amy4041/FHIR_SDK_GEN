# V7 交付指南

## 維護者：從目前來源匯出

需要 .NET SDK `9.0.317` 與 Python 3.13；從 repository 根目錄執行：

```powershell
python eng/Export-FhirDelivery.py --output artifacts/delivery/v7
```

輸出必須是 `artifacts/` 下不存在的新目錄。exporter 會重新 publish Dual consumer 取得六個 implementation DLL，重新 pack generator，再複製固定 FHIR inputs、package locks 與外部範例；不使用先前 `artifacts/fhir-versions/v7/verified/` 的歷史產物。

首次 publish 可能需要 NuGet restore。若失敗，修正原因後使用新的輸出目錄；沒有完整 `delivery-manifest.json` 的目錄不能作為完成的交付包。

完整目錄結構與使用命令見 [外部範例 README](../../examples/external/README.md)。輸出包根目錄有相同 README，接收者不需要 repository。包內 `NuGet.Config` 只指向本機 feed；範例沒有外部 NuGet 或 ProjectReference 相依。

本機驗證命令：

```powershell
python eng/Test-FhirDelivery.py --bundle artifacts/delivery/v7 --output artifacts/delivery/v7/verification
```

驗證器將 manifest 指定的交付檔案複製到 repository 外的全新暫存目錄，以空快取 publish／執行三個 DLL consumers，安裝 tool 並對兩版各生成兩次；會清除繼承的 MYFHIRSDK endpoint 環境變數，不會自動連到你的 server。結果寫到指定的 verification 目錄。

CI 在 V7 的 Windows／Ubuntu 工作中執行同樣步驟，artifact 名稱為 `fhir-external-delivery-windows-latest`／`fhir-external-delivery-ubuntu-latest`。下載整包時保留 `.config/`；workflow 已開啟 hidden files 上傳。原有 `fhir-v7-*` artifacts 仍保存歷史格式的交付驗收證據。

## 接收者：移到 repository 外

將整包複製或解壓縮到獨立目錄，例如 `D:\FHIR_DELIVERY`，再依 README 執行 R4／R5／Dual 範例及生成命令。不要只傳 DLL 而漏掉需要的其他元件，也不要混用不同交付包的檔案。

| 內容 | 用途 |
| --- | --- |
| `sdk/MyFhirSdk.dll` | Parser、Serializer、Validator、Client |
| `sdk/MyFhirSdk.Runtime.dll` | 共用 kernel |
| `sdk/MyFhirSdk.ModelContracts.dll` | provider 契約 |
| `sdk/MyFhirSdk.ModelSupport.dll` | 共用 codecs／validation 支援 |
| `sdk/MyFhirSdk.R4.Models.dll` | R4 模型與 provider |
| `sdk/MyFhirSdk.R5.Models.dll` | R5 模型與 provider；目前 SDK 仍依賴 |
| `feed/*.nupkg` | 安裝用 CodeGen Tool，非 SDK NuGet 套件 |
| `inputs/` | 鎖定的 core packages 與來源 hashes |
| `examples/` | 只引用交付 DLL 的 R4／R5／Dual 範例 |
| `delivery-manifest.json` | 來源 fingerprint 與交付檔案 SHA-256 |

範例預設為離線的 JSON／validation／受控 HTTP read。要讀取外部 server，需同時設定該版 endpoint 與已存在的 Patient ID；不是未 Create 就憑空讀取資料。完整 publish 輸出包含應用程式及 `.deps.json`／`.runtimeconfig.json`，目標機需 .NET 9 runtime。

## 交付驗證與來源身分

正式交付時，先提交所有程式與文件，再從乾淨 checkout 重新匯出；`delivery-manifest.json` 的 `sourceRevision` 應是預期 commit，`workingTreeDirty` 應為 false。開發中匯出允許 dirty，但會明確記錄，不能只引用 HEAD 當成整包的來源。

檢查 manifests 的檔案 hashes、執行三個範例、生成兩版 models／primitives；外部 server 結果另外記錄實測版本、操作及來源包 hash，不保存憑證或真實病人資料。

此交付不會自動發布套件、修改 main、執行外部寫入或更新歷史 baseline。V7 自動 CI 與使用者回報的手動驗收見 [結案紀錄](V7_Closeout.md)。
