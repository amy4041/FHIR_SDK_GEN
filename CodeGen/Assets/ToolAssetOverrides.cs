namespace MyFhirSdk.CodeGen.Assets;

public sealed record ToolAssetOverrides(
    string? PolicyRoot = null,
    string? RuntimeContractPath = null,
    IReadOnlyList<string>? RuntimeReferencePaths = null,
    string? CompilerSourcePath = null)
{
    public IReadOnlyList<string> RuntimeReferences =>
        RuntimeReferencePaths ?? Array.Empty<string>();
}
