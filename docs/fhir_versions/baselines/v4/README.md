# V4 — 共用 Generator 的 R4／R5 release configuration

日期：2026-10-16。前置 V3 commit `d264de5692b4b370bd3c74ac42185861d2d47035` 已提交，使用者確認 CI 通過。
V4 繼續於 `feature/fhir-r4-v0` 開發；尚未 commit／push，新增 CI 待提交後執行。main 不變。

## 實作與相容組合

| 項目 | R4 | R5 |
| --- | --- | --- |
| FHIR／package version | 4.0.1 | 5.0.0 |
| Package | hl7.fhir.r4.core | hl7.fhir.r5.core |
| Official archive | 既有 r4-package-lock.json | 既有 r5-package-lock.json |
| Primitive policy | 1.0.0，19 supported／xhtml unsupported | 1.1.0，20 supported／xhtml unsupported |
| Model namespace | MyFhirSdk.R4.Types／Resources | MyFhirSdk.Types／Resources |
| Primitive namespace | MyFhirSdk.R4.Primitives | MyFhirSdk.Primitives |
| Model output | Generated/R4 | Generated/R5 |
| Metadata entry point | GeneratedR4ModelMetadata | GeneratedR5ModelMetadata |
| Validation entry point | GeneratedR4ValidationRules | GeneratedR5ValidationRules |
| Packaged policies | Policy/R4 | Policy |
| Packaged descriptor | Contracts/R4/runtime-contract.json | Contracts/runtime-contract.json |
| Compiler source | Assets/CompilerSources/R4/SimpleQuantity.cs | Assets/CompilerSources/SimpleQuantity.cs |
| SimpleQuantity base | R4 Quantity | R5 Quantity |
| Runtime contract | runtime-kernel-v2 | runtime-kernel-v2 |

`FhirReleaseConfiguration` 固定 identity、namespace、artifact paths、archive hash 與 auxiliary identity，沒有可變的全域 current version。
Tool／CodeGen 為 1.3.0；descriptor schema 2、manifest schema 3 不變。兩個 descriptor 各自固定 compatibility、policy hashes 與 compiler source CLR identity／version／hash。
兩者使用相同 canonical Runtime compiler reference，SHA-256 `8b990107f921287068ffc8326ce69b28a422dace2fcbf5a657ef50a7adde8e71`。
Runtime API、ModelContracts、ModelSupport、R5 model API 與 852 個 R5 generated C# bytes 不變。
兩份 R5 manifests 更新為 1.3.0／新 descriptor provenance；新的 tool oracle 獨立存於 `tool-1.3.0/`，不覆寫 V0–V3 或 K 系列歷史 evidence。

R4 policies 依鎖定官方 definitions 盤點，移除 R5-only primitive integer64；Attachment.size 使用 unsignedInt。
R4 package metadata type 是 `fhir.core`，R5 是 `Core`。R4 Element／Resource 是獨立 roots，MetadataResource 為 logical specialization。
R4 complex datatypes 官方從 Element／BackboneElement 繼承時，CLR 使用共享 DataType／BackboneType 抽象橋接，以提供 IFhirExtensionValue；官方 graph identities 保留。
這兩個 bridge 也明列並驗證於 R4 naming policy。SimpleQuantity 是版本專屬 constraint auxiliary，其他 constraint profiles 不屬一般 model declarations。

## CLI 與生成邊界

沿用 `--fhir-version`／`--package-id`／`--package-version`，只接受上述兩個 exact 組合，不接受 R4B 或 wildcards。
Model／primitive mode 可省略 `--policy`，使用 installed package 對應 release 的 policy；舊 R5 explicit-policy 命令仍支援。
`--policy-root` 僅用於 model mode，指向該 release 的 policy 目錄；`--canonical` 維持既有 dependency closure 語意。

Model mode 的 `--output` 是生成根目錄，工具在其下建立 `Generated/R4` 或 `Generated/R5`；primitive mode 直接寫入指定目錄。
Models 與 primitives 是兩次命令，生成同一套 Models 時必須使用相同 release、package 與 policies。
所有不相容檢查在 writer 前執行；保留既有 archive／definition diagnostics，合法 archive 再驗證 pinned SHA-256。
Namespace、composition 名稱與 logical output paths 由 release configuration 決定，不對生成 C# 做文字取代。

