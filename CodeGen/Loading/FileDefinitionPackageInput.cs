namespace MyFhirSdk.CodeGen.Loading;

public sealed class FileDefinitionPackageInput : IDefinitionPackageInput
{
    private readonly string _path;

    public FileDefinitionPackageInput(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A package archive path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
    }

    public string SourceIdentity => _path;

    public ValueTask<Stream> OpenReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream>(File.OpenRead(_path));
    }
}
