namespace MyFhirSdk.CodeGen.Writing;

public sealed class OutputSafetyContext
{
    public OutputSafetyContext(
        string toolInstallationDirectory,
        IEnumerable<string>? protectedAssetPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolInstallationDirectory);
        ToolInstallationDirectory = Path.GetFullPath(toolInstallationDirectory);
        ProtectedAssetPaths = Array.AsReadOnly((protectedAssetPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray());
    }

    public string ToolInstallationDirectory { get; }

    public IReadOnlyList<string> ProtectedAssetPaths { get; }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
