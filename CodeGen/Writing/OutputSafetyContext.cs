namespace MyFhirSdk.CodeGen.Writing;

public sealed class OutputSafetyContext
{
    private static readonly string[] DefaultDevelopmentProtectedDirectories =
    [
        "core",
        "Types",
        "Resources",
        "Serialization",
        "Validation",
        "CodeGen",
        Path.Combine("Primitives", "Runtime")
    ];

    public OutputSafetyContext(
        string toolInstallationDirectory,
        IEnumerable<string>? protectedAssetPaths = null)
        : this(
            toolInstallationDirectory,
            protectedAssetPaths,
            developmentRepositoryRoot: null,
            developmentProtectedPaths: null)
    {
    }

    private OutputSafetyContext(
        string toolInstallationDirectory,
        IEnumerable<string>? protectedAssetPaths,
        string? developmentRepositoryRoot,
        IEnumerable<string>? developmentProtectedPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolInstallationDirectory);
        ToolInstallationDirectory = Path.GetFullPath(toolInstallationDirectory);
        ProtectedAssetPaths = Array.AsReadOnly((protectedAssetPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray());
        DevelopmentRepositoryRoot = string.IsNullOrWhiteSpace(developmentRepositoryRoot)
            ? null
            : Path.GetFullPath(developmentRepositoryRoot);
        DevelopmentProtectedPaths = Array.AsReadOnly((developmentProtectedPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray());
    }

    public string ToolInstallationDirectory { get; }

    public IReadOnlyList<string> ProtectedAssetPaths { get; }

    public string? DevelopmentRepositoryRoot { get; }

    public IReadOnlyList<string> DevelopmentProtectedPaths { get; }

    public OutputSafetyContext WithDevelopmentRepository(
        string repositoryRoot,
        IEnumerable<string>? protectedRelativePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var root = Path.GetFullPath(repositoryRoot);
        var protectedPaths = (protectedRelativePaths ??
                DefaultDevelopmentProtectedDirectories)
            .Select(path => Path.Combine(root, path));
        return new OutputSafetyContext(
            ToolInstallationDirectory,
            ProtectedAssetPaths,
            root,
            protectedPaths);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
