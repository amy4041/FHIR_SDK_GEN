using System.Security.Cryptography;
using System.Text;
using MyFhirSdk.CodeGen.Compilation;

namespace MyFhirSdk.CodeGen.Assets;

/// <summary>The real SDK-owned auxiliary declaration used by model compilation.</summary>
/// <remarks>K1 embeds the source in the tool. External asset overrides and descriptor
/// provenance are introduced atomically with the Runtime reference in K4.</remarks>
public static class ModelCompilerSourceAsset
{
    public const string LogicalName = "MyFhirSdk.CodeGen.CompilerSources.SimpleQuantity.cs";
    public const string Version = "simple-quantity-source-v1";
    // SHA-256 of UTF-8 source with LF line endings; platform-independent source bytes.
    public const string Sha256 = "321d5cd03d6ec3f2e70a26e608f088563c1f4d7c3ad4e680488d47f0e0affde8";

    public static GeneratedSource Read()
    {
        using var stream = typeof(ModelCompilerSourceAsset).Assembly.GetManifestResourceStream(LogicalName)
            ?? throw new InvalidOperationException($"Required compiler source '{LogicalName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ValidateContent(reader.ReadToEnd());
    }

    internal static GeneratedSource ValidateContent(string content)
    {
        var source = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        if (hash != Sha256)
            throw new InvalidOperationException($"Compiler source '{LogicalName}' hash mismatch: {hash}.");
        return new GeneratedSource("CompilerSources/SimpleQuantity.cs", source);
    }
}
