namespace MyFhirSdk.CodeGen.Loading;

public interface IDefinitionPackageInput
{
    string SourceIdentity { get; }

    ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
