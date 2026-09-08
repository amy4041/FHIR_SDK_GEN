using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Assets;

namespace MyFhirSdk.CodeGen.Cli;

public sealed record CommandLineParseResult(
    string? Error,
    bool ShowHelp,
    PrimitiveGenerationOptions? PrimitiveOptions = null,
    ModelGenerationOptions? ModelOptions = null,
    ToolAssetOverrides? AssetOverrides = null)
{
    public bool IsSuccess => PrimitiveOptions is not null || ModelOptions is not null;
}
