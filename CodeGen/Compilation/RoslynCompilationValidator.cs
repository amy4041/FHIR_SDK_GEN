using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Compilation;

public sealed class RoslynCompilationValidator
{
    private const string ValidationAssemblyName =
        "MyFhirSdk.Generated.CompilationValidation";

    private readonly RuntimeReferenceSet _referenceSet;

    public RoslynCompilationValidator(RuntimeReferenceSet referenceSet)
    {
        ArgumentNullException.ThrowIfNull(referenceSet);
        _referenceSet = referenceSet;
    }

    public GenerationResult<IReadOnlyList<GeneratedSource>> Validate(
        IReadOnlyList<GeneratedSource> generatedSources)
    {
        ArgumentNullException.ThrowIfNull(generatedSources);

        var sources = generatedSources.ToArray();
        ValidateSources(sources);

        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp13);
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(
                source.Source,
                parseOptions,
                path: source.FileName))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            ValidationAssemblyName,
            syntaxTrees,
            _referenceSet.ReferencePaths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));

        var diagnostics = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(ToGeneratorDiagnostic)
            .ToArray();

        return new GenerationResult<IReadOnlyList<GeneratedSource>>(
            sources,
            diagnostics);
    }

    private static void ValidateSources(IEnumerable<GeneratedSource> sources)
    {
        var fileNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (string.IsNullOrWhiteSpace(source.FileName))
            {
                throw new ArgumentException(
                    "Every generated source must have a file name.",
                    nameof(sources));
            }

            if (!fileNames.Add(source.FileName))
            {
                throw new ArgumentException(
                    $"Generated source file names must be unique: " +
                    $"'{source.FileName}'.",
                    nameof(sources));
            }

            if (source.Source is null)
            {
                throw new ArgumentException(
                    $"Generated source '{source.FileName}' has no content.",
                    nameof(sources));
            }
        }
    }

    private static GeneratorDiagnostic ToGeneratorDiagnostic(
        Diagnostic diagnostic)
    {
        var sourceFile = diagnostic.Location.SourceTree?.FilePath;
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            sourceFile = "<generated-source-batch>";
        }

        var lineSuffix = "";
        if (diagnostic.Location.IsInSource)
        {
            var position = diagnostic.Location.GetLineSpan().StartLinePosition;
            lineSuffix = $" (line {position.Line + 1}, column {position.Character + 1})";
        }

        return new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.CompilationFailure,
            GeneratorDiagnosticSeverity.Error,
            $"{diagnostic.Id}: " +
            $"{diagnostic.GetMessage(CultureInfo.InvariantCulture)}" +
            lineSuffix,
            sourceFile);
    }

}
