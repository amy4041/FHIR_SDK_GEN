using System.Collections.ObjectModel;
using System.Collections.Immutable;
using MyFhirSdk.CodeGen.Contracts;

namespace MyFhirSdk.CodeGen.Assets;

public enum RuntimeReferenceKind
{
    TrustedPlatform,
    RuntimeContract
}

public sealed class ResolvedRuntimeReference
{
    internal ResolvedRuntimeReference(
        string path,
        RuntimeAssemblyIdentity assembly,
        string? targetFramework,
        string? sha256,
        RuntimeReferenceKind kind,
        ImmutableArray<byte> validatedImage)
    {
        Path = path;
        Assembly = assembly;
        TargetFramework = targetFramework;
        Sha256 = sha256;
        Kind = kind;
        ValidatedImage = validatedImage;
    }

    public string Path { get; }

    public RuntimeAssemblyIdentity Assembly { get; }

    public string? TargetFramework { get; }

    public string? Sha256 { get; }

    public RuntimeReferenceKind Kind { get; }

    public string AssemblyIdentity =>
        $"{Assembly.Name}, Version={Assembly.Version}, PublicKeyToken={Assembly.PublicKeyToken}";

    public RuntimeReferenceLogicalIdentity LogicalIdentity => new(
        TargetFramework ?? "<missing>",
        Assembly);

    internal ImmutableArray<byte> ValidatedImage { get; }
}

public sealed record RuntimeReferenceLogicalIdentity(
    string TargetFramework,
    RuntimeAssemblyIdentity Assembly)
{
    public override string ToString() =>
        $"{Assembly.Name}, Version={Assembly.Version}, " +
        $"PublicKeyToken={Assembly.PublicKeyToken}, TargetFramework={TargetFramework}";
}

public sealed class RuntimeReferenceSet
{
    internal RuntimeReferenceSet(
        string targetFramework,
        RuntimeReferenceLogicalIdentity logicalAssemblyIdentity,
        string contractSha256,
        string referenceSha256,
        IEnumerable<ResolvedRuntimeReference> trustedPlatformReferences,
        IEnumerable<ResolvedRuntimeReference> runtimeContractReferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);
        ArgumentNullException.ThrowIfNull(logicalAssemblyIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceSha256);
        ArgumentNullException.ThrowIfNull(trustedPlatformReferences);
        ArgumentNullException.ThrowIfNull(runtimeContractReferences);

        TargetFramework = targetFramework;
        LogicalAssemblyIdentity = logicalAssemblyIdentity;
        ContractSha256 = contractSha256;
        ReferenceSha256 = referenceSha256;
        TrustedPlatformReferences = AsOrderedReadOnly(trustedPlatformReferences);
        RuntimeContractReferences = AsOrderedReadOnly(runtimeContractReferences);
        OrderedReferences = AsOrderedReadOnly(
            TrustedPlatformReferences.Concat(RuntimeContractReferences));
        ReferencePaths = new ReadOnlyCollection<string>(
            OrderedReferences.Select(reference => reference.Path).ToArray());
    }

    public string TargetFramework { get; }

    public RuntimeReferenceLogicalIdentity LogicalAssemblyIdentity { get; }

    public string ContractSha256 { get; }

    public string ReferenceSha256 { get; }

    public IReadOnlyList<ResolvedRuntimeReference> TrustedPlatformReferences { get; }

    public IReadOnlyList<ResolvedRuntimeReference> RuntimeContractReferences { get; }

    public IReadOnlyList<ResolvedRuntimeReference> OrderedReferences { get; }

    public IReadOnlyList<string> ReferencePaths { get; }

    private static IReadOnlyList<ResolvedRuntimeReference> AsOrderedReadOnly(
        IEnumerable<ResolvedRuntimeReference> references) =>
        new ReadOnlyCollection<ResolvedRuntimeReference>(references
            .OrderBy(
                reference => reference.LogicalIdentity.ToString(),
                StringComparer.Ordinal)
            .ToArray());
}
