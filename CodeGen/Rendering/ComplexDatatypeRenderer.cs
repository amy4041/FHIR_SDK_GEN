using MyFhirSdk.CodeGen.Ir;

namespace MyFhirSdk.CodeGen.Rendering;

public sealed class ComplexDatatypeRenderer
{
    private readonly ModelDeclarationSourceRenderer _renderer = new();

    public string Render(ModelDeclarationIr declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.Category is not (
            ModelIrCategory.ComplexDatatype or
            ModelIrCategory.ComplexDatatypeComponent))
        {
            throw new ArgumentException(
                $"Complex datatype renderer cannot render category '{declaration.Category}'.",
                nameof(declaration));
        }

        return _renderer.Render(
            declaration,
            $"FHIR R5 {declaration.FhirName} datatype.");
    }
}
