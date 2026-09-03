using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class ComplexDatatypeGenerationPipeline
{
    private readonly ComplexDatatypeRenderer _renderer;
    private readonly RoslynCompilationValidator _compilationValidator;

    public ComplexDatatypeGenerationPipeline()
        : this(new ComplexDatatypeRenderer(), new RoslynCompilationValidator())
    {
    }

    public ComplexDatatypeGenerationPipeline(
        ComplexDatatypeRenderer renderer,
        RoslynCompilationValidator compilationValidator)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(compilationValidator);
        _renderer = renderer;
        _compilationValidator = compilationValidator;
    }

    public GenerationResult<ComplexDatatypeGenerationBatch?> Generate(ModelIrBatch irBatch)
    {
        ArgumentNullException.ThrowIfNull(irBatch);
        var declarations = irBatch.Declarations
            .Where(declaration => declaration.Category is
                ModelIrCategory.ComplexDatatype or
                ModelIrCategory.ComplexDatatypeComponent)
            .OrderBy(declaration => declaration.ArtifactPath, StringComparer.Ordinal)
            .ToArray();
        if (declarations.Length == 0)
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidModelIr,
                GeneratorDiagnosticSeverity.Error,
                "Complex datatype generation requires at least one datatype declaration.",
                "<complex-datatype-batch>")]);
        }
        var diagnostics = ValidateBatchDependencies(declarations);
        if (diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        GeneratedSource[] sources;
        try
        {
            sources = declarations.Select(declaration => new GeneratedSource(
                    declaration.ArtifactPath,
                    _renderer.Render(declaration)))
                .ToArray();
        }
        catch (ArgumentException exception)
        {
            return Failure([new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedModelShape,
                GeneratorDiagnosticSeverity.Error,
                exception.Message,
                "<complex-datatype-renderer>")]);
        }

        var compilationResult = _compilationValidator.Validate(sources);
        if (!compilationResult.IsSuccess)
        {
            return Failure(compilationResult.Diagnostics);
        }
        return new GenerationResult<ComplexDatatypeGenerationBatch?>(
            new ComplexDatatypeGenerationBatch(sources),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static IReadOnlyList<GeneratorDiagnostic> ValidateBatchDependencies(
        IReadOnlyList<ModelDeclarationIr> declarations)
    {
        var declarationTypes = declarations
            .Select(declaration => declaration.FullyQualifiedName)
            .ToHashSet(StringComparer.Ordinal);
        var diagnostics = new List<GeneratorDiagnostic>();
        foreach (var declaration in declarations)
        {
            ValidateReference(declaration, null, declaration.BaseType);
            foreach (var member in declaration.Members)
            {
                foreach (var property in member.Properties)
                {
                    if (property.Type is not null)
                    {
                        ValidateReference(declaration, member, property.Type);
                    }
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
                $"Complex datatype batch does not contain generated dependency '{reference.TargetCanonical}'.",
                declaration.Source.SourceIdentity,
                declaration.Source.DefinitionCanonical,
                declaration.Source.DefinitionVersion,
                member?.Source.ElementId,
                member?.Source.ElementPath));
        }
    }

    private static GenerationResult<ComplexDatatypeGenerationBatch?> Failure(
        IEnumerable<GeneratorDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
}
