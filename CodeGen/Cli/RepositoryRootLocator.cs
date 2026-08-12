namespace MyFhirSdk.CodeGen.Cli;

public static class RepositoryRootLocator
{
    public static string Find(string startPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyFhirSdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startPath);
    }
}
