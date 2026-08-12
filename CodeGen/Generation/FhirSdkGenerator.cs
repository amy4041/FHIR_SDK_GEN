using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class FhirSdkGenerator
{
    private readonly StructureDefinitionLoader _loader;
    private readonly StructureDefinitionParser _parser;
    private readonly CSharpClassRenderer _renderer;
    private readonly RoslynCompilationValidator _compilationValidator;
    private readonly GeneratedFileWriter _writer;

    public FhirSdkGenerator(string repositoryRoot)
        : this(
            new StructureDefinitionLoader(),
            new StructureDefinitionParser(),
            new CSharpClassRenderer(),
            new RoslynCompilationValidator(),
            new GeneratedFileWriter(repositoryRoot))
    {
    }

    public FhirSdkGenerator(
        StructureDefinitionLoader loader,
        StructureDefinitionParser parser,
        CSharpClassRenderer renderer,
        RoslynCompilationValidator compilationValidator,
        GeneratedFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(compilationValidator);
        ArgumentNullException.ThrowIfNull(writer);

        _loader = loader;
        _parser = parser;
        _renderer = renderer;
        _compilationValidator = compilationValidator;
        _writer = writer;
    }

    public async Task<GenerationResult<IReadOnlyList<string>>> GenerateAsync(
        GeneratorOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loadResult = await _loader.LoadAsync(
            options.InputPath,
            options.FhirVersion,
            cancellationToken);
        if (!loadResult.IsSuccess)
        {
            return Failure(loadResult.Diagnostics);
        }

        var selectionResult = SelectDefinitions(loadResult.Value, options);
        if (!selectionResult.IsSuccess)
        {
            return Failure(selectionResult.Diagnostics);
        }

        var modelResult = ParseModels(selectionResult.Value, options);
        if (!modelResult.IsSuccess)
        {
            return Failure(modelResult.Diagnostics);
        }

        var sourceResult = RenderSources(modelResult.Value);
        if (!sourceResult.IsSuccess)
        {
            return Failure(sourceResult.Diagnostics);
        }

        var compilationResult = _compilationValidator.Validate(sourceResult.Value);
        if (!compilationResult.IsSuccess)
        {
            return Failure(compilationResult.Diagnostics);
        }

        return await _writer.WriteAsync(
            options.OutputPath,
            sourceResult.Value,
            cancellationToken);
    }

    private static GenerationResult<IReadOnlyList<LoadedStructureDefinition>>
        SelectDefinitions(
            IReadOnlyList<LoadedStructureDefinition> loadedDefinitions,
            GeneratorOptions options)
    {
        var definitionsByType = loadedDefinitions
            .Where(loaded => !string.IsNullOrWhiteSpace(loaded.Definition.Type))
            .GroupBy(loaded => loaded.Definition.Type!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        var diagnostics = new List<GeneratorDiagnostic>();
        var selectedDefinitions = new List<LoadedStructureDefinition>();

        foreach (var typeName in options.TypeNames)
        {
            if (!definitionsByType.TryGetValue(typeName, out var matches))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.UnsupportedDefinition,
                    GeneratorDiagnosticSeverity.Error,
                    $"Requested FHIR type '{typeName}' was not found in the input.",
                    options.InputPath));
                continue;
            }

            if (matches.Length > 1)
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidInput,
                    GeneratorDiagnosticSeverity.Error,
                    $"Requested FHIR type '{typeName}' has multiple input definitions.",
                    string.Join(", ", matches.Select(match => match.SourceFile))));
                continue;
            }

            selectedDefinitions.Add(matches[0]);
        }

        return new GenerationResult<IReadOnlyList<LoadedStructureDefinition>>(
            selectedDefinitions
                .OrderBy(
                    definition => definition.Definition.Type,
                    StringComparer.Ordinal)
                .ToArray(),
            diagnostics.ToArray());
    }

    private GenerationResult<IReadOnlyList<FhirTypeModel>> ParseModels(
        IReadOnlyList<LoadedStructureDefinition> selectedDefinitions,
        GeneratorOptions options)
    {
        var previewTypeNames = options.TypeNames.ToHashSet(StringComparer.Ordinal);
        var models = new List<FhirTypeModel>();
        var diagnostics = new List<GeneratorDiagnostic>();

        foreach (var loadedDefinition in selectedDefinitions)
        {
            var parseResult = _parser.Parse(
                loadedDefinition,
                options.TargetNamespace,
                previewTypeNames);
            diagnostics.AddRange(parseResult.Diagnostics);
            if (parseResult.Value is not null)
            {
                models.Add(parseResult.Value);
            }
        }

        return new GenerationResult<IReadOnlyList<FhirTypeModel>>(
            models.ToArray(),
            diagnostics.ToArray());
    }

    private GenerationResult<IReadOnlyList<GeneratedSource>> RenderSources(
        IReadOnlyList<FhirTypeModel> models)
    {
        try
        {
            var sources = models
                .Select(model => new GeneratedSource(
                    $"{model.CSharpName}.g.cs",
                    _renderer.Render(model)))
                .OrderBy(source => source.FileName, StringComparer.Ordinal)
                .ToArray();

            return new GenerationResult<IReadOnlyList<GeneratedSource>>(
                sources,
                Array.Empty<GeneratorDiagnostic>());
        }
        catch (Exception exception)
        {
            return new GenerationResult<IReadOnlyList<GeneratedSource>>(
                Array.Empty<GeneratedSource>(),
                [new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.CompilationFailure,
                    GeneratorDiagnosticSeverity.Error,
                    $"Could not render generated source: {exception.Message}",
                    "<generated-source-batch>")]);
        }
    }

    private static GenerationResult<IReadOnlyList<string>> Failure(
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<IReadOnlyList<string>>(
            Array.Empty<string>(),
            diagnostics.ToArray());
    }
}
