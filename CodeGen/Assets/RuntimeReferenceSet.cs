using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Assets;

public sealed class RuntimeReferenceSet
{
    public RuntimeReferenceSet(
        string targetFramework,
        IEnumerable<string> referencePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);
        ArgumentNullException.ThrowIfNull(referencePaths);

        var paths = referencePaths.Select(path =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return Path.GetFullPath(path);
        }).ToArray();
        if (paths.Length == 0)
        {
            throw new ArgumentException(
                "At least one compiler reference path is required.",
                nameof(referencePaths));
        }

        TargetFramework = targetFramework;
        ReferencePaths = new ReadOnlyCollection<string>(paths);
    }

    public string TargetFramework { get; }

    public IReadOnlyList<string> ReferencePaths { get; }
}
