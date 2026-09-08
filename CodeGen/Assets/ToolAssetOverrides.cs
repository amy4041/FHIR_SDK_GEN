namespace MyFhirSdk.CodeGen.Assets;

public sealed record ToolAssetOverrides(
    string? PolicyRoot = null,
    string? RuntimeContractPath = null,
    IReadOnlyList<string>? RuntimeReferencePaths = null)
{
    public IReadOnlyList<string> RuntimeReferences =>
        RuntimeReferencePaths ?? Array.Empty<string>();
}
