using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class ResourceBackboneGenerationPipeline
{
    private readonly ComplexDatatypeRenderer _datatypeRenderer;
    private readonly ResourceBackboneRenderer _resourceRenderer;
    private readonly RoslynCompilationValidator _compilationValidator;

    public ResourceBackboneGenerationPipeline()
        : this(
            new ComplexDatatypeRenderer(),
            new ResourceBackboneRenderer(),
            new RoslynCompilationValidator())
    {
    }

    public ResourceBackboneGenerationPipeline(
        ComplexDatatypeRenderer datatypeRenderer,
        ResourceBackboneRenderer resourceRenderer,
        RoslynCompilationValidator compilationValidator)
    {
        ArgumentNullException.ThrowIfNull(datatypeRenderer);
        ArgumentNullException.ThrowIfNull(resourceRenderer);
        ArgumentNullException.ThrowIfNull(compilationValidator);
        _datatypeRenderer = datatypeRenderer;
        _resourceRenderer = resourceRenderer;
        _compilationValidator = compilationValidator;
    }

    public GenerationResult<ResourceBackboneGenerationBatch?> Generate(ModelIrBatch irBatch)
    {
        ArgumentNullException.ThrowIfNull(irBatch);
        var declarations = irBatch.Declarations
            .Where(IsRenderableCategory)
            .OrderBy(declaration => declaration.ArtifactPath, StringComparer.Ordinal)
            .ToArray();
        if (!declarations.Any(declaration => declaration.Category == ModelIrCategory.Resource))
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                GeneratorDiagnosticSeverity.Error,
                "Resource/backbone generation requires at least one resource declaration.",
                "<resource-backbone-batch>")]);
        }

        var diagnostics = ValidateBatch(declarations);
        if (diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        GeneratedSource[] sources;
        try
        {
            sources = declarations
                .Select(declaration => new GeneratedSource(
                    declaration.ArtifactPath,
                    Render(declaration)))
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                GeneratorDiagnosticSeverity.Error,
                exception.Message,
                "<resource-backbone-renderer>")]);
        }

        var compilationResult = _compilationValidator.Validate(sources);
        if (!compilationResult.IsSuccess)
        {
            return Failure(compilationResult.Diagnostics);
        }

        return new GenerationResult<ResourceBackboneGenerationBatch?>(
            new ResourceBackboneGenerationBatch(sources),
            Array.Empty<GeneratorDiagnostic>());
    }

    private string Render(ModelDeclarationIr declaration) =>
        declaration.Category is ModelIrCategory.Resource or ModelIrCategory.Backbone
            ? _resourceRenderer.Render(declaration)
            : _datatypeRenderer.Render(declaration);

    private static bool IsRenderableCategory(ModelDeclarationIr declaration) =>
        declaration.Category is
            ModelIrCategory.ComplexDatatype or
            ModelIrCategory.ComplexDatatypeComponent or
            ModelIrCategory.Resource or
            ModelIrCategory.Backbone;

    private static IReadOnlyList<GeneratorDiagnostic> ValidateBatch(
        IReadOnlyList<ModelDeclarationIr> declarations)
    {
        var declarationTypes = declarations
            .Select(declaration => declaration.FullyQualifiedName)
            .ToHashSet(StringComparer.Ordinal);
        var resourceCanonicals = declarations
            .Where(declaration => declaration.Category == ModelIrCategory.Resource)
            .Select(declaration => declaration.Source.DefinitionCanonical)
            .ToHashSet(StringComparer.Ordinal);
        var diagnostics = new List<GeneratorDiagnostic>();

        foreach (var declaration in declarations)
        {
            if (declaration.Category == ModelIrCategory.Backbone &&
                (string.IsNullOrWhiteSpace(declaration.ResourceOwnerCanonical) ||
                 !resourceCanonicals.Contains(declaration.ResourceOwnerCanonical)))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.MissingDependency,
                    GeneratorDiagnosticSeverity.Error,
                    $"Backbone declaration does not have its resource owner '{declaration.ResourceOwnerCanonical}' in the generation batch.",
                    declaration.Source.SourceIdentity,
                    declaration.Source.DefinitionCanonical,
                    declaration.Source.DefinitionVersion,
                    declaration.BackboneElementId,
                    null));
            }

            ValidateReference(declaration, null, declaration.BaseType);
            foreach (var member in declaration.Members)
            {
                foreach (var alternative in member.TypeAlternatives)
                {
                    ValidateReference(declaration, member, alternative);
                }
            }
        }

        return diagnostics
            .OrderBy(diagnostic => diagnostic.DefinitionCanonical, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.ElementId, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();

        void ValidateReference(
            ModelDeclarationIr declaration,
            ModelMemberIr? member,
            ModelTypeReferenceIr reference)
        {
            if (reference.IsExternal || reference.IsPrimitive ||
                declarationTypes.Contains(reference.ClrType!))
            {
                return;
            }
            diagnostics.Add(new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.MissingDependency,
                GeneratorDiagnosticSeverity.Error,
                $"Resource/backbone batch does not contain generated dependency '{reference.TargetCanonical}'.",
                declaration.Source.SourceIdentity,
                declaration.Source.DefinitionCanonical,
                declaration.Source.DefinitionVersion,
                member?.Source.ElementId,
                member?.Source.ElementPath));
        }
    }

    private static GenerationResult<ResourceBackboneGenerationBatch?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
}
