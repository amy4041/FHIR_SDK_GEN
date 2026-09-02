using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class DefinitionInventoryPipeline
{
    private readonly DefinitionPackageLoader _packageLoader;
    private readonly DefinitionInventoryBuilder _inventoryBuilder;

    public DefinitionInventoryPipeline()
        : this(new DefinitionPackageLoader(), new DefinitionInventoryBuilder())
    {
    }

    public DefinitionInventoryPipeline(
        DefinitionPackageLoader packageLoader,
        DefinitionInventoryBuilder inventoryBuilder)
    {
        ArgumentNullException.ThrowIfNull(packageLoader);
        ArgumentNullException.ThrowIfNull(inventoryBuilder);

        _packageLoader = packageLoader;
        _inventoryBuilder = inventoryBuilder;
    }

    public async Task<GenerationResult<DefinitionInventory?>> BuildAsync(
        IDefinitionPackageInput input,
        DefinitionPackageLoadOptions options,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await _packageLoader.LoadAsync(
            input,
            options,
            cancellationToken);
        if (!loadResult.IsSuccess || loadResult.Value is null)
        {
            return Failure(loadResult.Diagnostics);
        }

        return _inventoryBuilder.Build(loadResult.Value);
    }

    private static GenerationResult<DefinitionInventory?> Failure(
        IReadOnlyList<GeneratorDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
}
