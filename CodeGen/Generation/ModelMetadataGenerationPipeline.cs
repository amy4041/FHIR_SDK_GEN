using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Metadata;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class ModelMetadataGenerationPipeline
{
    private readonly ResourceBackboneGenerationPipeline _modelPipeline;
    private readonly ModelMetadataIrBuilder _metadataBuilder;
    private readonly ModelMetadataRenderer _metadataRenderer;
    private readonly ValidationCompositionRenderer _validationRenderer;
    private readonly RoslynCompilationValidator _compilationValidator;

    public ModelMetadataGenerationPipeline()
        : this(
            new ResourceBackboneGenerationPipeline(),
            new ModelMetadataIrBuilder(),
            new ModelMetadataRenderer(),
            new ValidationCompositionRenderer(),
            new RoslynCompilationValidator())
    {
    }

    public ModelMetadataGenerationPipeline(
        ResourceBackboneGenerationPipeline modelPipeline,
        ModelMetadataIrBuilder metadataBuilder,
        ModelMetadataRenderer metadataRenderer,
        ValidationCompositionRenderer validationRenderer,
        RoslynCompilationValidator compilationValidator)
    {
        ArgumentNullException.ThrowIfNull(modelPipeline);
        ArgumentNullException.ThrowIfNull(metadataBuilder);
        ArgumentNullException.ThrowIfNull(metadataRenderer);
        ArgumentNullException.ThrowIfNull(validationRenderer);
        ArgumentNullException.ThrowIfNull(compilationValidator);
        _modelPipeline = modelPipeline;
        _metadataBuilder = metadataBuilder;
        _metadataRenderer = metadataRenderer;
        _validationRenderer = validationRenderer;
        _compilationValidator = compilationValidator;
    }

    public GenerationResult<ModelMetadataGenerationBatch?> Generate(ModelIrBatch modelIr)
    {
        ArgumentNullException.ThrowIfNull(modelIr);
        var modelResult = _modelPipeline.Generate(modelIr);
        if (!modelResult.IsSuccess)
        {
            return Failure(modelResult.Diagnostics);
        }
        var metadataResult = _metadataBuilder.Build(modelIr);
        if (!metadataResult.IsSuccess)
        {
            return Failure(metadataResult.Diagnostics);
        }

        var metadata = metadataResult.Value!;
        GeneratedSource[] metadataSources;
        try
        {
            metadataSources =
            [
                new GeneratedSource(
                    ModelMetadataRenderer.ArtifactPath,
                    _metadataRenderer.Render(metadata)),
                new GeneratedSource(
                    ValidationCompositionRenderer.ArtifactPath,
                    _validationRenderer.Render(metadata))
            ];
        }
        catch (ArgumentException exception)
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                GeneratorDiagnosticSeverity.Error,
                exception.Message,
                "<model-metadata-renderer>")]);
        }

        var compilationSources = modelResult.Value!.Sources
            .Concat(metadataSources)
            .OrderBy(source => source.FileName, StringComparer.Ordinal)
            .ToArray();
        var compilation = _compilationValidator.Validate(compilationSources);
        if (!compilation.IsSuccess)
        {
            return Failure(compilation.Diagnostics);
        }

        return new GenerationResult<ModelMetadataGenerationBatch?>(
            new ModelMetadataGenerationBatch(
                metadata,
                metadataSources.OrderBy(source => source.FileName, StringComparer.Ordinal),
                compilationSources),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static GenerationResult<ModelMetadataGenerationBatch?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
}
