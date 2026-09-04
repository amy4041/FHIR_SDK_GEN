using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Policy;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class ModelGenerationPipeline
{
    public const string DefaultCodeGenVersion = "1.0.0";

    private readonly DefinitionInventoryPipeline _inventoryPipeline = new();
    private readonly PrimitiveGenerationPolicyLoader _primitivePolicyLoader = new();
    private readonly PrimitiveGenerationPolicyValidator _primitivePolicyValidator = new();
    private readonly ModelOwnershipPolicyLoader _ownershipPolicyLoader = new();
    private readonly DefinitionDependencyGraphBuilder _graphBuilder = new();
    private readonly GenerationScopeSelector _scopeSelector = new();
    private readonly ModelIrGenerationPolicyLoader _modelPolicyLoader = new();
    private readonly ModelIrBuilder _irBuilder = new();
    private readonly ModelMetadataGenerationPipeline _renderPipeline = new();
    private readonly ModelGenerationManifestRenderer _manifestRenderer = new();
    private readonly GeneratedFileWriter _writer;

    public ModelGenerationPipeline(string repositoryRoot) =>
        _writer = new GeneratedFileWriter(repositoryRoot);

    public async Task<GenerationResult<ModelGenerationBatch?>> BuildAsync(
        ModelGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var primitiveDocument = await _primitivePolicyLoader.LoadAsync(options.PrimitivePolicyPath, cancellationToken);
        if (!primitiveDocument.IsSuccess || primitiveDocument.Value is null) return Failure(primitiveDocument.Diagnostics);
        var primitivePolicy = _primitivePolicyValidator.Validate(primitiveDocument.Value, Path.GetFullPath(options.PrimitivePolicyPath));
        if (!primitivePolicy.IsSuccess || primitivePolicy.Value is null) return Failure(primitivePolicy.Diagnostics);
        if (!string.Equals(primitivePolicy.Value.FhirVersion, options.FhirVersion, StringComparison.Ordinal))
        {
            return Failure([Diagnostic(GeneratorDiagnosticCodes.FhirVersionMismatch,
                $"Primitive policy FHIR version '{primitivePolicy.Value.FhirVersion}' does not match requested version '{options.FhirVersion}'.",
                options.PrimitivePolicyPath)]);
        }

        var inventoryResult = await _inventoryPipeline.BuildAsync(
            new FileDefinitionPackageInput(options.PackagePath),
            new DefinitionPackageLoadOptions(options.PackageId, options.PackageVersion, options.FhirVersion),
            cancellationToken);
        if (!inventoryResult.IsSuccess || inventoryResult.Value is null) return Failure(inventoryResult.Diagnostics);

        var ownershipResult = await _ownershipPolicyLoader.LoadAsync(options.OwnershipPolicyPath, cancellationToken);
        if (!ownershipResult.IsSuccess || ownershipResult.Value is null) return Failure(ownershipResult.Diagnostics);
        var mappings = new PrimitiveTypeMappingView(primitivePolicy.Value);
        var graphResult = _graphBuilder.Build(inventoryResult.Value, mappings, ownershipResult.Value, options.OwnershipPolicyPath);
        if (!graphResult.IsSuccess || graphResult.Value is null) return Failure(graphResult.Diagnostics);

        var scopeResult = options.SelectedCanonicals.Count == 0
            ? _scopeSelector.SelectAll(graphResult.Value)
            : _scopeSelector.Select(graphResult.Value, options.SelectedCanonicals);
        if (!scopeResult.IsSuccess || scopeResult.Value is null) return Failure(scopeResult.Diagnostics);

        var modelPolicyResult = await _modelPolicyLoader.LoadAsync(options.ModelIrPolicyPaths, cancellationToken);
        if (!modelPolicyResult.IsSuccess || modelPolicyResult.Value is null) return Failure(modelPolicyResult.Diagnostics);
        var irResult = _irBuilder.Build(graphResult.Value, scopeResult.Value, mappings, modelPolicyResult.Value);
        if (!irResult.IsSuccess || irResult.Value is null) return Failure(irResult.Diagnostics);
        var renderResult = _renderPipeline.Generate(irResult.Value);
        if (!renderResult.IsSuccess || renderResult.Value is null) return Failure(renderResult.Diagnostics);

        var sources = renderResult.Value.CompilationSources
            .OrderBy(source => source.FileName, StringComparer.Ordinal)
            .ToArray();
        var collision = sources.GroupBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (collision is not null)
        {
            return Failure([Diagnostic(GeneratorDiagnosticCodes.ModelIrCollision,
                $"Generated artifact path '{collision.Key}' is duplicated.", "<model-generation-batch>")]);
        }

        try
        {
            var policies = new[]
            {
                ("backbone", options.ModelIrPolicyPaths.BackbonePolicyPath),
                ("choice-open-type", options.ModelIrPolicyPaths.ChoicePolicyPath),
                ("model-naming", options.ModelIrPolicyPaths.NamingPolicyPath),
                ("model-ownership", options.OwnershipPolicyPath),
                ("validation-capability", options.ValidationPolicyPath)
            };
            var policyModels = new List<ModelManifestPolicyModel>();
            foreach (var (name, path) in policies.OrderBy(x => x.Item1, StringComparer.Ordinal))
                policyModels.Add(new(name, await HashTextFileAsync(path, cancellationToken)));

            var artifactModels = sources.Select(source => new ModelManifestArtifactModel(
                source.FileName.Replace('\\', '/'), HashText(source.Source))).ToArray();
            var deferred = await ReadDeferredCapabilitiesAsync(
                options.ValidationPolicyPath, options.FhirVersion, cancellationToken);
            var manifest = new ModelGenerationManifestModel(
                options.PackageId, options.PackageVersion, options.FhirVersion,
                await HashBinaryFileAsync(options.PackagePath, cancellationToken),
                primitivePolicy.Value.PolicyVersion,
                await HashTextFileAsync(options.PrimitivePolicyPath, cancellationToken),
                options.CodeGenVersion, primitivePolicy.Value.RuntimeContractVersion,
                options.SelectedCanonicals.Count == 0 ? "full" : "selected",
                options.SelectedCanonicals, policyModels, artifactModels, deferred);
            return new GenerationResult<ModelGenerationBatch?>(
                new ModelGenerationBatch(sources, manifest, _manifestRenderer.Render(manifest)), []);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Failure([Diagnostic(GeneratorDiagnosticCodes.InvalidInput,
                $"Could not create model generation manifest: {exception.Message}", "<model-generation-manifest>")]);
        }
    }

    public async Task<GenerationResult<IReadOnlyList<string>>> GenerateAsync(
        ModelGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = await BuildAsync(options, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
            return new(Array.Empty<string>(), result.Diagnostics);
        return await _writer.WriteArtifactsAsync(options.OutputPath, result.Value.Artifacts, cancellationToken);
    }

    private static async Task<string> HashBinaryFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.GetFullPath(path));
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static async Task<string> HashTextFileAsync(
        string path,
        CancellationToken cancellationToken) =>
        HashText(await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken));

    private static string HashText(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static async Task<IReadOnlyList<ModelManifestCapabilityModel>> ReadDeferredCapabilitiesAsync(
        string path, string expectedFhirVersion, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.GetFullPath(path));
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var actualFhirVersion = document.RootElement.GetProperty("fhirVersion").GetString();
        if (!string.Equals(actualFhirVersion, expectedFhirVersion, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Validation policy FHIR version '{actualFhirVersion}' does not match requested version '{expectedFhirVersion}'.");
        return document.RootElement.GetProperty("capabilities").EnumerateArray()
            .Select(item => new ModelManifestCapabilityModel(
                item.GetProperty("id").GetString()!, item.GetProperty("status").GetString()!))
            .Where(item => item.Status.Contains("preserve-only", StringComparison.Ordinal) ||
                           item.Status.StartsWith("zero-in-", StringComparison.Ordinal))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static GeneratorDiagnostic Diagnostic(string code, string message, string source) =>
        new(code, GeneratorDiagnosticSeverity.Error, message, source);

    private static GenerationResult<ModelGenerationBatch?> Failure(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
}
