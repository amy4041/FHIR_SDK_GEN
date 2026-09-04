using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen.Cli;

public sealed record CommandLineParseResult(
    GeneratorOptions? Options,
    string? Error,
    bool ShowHelp,
    PrimitiveGenerationOptions? PrimitiveOptions = null,
    ModelGenerationOptions? ModelOptions = null)
{
    public bool IsSuccess => Options is not null || PrimitiveOptions is not null || ModelOptions is not null;
}
