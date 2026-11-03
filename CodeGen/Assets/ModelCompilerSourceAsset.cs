using System.Security.Cryptography;
using System.Text;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;

namespace MyFhirSdk.CodeGen.Assets;

/// <summary>The real SDK-owned auxiliary declaration used by model compilation.</summary>
public static class ModelCompilerSourceAsset
{
    public const string LogicalName = "SimpleQuantity.cs";
    public const string Version = "simple-quantity-source-v1";
    // SHA-256 of UTF-8 source with LF line endings; platform-independent source bytes.
    public const string Sha256 = "321d5cd03d6ec3f2e70a26e608f088563c1f4d7c3ad4e680488d47f0e0affde8";

    internal static RuntimeCompilerSource ExpectedIdentity(Generation.FhirReleaseConfiguration release) =>
        new(LogicalName, release.CompilerSourceVersion, release.TypeNamespace + ".SimpleQuantity",
            "utf8-lf", release.CompilerSourceSha256);

    public static GeneratedSource Read(string? path = null, RuntimeContractView? contract = null)
    {
        var release = Generation.FhirReleaseConfiguration.ForVersion(contract?.Compatibility.FhirPackage.FhirVersion ?? "5.0.0");
        path ??= Path.Combine(AppContext.BaseDirectory, "Assets", "CompilerSources", release.Release == "R5" ? "" : "R4", LogicalName);
        if (contract is not null)
        {
            var identity = contract.CompilerSources.Single();
            if (identity != ExpectedIdentity(release))
                throw new InvalidOperationException($"Unsupported compiler source contract '{LogicalName}'.");
        }
        // Only the explicit path or the package-owned asset is used. No repository fallback.
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
            throw new InvalidOperationException($"Compiler source '{LogicalName}' must use UTF-8 without BOM.");
        return ValidateContent(new UTF8Encoding(false, true).GetString(bytes), release);
    }

    internal static GeneratedSource ValidateContent(string content, Generation.FhirReleaseConfiguration? release = null)
    {
        var source = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        if (hash != (release ?? Generation.FhirReleaseConfiguration.R5).CompilerSourceSha256)
            throw new InvalidOperationException($"Compiler source '{LogicalName}' hash mismatch: {hash}.");
        return new GeneratedSource("CompilerSources/SimpleQuantity.cs", source);
    }
}
