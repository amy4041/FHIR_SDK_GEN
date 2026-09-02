using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelIrBatch
{
    internal ModelIrBatch(IEnumerable<ModelDeclarationIr> declarations)
    {
        Declarations = new ReadOnlyCollection<ModelDeclarationIr>(declarations.ToArray());
    }

    public IReadOnlyList<ModelDeclarationIr> Declarations { get; }
}