```powershell
dotnet msbuild eng/MyFhirSdk.CodeGen.Build.proj /t:Pack /p:Configuration=Release
dotnet tool restore --add-source artifacts/packages --ignore-failed-sources
dotnet myfhir-codegen --mode model --input Tests/CodeGen/Fixtures/FhirPackages/R4/hl7.fhir.r4.core-4.0.1.tgz --output artifacts/r4 --fhir-version 4.0.1 --package-id hl7.fhir.r4.core --package-version 4.0.1
dotnet myfhir-codegen --mode primitive --input Tests/CodeGen/Fixtures/FhirPackages/R4/hl7.fhir.r4.core-4.0.1.tgz --output artifacts/r4/Generated/R4/Primitives --fhir-version 4.0.1 --package-id hl7.fhir.r4.core --package-version 4.0.1
```

同一棵輸出目錄必須先生成 models，再生成其下的 primitives。每次命令會替換自己的整個 `--output` 目錄，因此重新生成 models 後也必須重新生成 primitives；不同版本使用各自的 output root。

## 驗證與證據

Review 修正後的本機 G4 Passed：完整 solution 共 993 項，992 通過、0 失敗、1 項外部 Client smoke 因未設定 endpoint 略過；13 個 Python integrity tests 通過，結果見 [summary](summary.json)。
16 項 V4 cases 包含 review 後新增的文件命令整合測試，以及 R4／R5 在兩種 mode 下的 foreign auxiliary CLR identity 拒絕案例；31 項 V2 cases 與 4 項 V3 cases 繼續通過。
R4 生成 657 個 model／composition C#、20 個 primitive／registry C#；R5 對應為 831／21。全部 852 個 R5 C# 與 V3 一致。
本機驗證不代表已執行新增 CI；Windows／Ubuntu CI 待使用者提交後確認。

```powershell
dotnet restore MyFhirSdk.sln
python eng/Test-FhirV4Generator.py --output artifacts/fhir-versions/v4/run
```

G4 先執行 G3／G2 的全 solution regression、Python integrity checks、既有 architecture guards 與 R5 source／API preservation。
新增測試包含 R4 full model compile、重複生成、primitive membership、fresh composition semantic compile，以及正式 context／engines 的基本 R4 行為。
負面案例包含 R5 primitive／model policies、descriptor、auxiliary、package identity、R4B，以及只修改 gzip header 而 identity 不變的 package hash mismatch；輸出 sentinel 保持原樣。
Fresh R4 semantic fixture 沒有 SDK assembly reference；以 neutral contracts／shared support 編譯後，由正式 context／engines 驗證 Patient JSON、Attachment.size number、required rules 與跨版本拒絕。

G4 再從測試 pack 的 nupkg，在 checkout 外建立全新 tool manifest／NuGet cache，僅使用 local feed，官方 package 也複製進隔離目錄。
R4／R5 都先生成 full models，再於同一棵樹下生成 primitives，重複生成並確認兩者全部產物同時存在，記錄各批次與整棵樹的 artifact hashes；R5 輸出與 committed artifacts 精確比對。
錯誤 auxiliary／policy-root／missing source 被拒絕且既有輸出不變；nupkg 不包含 SDK／Models／ModelSupport implementation。
完整 auxiliary identity（logical name、version、CLR type、normalization、hash）由共用定義檢查；installed tool 也驗證 R4／R5 在 primitive／model mode 都拒絕混入另一版本的 CLR type，且整棵輸出樹保持不變。
CI 新增 Windows／Ubuntu G4 job；既有 G1–G3、K3、K6 與 cross-platform gates 保留。

Portable evidence 見 [summary](summary.json)、[V4 cases](v4-test-cases.json) 與 [file hashes](evidence-files.json)。
完整原始 logs、TRX 與 installed tool hashes 留在 ignored `artifacts/fhir-versions/v4/review-fixes/verified/`；先前失敗或較早 source fingerprint 的 development runs 不作為最終通過證據。

## 範圍與後續

Root SDK 排除所有 Generated trees，避免在 repository root 生成 R4 後意外將它編譯進 SDK；隔離 source tree 的實際 MSBuild evaluation 驗證 R4／R5 probes 都被排除。
`.gitignore` 僅開放 R4 的 Primitives／Types／Resources／ModelMetadata 與 manifest 目的地，供 V5 受控加入；G4 記錄 source counts／bytes。
R4 full generator 輸出只保留於 artifacts，沒有新增正式 R4 Models project 或提交整批 R4 生成檔；既有 V1／V2 fixture 維持原樣。
V4 的 fresh R4 provider 是測試用組合。完整 R4 inventory、runtime／primitive lexical 邊界與能力驗收屬 V5，Client 屬 V6，交付驗收屬 V7。
通用 constraint generator、FHIRPath invariants、terminology 與跨版本資料轉換未納入本階段。
Rollback 以完整 V3 source revision 為單位，重建 tool、manifests 與 consumers；不混用 V3 tool 與 V4 descriptor／policies。
