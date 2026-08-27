using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Policy;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class FhirSdkGenerator
{
    private readonly StructureDefinitionLoader _loader;
    private readonly PrimitiveGenerationPolicyLoader _policyLoader;
    private readonly PrimitiveGenerationPolicyValidator _policyValidator;
    private readonly CSharpClassRenderer _renderer;
    private readonly RoslynCompilationValidator _compilationValidator;
    private readonly GeneratedFileWriter _writer;

    public FhirSdkGenerator(string repositoryRoot)
        : this(
            new StructureDefinitionLoader(),
            new PrimitiveGenerationPolicyLoader(),
            new PrimitiveGenerationPolicyValidator(),
            new CSharpClassRenderer(),
            new RoslynCompilationValidator(),
            new GeneratedFileWriter(repositoryRoot))
    {
    }

    public FhirSdkGenerator(
        StructureDefinitionLoader loader,
        PrimitiveGenerationPolicyLoader policyLoader,
        PrimitiveGenerationPolicyValidator policyValidator,
        CSharpClassRenderer renderer,
        RoslynCompilationValidator compilationValidator,
        GeneratedFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(policyLoader);
        ArgumentNullException.ThrowIfNull(policyValidator);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(compilationValidator);
        ArgumentNullException.ThrowIfNull(writer);

        _loader = loader;
        _policyLoader = policyLoader;
        _policyValidator = policyValidator;
        _renderer = renderer;
        _compilationValidator = compilationValidator;
        _writer = writer;
    }

    public async Task<GenerationResult<IReadOnlyList<string>>> GenerateAsync(
        GeneratorOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var policyLoadResult = await _policyLoader.LoadAsync(
            options.PrimitivePolicyPath,
            cancellationToken);
        if (!policyLoadResult.IsSuccess || policyLoadResult.Value is null)
        {
            return Failure(policyLoadResult.Diagnostics);
        }

        var policyResult = _policyValidator.Validate(
            policyLoadResult.Value,
            Path.GetFullPath(options.PrimitivePolicyPath));
        if (!policyResult.IsSuccess || policyResult.Value is null)
        {
            return Failure(policyResult.Diagnostics);
        }

        if (!string.Equals(
                policyResult.Value.FhirVersion,
                options.FhirVersion,
                StringComparison.Ordinal))
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.FhirVersionMismatch,
                GeneratorDiagnosticSeverity.Error,
                $"Primitive policy FHIR version " +
                $"'{policyResult.Value.FhirVersion}' does not match requested " +
                $"version '{options.FhirVersion}'.",
                policyResult.Value.SourceFile,
                DefinitionVersion: policyResult.Value.FhirVersion)]);
        }

        var parser = new StructureDefinitionParser(
            new CSharpTypeMapper(new PrimitiveTypeMappingView(policyResult.Value)));

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

        var modelResult = ParseModels(selectionResult.Value, options, parser);
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

    private static GenerationResult<IReadOnlyList<FhirTypeModel>> ParseModels(
        IReadOnlyList<LoadedStructureDefinition> selectedDefinitions,
        GeneratorOptions options,
        StructureDefinitionParser parser)
    {
        var previewTypeNames = options.TypeNames.ToHashSet(StringComparer.Ordinal);
        var models = new List<FhirTypeModel>();
        var diagnostics = new List<GeneratorDiagnostic>();

        foreach (var loadedDefinition in selectedDefinitions)
        {
            var parseResult = parser.Parse(
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
