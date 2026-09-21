using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class PrimitiveInventoryCoveragePipeline
{
    private readonly StructureDefinitionLoader _definitionLoader;
    private readonly PrimitiveDefinitionInventoryBuilder _inventoryBuilder;
    private readonly PrimitiveGenerationPolicyLoader _policyLoader;
    private readonly PrimitiveGenerationPolicyValidator _policyValidator;
    private readonly PrimitiveInventoryPolicyJoiner _joiner;

    public PrimitiveInventoryCoveragePipeline()
        : this(
            new StructureDefinitionLoader(),
            new PrimitiveDefinitionInventoryBuilder(),
            new PrimitiveGenerationPolicyLoader(),
            new PrimitiveGenerationPolicyValidator(),
            new PrimitiveInventoryPolicyJoiner())
    {
    }

    public PrimitiveInventoryCoveragePipeline(
        StructureDefinitionLoader definitionLoader,
        PrimitiveDefinitionInventoryBuilder inventoryBuilder,
        PrimitiveGenerationPolicyLoader policyLoader,
        PrimitiveGenerationPolicyValidator policyValidator,
        PrimitiveInventoryPolicyJoiner joiner)
    {
        ArgumentNullException.ThrowIfNull(definitionLoader);
        ArgumentNullException.ThrowIfNull(inventoryBuilder);
        ArgumentNullException.ThrowIfNull(policyLoader);
        ArgumentNullException.ThrowIfNull(policyValidator);
        ArgumentNullException.ThrowIfNull(joiner);

        _definitionLoader = definitionLoader;
        _inventoryBuilder = inventoryBuilder;
        _policyLoader = policyLoader;
        _policyValidator = policyValidator;
        _joiner = joiner;
    }

    public async Task<GenerationResult<PrimitiveInventoryPolicyCoverage?>> BuildAsync(
        string definitionsPath,
        string policyPath,
        string expectedFhirVersion,
        CancellationToken cancellationToken = default)
    {
        var definitionResult = await _definitionLoader.LoadAsync(
            definitionsPath,
            expectedFhirVersion,
            StructureDefinitionLoadProfile.PrimitiveType,
            cancellationToken);
        if (!definitionResult.IsSuccess)
        {
            return Failure(definitionResult.Diagnostics);
        }

        var inventoryResult = _inventoryBuilder.Build(
            definitionResult.Value,
            expectedFhirVersion);
        if (!inventoryResult.IsSuccess || inventoryResult.Value is null)
        {
            return Failure(inventoryResult.Diagnostics);
        }

        return await JoinPolicyAsync(inventoryResult.Value, policyPath, cancellationToken);
    }

    public async Task<GenerationResult<PrimitiveInventoryPolicyCoverage?>> BuildAsync(
        PrimitiveDefinitionInput input,
        string policyPath,
        DefinitionPackageLoadOptions packageOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(packageOptions);
        if (input.Kind == PrimitiveDefinitionInputKind.Directory)
        {
            return await BuildAsync(input.Path, policyPath, packageOptions.FhirVersion, cancellationToken);
        }
        if (input.Kind != PrimitiveDefinitionInputKind.PackageArchive)
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidInput, GeneratorDiagnosticSeverity.Error,
                "Unknown primitive definition input kind.", input.Path)]);
        }
        var loaded = await new DefinitionPackageLoader().LoadAsync(
            new FileDefinitionPackageInput(input.Path), packageOptions, cancellationToken);
        if (!loaded.IsSuccess || loaded.Value is null) return Failure(loaded.Diagnostics);
        var inventory = new PackagePrimitiveSelector().Select(loaded.Value, packageOptions.FhirVersion);
        if (!inventory.IsSuccess || inventory.Value is null) return Failure(inventory.Diagnostics);
        return await JoinPolicyAsync(inventory.Value, policyPath, cancellationToken);
    }

    private async Task<GenerationResult<PrimitiveInventoryPolicyCoverage?>> JoinPolicyAsync(
        PrimitiveDefinitionInventory inventory,
        string policyPath,
        CancellationToken cancellationToken)
    {
        var policyLoadResult = await _policyLoader.LoadAsync(
            policyPath,
            cancellationToken);
        if (!policyLoadResult.IsSuccess || policyLoadResult.Value is null)
        {
            return Failure(policyLoadResult.Diagnostics);
        }

        var policyResult = _policyValidator.Validate(
            policyLoadResult.Value,
            Path.GetFullPath(policyPath));
        if (!policyResult.IsSuccess || policyResult.Value is null)
        {
            return Failure(policyResult.Diagnostics);
        }

        return _joiner.Join(inventory, policyResult.Value);
    }

    private static GenerationResult<PrimitiveInventoryPolicyCoverage?> Failure(
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<PrimitiveInventoryPolicyCoverage?>(
            null,
            diagnostics.ToArray());
    }
}
