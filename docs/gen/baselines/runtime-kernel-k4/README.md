# K4 Runtime compiler contract evidence

2026-10-12，Tool/CodeGen 1.2.0、runtime-kernel-v2、descriptor schema 2、
model/primitive manifest schema 3。

- cli-help-1.2.0.txt：支援 model --compiler-source 覆寫的 CLI help。
- directory-manifest-1.2.0.json：固定 Runtime/reference/auxiliary source provenance。
- directory-artifact-inventory-1.2.0.txt：21 C# sources 與一份 manifest。
- directory-artifact-hashes-1.2.0.txt：UTF-8/LF artifact bytes hashes。
- package-primitive-fixture-contract.txt：沿用固定官方 R5 archive 的 primitive fixture oracle。

K0/kernel-accessor/primitive-tgz-input 歷史證據不改寫。C# hashes 維持固定，
新版本改變的是 descriptor/source inventory 與 manifest provenance。
PrimitiveTgzInputBaselineTests 同時驗證新 snapshot 與 historical source hashes。

Package exact layout 由 Tests/CodeGen/Packaging/codegen-tool-package-layout.txt 驗證；
PE/source/policy hashes 由 runtime-contract.json、packaging tests 與
eng/Write-CodeGenToolPackageInventory.ps1 驗證。DLL/nupkg/TRX 不提交。

完整本機結果、gate 命令及尚待驗收項目見
[Implementation Guide K4](../../MyFhirSdk_Runtime_Kernel_Extraction_Implementation_Guide.md#k4遷移-versioned-runtime-contract)。
