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

        return _joiner.Join(inventoryResult.Value, policyResult.Value);
    }

    private static GenerationResult<PrimitiveInventoryPolicyCoverage?> Failure(
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        return new GenerationResult<PrimitiveInventoryPolicyCoverage?>(
            null,
            diagnostics.ToArray());
    }
}
