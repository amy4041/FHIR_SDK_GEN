using MyFhirSdk.CodeGen.Ir;

namespace MyFhirSdk.CodeGen.Rendering;

public sealed class ResourceBackboneRenderer
{
    private readonly ModelDeclarationSourceRenderer _renderer = new();

    public string Render(ModelDeclarationIr declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.Category is not (ModelIrCategory.Resource or ModelIrCategory.Backbone))
        {
            throw new ArgumentException(
                $"Resource/backbone renderer cannot render category '{declaration.Category}'.",
                nameof(declaration));
        }

        var isConcreteResource = declaration.Category == ModelIrCategory.Resource &&
            !declaration.IsAbstract;
        var kind = declaration.Category == ModelIrCategory.Resource
            ? "resource"
            : $"{declaration.ResourceOwnerCanonical} backbone element";

        return _renderer.Render(
            declaration,
            $"FHIR R5 {declaration.FhirName} {kind}.",
            isConcreteResource ? declaration.FhirName : null);
    }
}
