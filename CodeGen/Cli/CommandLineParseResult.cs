using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen.Cli;

public sealed record CommandLineParseResult(
    string? Error,
    bool ShowHelp,
    PrimitiveGenerationOptions? PrimitiveOptions = null,
    ModelGenerationOptions? ModelOptions = null)
{
    public bool IsSuccess => PrimitiveOptions is not null || ModelOptions is not null;
}
