using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Loading;

public sealed class LoadedDefinitionPackage
{
    public LoadedDefinitionPackage(
        DefinitionPackageIdentity identity,
        IEnumerable<LoadedStructureDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(definitions);

        Identity = identity;
        Definitions = new ReadOnlyCollection<LoadedStructureDefinition>(
            definitions.ToArray());
    }

    public DefinitionPackageIdentity Identity { get; }

    public IReadOnlyList<LoadedStructureDefinition> Definitions { get; }
}
