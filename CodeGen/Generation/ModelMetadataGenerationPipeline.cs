using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
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
    private readonly CompositionStructureValidator _structureValidator;
    private readonly FhirReleaseConfiguration _release;

    public ModelMetadataGenerationPipeline(
        RuntimeContractView runtimeContract,
        RoslynCompilationValidator compilationValidator,
        FhirReleaseConfiguration? release = null)
        : this(
            new ResourceBackboneGenerationPipeline(
                new ComplexDatatypeRenderer(),
                new ResourceBackboneRenderer(),
                compilationValidator),
            new ModelMetadataIrBuilder(runtimeContract),
            new ModelMetadataRenderer(release),
            new ValidationCompositionRenderer(release),
            compilationValidator, release)
    {
    }

    public ModelMetadataGenerationPipeline(
        ResourceBackboneGenerationPipeline modelPipeline,
        ModelMetadataIrBuilder metadataBuilder,
        ModelMetadataRenderer metadataRenderer,
        ValidationCompositionRenderer validationRenderer,
        RoslynCompilationValidator compilationValidator,
        FhirReleaseConfiguration? release = null)
    {
        _release = release ?? FhirReleaseConfiguration.R5;
        _structureValidator = new(_release);
        ArgumentNullException.ThrowIfNull(modelPipeline);
        ArgumentNullException.ThrowIfNull(metadataBuilder);
        ArgumentNullException.ThrowIfNull(metadataRenderer);
        ArgumentNullException.ThrowIfNull(validationRenderer);
        ArgumentNullException.ThrowIfNull(compilationValidator);
        _modelPipeline = modelPipeline;
        _metadataBuilder = metadataBuilder;
        _metadataRenderer = metadataRenderer;
        _validationRenderer = validationRenderer;
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
                    _release.MetadataPath,
                    _metadataRenderer.Render(metadata)),
                new GeneratedSource(
                    _release.ValidationPath,
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
        // Models were compiled by _modelPipeline. SDK-owned composition semantic
        // compatibility is checked by a real SDK build with this generated output.
        var structure = _structureValidator.Validate(metadataSources);
        if (!structure.IsSuccess)
        {
            return Failure(structure.Diagnostics);
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
