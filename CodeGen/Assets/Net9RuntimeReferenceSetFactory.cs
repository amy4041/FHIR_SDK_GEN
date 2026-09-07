namespace MyFhirSdk.CodeGen.Assets;

public static class Net9RuntimeReferenceSetFactory
{
    public const string TargetFramework = "net9.0";

    public static RuntimeReferenceSet Create(string runtimeAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeAssemblyPath);
        var referenceAssemblyDirectory = FindReferenceAssemblyDirectory();
        var paths = Directory
            .EnumerateFiles(referenceAssemblyDirectory, "*.dll")
            .Append(runtimeAssemblyPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        return new RuntimeReferenceSet(TargetFramework, paths);
    }

    private static string FindReferenceAssemblyDirectory()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException(
                "Could not determine the .NET runtime directory.");
        var dotnetRoot = Directory.GetParent(runtimeDirectory)?
            .Parent?
            .Parent?
            .FullName;
        if (string.IsNullOrWhiteSpace(dotnetRoot))
        {
            throw new InvalidOperationException(
                "Could not determine the .NET installation directory.");
        }

        var packRoot = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(packRoot))
        {
            throw new InvalidOperationException(
                $".NET reference assembly pack was not found at '{packRoot}'.");
        }

        var packVersionDirectory = Directory
            .EnumerateDirectories(packRoot, "9.0.*")
            .Select(path => new
            {
                Path = path,
                Version = ParseVersion(Path.GetFileName(path))
            })
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
        if (packVersionDirectory is null)
        {
            throw new InvalidOperationException(
                $"A .NET 9 reference assembly pack was not found at '{packRoot}'.");
        }

        var referenceAssemblyDirectory = Path.Combine(
            packVersionDirectory,
            "ref",
            TargetFramework);
        if (!Directory.Exists(referenceAssemblyDirectory))
        {
            throw new InvalidOperationException(
                $".NET 9 reference assemblies were not found at '{referenceAssemblyDirectory}'.");
        }
        return referenceAssemblyDirectory;
    }

    private static Version? ParseVersion(string value) =>
        Version.TryParse(value, out var version) ? version : null;
}
