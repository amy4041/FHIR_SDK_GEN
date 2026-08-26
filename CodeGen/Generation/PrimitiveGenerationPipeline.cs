using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class PrimitiveGenerationPipeline
{
    public const string DefaultCodeGenVersion = "1.0.0";

    private readonly PrimitiveInventoryCoveragePipeline _coveragePipeline;
    private readonly PrimitiveWrapperModelBuilder _wrapperModelBuilder;
    private readonly PrimitiveRegistryModelBuilder _registryModelBuilder;
    private readonly PrimitiveWrapperRenderer _wrapperRenderer;
    private readonly PrimitiveRegistryCompositionRenderer _registryRenderer;
    private readonly PrimitiveGenerationManifestModelBuilder _manifestModelBuilder;
    private readonly PrimitiveGenerationManifestRenderer _manifestRenderer;
    private readonly RoslynCompilationValidator _wrapperCompilationValidator;
    private readonly PrimitiveRegistryCompositionCompilationValidator
        _registryCompilationValidator;
    private readonly GeneratedFileWriter _writer;

    public PrimitiveGenerationPipeline(string repositoryRoot)
        : this(
            new PrimitiveInventoryCoveragePipeline(),
            new PrimitiveWrapperModelBuilder(),
            new PrimitiveRegistryModelBuilder(),
            new PrimitiveWrapperRenderer(),
            new PrimitiveRegistryCompositionRenderer(),
            new PrimitiveGenerationManifestModelBuilder(),
            new PrimitiveGenerationManifestRenderer(),
            new RoslynCompilationValidator(),
            new PrimitiveRegistryCompositionCompilationValidator(),
            new GeneratedFileWriter(repositoryRoot))
    {
    }

    public PrimitiveGenerationPipeline(
        PrimitiveInventoryCoveragePipeline coveragePipeline,
        PrimitiveWrapperModelBuilder wrapperModelBuilder,
        PrimitiveRegistryModelBuilder registryModelBuilder,
        PrimitiveWrapperRenderer wrapperRenderer,
        PrimitiveRegistryCompositionRenderer registryRenderer,
        PrimitiveGenerationManifestModelBuilder manifestModelBuilder,
        PrimitiveGenerationManifestRenderer manifestRenderer,
        RoslynCompilationValidator wrapperCompilationValidator,
        PrimitiveRegistryCompositionCompilationValidator registryCompilationValidator,
        GeneratedFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(coveragePipeline);
        ArgumentNullException.ThrowIfNull(wrapperModelBuilder);
        ArgumentNullException.ThrowIfNull(registryModelBuilder);
        ArgumentNullException.ThrowIfNull(wrapperRenderer);
        ArgumentNullException.ThrowIfNull(registryRenderer);
        ArgumentNullException.ThrowIfNull(manifestModelBuilder);
        ArgumentNullException.ThrowIfNull(manifestRenderer);
        ArgumentNullException.ThrowIfNull(wrapperCompilationValidator);
        ArgumentNullException.ThrowIfNull(registryCompilationValidator);
        ArgumentNullException.ThrowIfNull(writer);

        _coveragePipeline = coveragePipeline;
        _wrapperModelBuilder = wrapperModelBuilder;
        _registryModelBuilder = registryModelBuilder;
        _wrapperRenderer = wrapperRenderer;
        _registryRenderer = registryRenderer;
        _manifestModelBuilder = manifestModelBuilder;
        _manifestRenderer = manifestRenderer;
        _wrapperCompilationValidator = wrapperCompilationValidator;
        _registryCompilationValidator = registryCompilationValidator;
        _writer = writer;
    }

    public async Task<GenerationResult<PrimitiveGenerationBatch?>> BuildAsync(
        PrimitiveGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var coverageResult = await _coveragePipeline.BuildAsync(
            options.DefinitionsPath,
            options.PolicyPath,
            options.FhirVersion,
            cancellationToken);
        if (!coverageResult.IsSuccess || coverageResult.Value is null)
        {
            return Failure(coverageResult.Diagnostics);
        }

        var coverage = coverageResult.Value;
        var wrapperResult = _wrapperModelBuilder.Build(coverage);
        if (!wrapperResult.IsSuccess)
        {
            return Failure(wrapperResult.Diagnostics);
        }

        var registryResult = _registryModelBuilder.Build(coverage);
        if (!registryResult.IsSuccess || registryResult.Value is null)
        {
            return Failure(registryResult.Diagnostics);
        }

        IReadOnlyList<GeneratedSource> wrappers;
        GeneratedSource composition;
        try
        {
            wrappers = _wrapperRenderer.RenderAll(wrapperResult.Value);
            composition = new GeneratedSource(
                registryResult.Value.FileName,
                _registryRenderer.Render(registryResult.Value));
        }
        catch (Exception exception)
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.CompilationFailure,
                GeneratorDiagnosticSeverity.Error,
                $"Could not render primitive generation batch: {exception.Message}",
                "<primitive-generation-batch>")]);
        }

        var sources = wrappers
            .Append(composition)
            .OrderBy(source => source.FileName, StringComparer.Ordinal)
            .ToArray();
        var manifest = _manifestModelBuilder.Build(coverage, options, sources);
        var batch = new PrimitiveGenerationBatch(
            sources,
            manifest,
            _manifestRenderer.Render(manifest));

        var wrapperCompilation = _wrapperCompilationValidator.Validate(wrappers);
        if (!wrapperCompilation.IsSuccess)
        {
            return Failure(wrapperCompilation.Diagnostics);
        }

        var registryCompilation = _registryCompilationValidator.Validate(
            composition,
            registryResult.Value);
        if (!registryCompilation.IsSuccess)
        {
            return Failure(registryCompilation.Diagnostics);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new GenerationResult<PrimitiveGenerationBatch?>(
            batch,
            Array.Empty<GeneratorDiagnostic>());
    }

    public async Task<GenerationResult<IReadOnlyList<string>>> GenerateAsync(
        PrimitiveGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        var buildResult = await BuildAsync(options, cancellationToken);
        if (!buildResult.IsSuccess || buildResult.Value is null)
        {
            return new GenerationResult<IReadOnlyList<string>>(
                Array.Empty<string>(),
                buildResult.Diagnostics);
        }

        return await _writer.WriteArtifactsAsync(
            options.OutputPath,
            buildResult.Value.Artifacts,
            cancellationToken);
    }

    private static GenerationResult<PrimitiveGenerationBatch?> Failure(
        IReadOnlyList<GeneratorDiagnostic> diagnostics) => new(
            null,
            diagnostics.ToArray());
}
