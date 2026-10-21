# R4／R5 外部使用範例

本文件以 `Export-FhirDelivery.py` 匯出的交付包根目錄為工作目錄。將整包複製到 repository 外再執行；例如 `D:\FHIR_DELIVERY`。需要 .NET SDK `9.0.317`，執行已 publish 的程式則只需 .NET 9 runtime。

此處在 Git 中的 `.csproj.template` 是匯出前的範本；exporter 會轉為 `.csproj`。交付包內的專案只有 DLL references，不需原始 repository、ProjectReference 或 SDK NuGet feed。

## 內容

```text
delivery/
├─ global.json
├─ NuGet.Config
├─ delivery-manifest.json
├─ sdk/                         六個 MyFhirSdk implementation DLL
├─ feed/                        MyFhirSdk.CodeGen.Tool.1.3.1.nupkg
├─ inputs/                      固定的 R4／R5 core tgz 與 package locks
├─ .config/dotnet-tools.json
└─ examples/
   ├─ SdkReferences.props
   ├─ Shared/Program.cs
   ├─ R4/R4Example.csproj
   ├─ R5/R5Example.csproj
   └─ Dual/DualExample.csproj
```

## SDK 範例

在交付包根目錄執行：

```powershell
dotnet run --project examples/R4/R4Example.csproj -c Release
dotnet run --project examples/R5/R5Example.csproj -c Release
dotnet run --project examples/Dual/DualExample.csproj -c Release
```

預設執行 synthetic Patient 的解析、序列化、validation 與自訂 handler 的 HTTP read，不需外部服務。R4 的 Attachment.size 使用 JSON number；R5 使用 JSON string。Dual 在同一程序使用兩個獨立 context。

若要實際讀取 server 上已存在的 Patient，設定對應版本的 endpoint 與 logical ID：

```powershell
$env:MYFHIRSDK_R4_INTEGRATION_BASE_URL = 'https://your-r4-server.example/fhir/'
$env:MYFHIRSDK_R4_PATIENT_ID = 'existing-patient-id'
dotnet run --project examples/R4/R4Example.csproj -c Release
```

R5 使用 `MYFHIRSDK_R5_INTEGRATION_BASE_URL`、`MYFHIRSDK_R5_PATIENT_ID`。如需 bearer token，設定 `MYFHIRSDK_R4_INTEGRATION_BEARER_TOKEN` 或 `MYFHIRSDK_R5_INTEGRATION_BEARER_TOKEN`。程式不輸出 Patient、endpoint、ID 或 token。範例只執行 GET；Patient 必須事先存在。只設定 endpoint 或只設定 ID 會失敗，避免誤以為已完成外部測試。

R4 使用 `R4ModelProvider` 建立 `FhirModelContext`；R5 使用 `R5ModelContext.Default`。不要把某一版本的 CLR model 傳給另一版本 context。呼叫端持有及釋放 HttpClient。

部署時使用完整 publish 輸出：

```powershell
dotnet publish examples/Dual/DualExample.csproj -c Release --self-contained false -p:UseAppHost=false -o publish/Dual
dotnet publish/Dual/DualExample.dll
```

SDK 目前仍依賴 R5 Models，因此 R4 範例也保留 R5 Models。不要混用不同輪建置的六個 DLL，也不要把 generator 內的 compiler reference 當作 runtime DLL。

## Generator 範例

在交付包根目錄執行，使用這份交付包專屬的 NuGet cache。`NuGet.Config` 只指向包內的 `feed/`：

```powershell
$previousPackages = $env:NUGET_PACKAGES
try {
    $env:NUGET_PACKAGES = Join-Path $PWD '.tool-cache'
    dotnet tool restore --configfile ./NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }

    dotnet myfhir-codegen --mode model --input ./inputs/hl7.fhir.r4.core-4.0.1.tgz --output ./generated/r4 --fhir-version 4.0.1 --package-id hl7.fhir.r4.core --package-version 4.0.1
    if ($LASTEXITCODE -ne 0) { throw 'R4 model generation failed.' }
    dotnet myfhir-codegen --mode primitive --input ./inputs/hl7.fhir.r4.core-4.0.1.tgz --output ./generated/r4/Generated/R4/Primitives --fhir-version 4.0.1 --package-id hl7.fhir.r4.core --package-version 4.0.1
    if ($LASTEXITCODE -ne 0) { throw 'R4 primitive generation failed.' }

    dotnet myfhir-codegen --mode model --input ./inputs/hl7.fhir.r5.core-5.0.0.tgz --output ./generated/r5 --fhir-version 5.0.0 --package-id hl7.fhir.r5.core --package-version 5.0.0
    if ($LASTEXITCODE -ne 0) { throw 'R5 model generation failed.' }
    dotnet myfhir-codegen --mode primitive --input ./inputs/hl7.fhir.r5.core-5.0.0.tgz --output ./generated/r5/Generated/R5/Primitives --fhir-version 5.0.0 --package-id hl7.fhir.r5.core --package-version 5.0.0
    if ($LASTEXITCODE -ne 0) { throw 'R5 primitive generation failed.' }
} finally {
    $env:NUGET_PACKAGES = $previousPackages
}
```

請先生成 model，再生成其下的 primitives；重新生成 model 會替換輸出目錄。不要將手寫程式放進生成目錄。

Tool 內已包含 policies、SimpleQuantity auxiliary source 與 canonical Runtime compiler reference，不需要另找 repository 內的檔案。此版本只接受固定的 R4 `4.0.1`／R5 `5.0.0` core inputs。

生成結果是 C# 與 manifests，並非完整 Models DLL。完整 Models 還需相應 provider、composition 與版本補充型別；一般應用程式直接使用交付的 Models DLL 即可。

## 核對交付身分

`delivery-manifest.json` 保存來源 revision、working tree 是否有變更、來源 fingerprint、工具與 FHIR input 版本，以及包內每個交付檔案的 SHA-256。它不包含自身 hash，也不代表已完成外部驗收。

每次換來源請匯出到全新目錄，再整包複製。工具版本暫仍為 `1.3.1`，不要用新的同版本 nupkg 覆蓋舊包並沿用舊 cache；新的交付目錄起初不應存在 `.tool-cache/`。

公開／私人 NuGet 發布、完整 terminology／FHIRPath、R4 IG／TW Core 與跨版本資料轉換不在本交付範圍。
