using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen.Cli;

public sealed class GeneratorCli
{
    private readonly FhirSdkGenerator _generator;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly GeneratorCommandLineParser _commandLineParser;

    public GeneratorCli(
        FhirSdkGenerator generator,
        TextWriter output,
        TextWriter error,
        GeneratorCommandLineParser? commandLineParser = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        _generator = generator;
        _output = output;
        _error = error;
        _commandLineParser = commandLineParser ?? new GeneratorCommandLineParser();
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parseResult = _commandLineParser.Parse(args);
        if (parseResult.ShowHelp)
        {
            await _output.WriteLineAsync(GeneratorCommandLineParser.Usage);
            return 0;
        }

        if (!parseResult.IsSuccess)
        {
            await _error.WriteLineAsync(GeneratorCommandLineParser.Usage);
            await _error.WriteLineAsync();
            await _error.WriteLineAsync(parseResult.Error);
            return 1;
        }

        var generationResult = await _generator.GenerateAsync(
            parseResult.Options!,
            cancellationToken);
        if (!generationResult.IsSuccess)
        {
            await WriteDiagnosticsAsync(generationResult.Diagnostics);
            return GeneratorExitCodeMapper.GetExitCode(
                generationResult.Diagnostics,
                fallback: 3);
        }

        foreach (var outputFile in generationResult.Value)
        {
            await _output.WriteLineAsync($"Generated {outputFile}");
        }

        return 0;
    }

    private async Task WriteDiagnosticsAsync(
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            await _error.WriteLineAsync(
                $"[{diagnostic.Code}] {diagnostic.Severity}: " +
                $"{diagnostic.SourceFile}: {diagnostic.Message}");
        }
    }
}
