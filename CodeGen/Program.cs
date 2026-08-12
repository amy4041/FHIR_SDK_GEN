using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen;

public static class Program
{
    private const string Usage =
        """
        Usage:
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --input <path> \
            --output <path> \
            --namespace <namespace> \
            --fhir-version <version> \
            --type <fhir-type> [--type <fhir-type> ...]
        """;

    public static int Main(string[] args)
    {
        return RunAsync(
                args,
                FindRepositoryRoot(Directory.GetCurrentDirectory()),
                Console.Out,
                Console.Error)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task<int> RunAsync(
        string[] args,
        string repositoryRoot,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var optionsResult = ParseOptions(args);
        if (optionsResult.ShowHelp)
        {
            await output.WriteLineAsync(Usage);
            return 0;
        }

        if (optionsResult.Options is null)
        {
            await error.WriteLineAsync(Usage);
            await error.WriteLineAsync();
            await error.WriteLineAsync(optionsResult.Error);
            return 1;
        }

        var options = optionsResult.Options;
        var loadResult = await new StructureDefinitionLoader().LoadAsync(
            options.InputPath,
            options.FhirVersion,
            cancellationToken);
        if (!loadResult.IsSuccess)
        {
            await WriteDiagnosticsAsync(error, loadResult.Diagnostics);
            return GetExitCode(loadResult.Diagnostics, fallback: 2);
        }

        var definitionsByType = loadResult.Value
            .Where(loaded => !string.IsNullOrWhiteSpace(loaded.Definition.Type))
            .GroupBy(loaded => loaded.Definition.Type!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var selectionDiagnostics = new List<GeneratorDiagnostic>();
        var selectedDefinitions = new List<LoadedStructureDefinition>();

        foreach (var typeName in options.TypeNames)
        {
            if (!definitionsByType.TryGetValue(typeName, out var matches))
            {
                selectionDiagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.UnsupportedDefinition,
                    GeneratorDiagnosticSeverity.Error,
                    $"Requested FHIR type '{typeName}' was not found in the input.",
                    options.InputPath));
                continue;
            }

            if (matches.Length > 1)
            {
                selectionDiagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    GeneratorDiagnosticSeverity.Error,
                    $"Requested FHIR type '{typeName}' has multiple input definitions.",
                    string.Join(", ", matches.Select(match => match.SourceFile))));
                continue;
            }

