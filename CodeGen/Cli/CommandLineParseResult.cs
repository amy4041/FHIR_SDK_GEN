using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen.Cli;

public sealed record CommandLineParseResult(
    GeneratorOptions? Options,
    string? Error,
    bool ShowHelp)
{
    public bool IsSuccess => Options is not null;
}