            selectedDefinitions.Add(matches[0]);
        }

        if (selectionDiagnostics.Count > 0)
        {
            await WriteDiagnosticsAsync(error, selectionDiagnostics);
            return GetExitCode(selectionDiagnostics, fallback: 3);
        }

        var parser = new StructureDefinitionParser();
        var previewTypeNames = options.TypeNames.ToHashSet(StringComparer.Ordinal);
        var models = new List<Models.FhirTypeModel>();
        var parseDiagnostics = new List<GeneratorDiagnostic>();

        foreach (var loadedDefinition in selectedDefinitions.OrderBy(
                     definition => definition.Definition.Type,
                     StringComparer.Ordinal))
        {
            var parseResult = parser.Parse(
                loadedDefinition,
                options.TargetNamespace,
                previewTypeNames);
            parseDiagnostics.AddRange(parseResult.Diagnostics);
            if (parseResult.Value is not null)
            {
                models.Add(parseResult.Value);
            }
        }

        if (parseDiagnostics.Any(diagnostic =>
                diagnostic.Severity == GeneratorDiagnosticSeverity.Error))
        {
            await WriteDiagnosticsAsync(error, parseDiagnostics);
            return GetExitCode(parseDiagnostics, fallback: 3);
        }

        IReadOnlyList<GeneratedSource> generatedSources;
        try
        {
            var renderer = new CSharpClassRenderer();
            generatedSources = models
                .Select(model => new GeneratedSource(
                    $"{model.CSharpName}.g.cs",
                    renderer.Render(model)))
                .OrderBy(source => source.FileName, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception)
        {
            await WriteDiagnosticsAsync(error,
                [new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.CompilationFailure,
                    GeneratorDiagnosticSeverity.Error,
                    $"Could not render generated source: {exception.Message}",
                    "<generated-source-batch>")]);
            return 4;
        }

        var compilationResult = new RoslynCompilationValidator().Validate(generatedSources);
        if (!compilationResult.IsSuccess)
        {
            await WriteDiagnosticsAsync(error, compilationResult.Diagnostics);
            return 4;
        }

        var writeResult = await new GeneratedFileWriter(repositoryRoot).WriteAsync(
            options.OutputPath,
            generatedSources,
            cancellationToken);
        if (!writeResult.IsSuccess)
        {
            await WriteDiagnosticsAsync(error, writeResult.Diagnostics);
            return 5;
        }

        foreach (var outputFile in writeResult.Value)
        {
            await output.WriteLineAsync($"Generated {outputFile}");
        }

        return 0;
    }

    private static CommandLineParseResult ParseOptions(IReadOnlyList<string> args)
    {
        if (args.Count == 1 &&
            (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
        {
            return new CommandLineParseResult(null, null, ShowHelp: true);
        }

        string? inputPath = null;
        string? outputPath = null;
        string? targetNamespace = null;
        string? fhirVersion = null;
        var typeNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < args.Count; index += 2)
        {
            var option = args[index];
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return InvalidOptions($"Option '{option}' requires a value.");
            }

            var value = args[index + 1];
            string? duplicateOption = option switch
            {
                "--input" when inputPath is not null => option,
                "--output" when outputPath is not null => option,
                "--namespace" when targetNamespace is not null => option,
                "--fhir-version" when fhirVersion is not null => option,
                _ => null
            };
            if (duplicateOption is not null)
            {
                return InvalidOptions($"Option '{duplicateOption}' may only be specified once.");
            }

            switch (option)
            {
                case "--input":
                    inputPath = value;
                    break;
                case "--output":
                    outputPath = value;
                    break;
                case "--namespace":
                    targetNamespace = value;
                    break;
                case "--fhir-version":
                    fhirVersion = value;
                    break;
                case "--type":
                    if (!typeNames.Add(value))
                    {
                        return InvalidOptions(
                            $"FHIR type '{value}' may only be specified once.");
                    }

                    break;
                default:
                    return InvalidOptions($"Unknown option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return InvalidOptions("Required option '--input' was not provided.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return InvalidOptions("Required option '--output' was not provided.");
        }

        if (string.IsNullOrWhiteSpace(targetNamespace))
        {
            return InvalidOptions("Required option '--namespace' was not provided.");
        }

        if (!IsValidNamespace(targetNamespace))
        {
            return InvalidOptions(
                $"Namespace '{targetNamespace}' is not a valid C# namespace.");
        }

        if (string.IsNullOrWhiteSpace(fhirVersion))
        {
            return InvalidOptions("Required option '--fhir-version' was not provided.");
        }

        if (typeNames.Count == 0)
        {
            return InvalidOptions("At least one '--type' option must be provided.");
        }

        return new CommandLineParseResult(
            new CommandLineOptions(
                inputPath,
                outputPath,
                targetNamespace,
                fhirVersion,
                typeNames.OrderBy(typeName => typeName, StringComparer.Ordinal).ToArray()),
            null,
            ShowHelp: false);
    }

    private static bool IsValidNamespace(string value)
    {
        return value.Split('.').All(segment =>
            segment.Length > 0 && SyntaxFacts.IsValidIdentifier(segment));
    }

    private static async Task WriteDiagnosticsAsync(
        TextWriter error,
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            await error.WriteLineAsync(
                $"[{diagnostic.Code}] {diagnostic.Severity}: " +
                $"{diagnostic.SourceFile}: {diagnostic.Message}");
        }
    }

    private static int GetExitCode(
        IEnumerable<GeneratorDiagnostic> diagnostics,
        int fallback)
    {
        var codes = diagnostics.Select(diagnostic => diagnostic.Code).ToHashSet();
        if (codes.Contains(GeneratorDiagnosticCodes.UnsafeOutputPath))
        {
            return 5;
        }

        if (codes.Contains(GeneratorDiagnosticCodes.CompilationFailure))
        {
            return 4;
        }

        if (codes.Any(code => code is
                GeneratorDiagnosticCodes.UnsupportedDefinition or
                GeneratorDiagnosticCodes.UnsupportedSlicing or
                GeneratorDiagnosticCodes.UnsupportedChoiceType or
                GeneratorDiagnosticCodes.UnsupportedContentReference or
                GeneratorDiagnosticCodes.MissingTypeMapping or
                GeneratorDiagnosticCodes.CSharpNameConflict))
        {
            return 3;
        }

        if (codes.Any(code => code is
                GeneratorDiagnosticCodes.InvalidInput or
                GeneratorDiagnosticCodes.FhirVersionMismatch or
                GeneratorDiagnosticCodes.MissingSnapshot or
                GeneratorDiagnosticCodes.MissingDifferential))
        {
            return 2;
        }

        return fallback;
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyFhirSdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startPath);
    }

    private static CommandLineParseResult InvalidOptions(string message) =>
        new(null, message, ShowHelp: false);

    private sealed record CommandLineOptions(
        string InputPath,
        string OutputPath,
        string TargetNamespace,
        string FhirVersion,
        IReadOnlyList<string> TypeNames);

    private sealed record CommandLineParseResult(
        CommandLineOptions? Options,
        string? Error,
        bool ShowHelp);
}
