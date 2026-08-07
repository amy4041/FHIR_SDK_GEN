using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.Core;

namespace MyFhirSdk.CodeGen.Compilation;

public sealed class RoslynCompilationValidator
{
    private const string ValidationAssemblyName =
        "MyFhirSdk.Generated.CompilationValidation";

    private readonly IReadOnlyList<MetadataReference> _references;

    public RoslynCompilationValidator()
        : this(CreateDefaultReferences())
    {
    }

    internal RoslynCompilationValidator(
        IReadOnlyList<MetadataReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        _references = references;
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
            _references,
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

    private static IReadOnlyList<MetadataReference> CreateDefaultReferences()
    {
        var referenceAssemblyDirectory = FindNet9ReferenceAssemblyDirectory();
        var referencePaths = Directory
            .EnumerateFiles(referenceAssemblyDirectory, "*.dll")
            .Append(typeof(DataType).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        return referencePaths
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static string FindNet9ReferenceAssemblyDirectory()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException(
                "Could not determine the .NET runtime directory.");
        var dotnetRoot = Directory.GetParent(runtimeDirectory)?
            .Parent?
            .Parent?
            .FullName;
        if (string.IsNullOrWhiteSpace(dotnetRoot))
        {
            throw new InvalidOperationException(
                "Could not determine the .NET installation directory.");
        }

        var packRoot = Path.Combine(
            dotnetRoot,
            "packs",
            "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(packRoot))
        {
            throw new InvalidOperationException(
                $".NET reference assembly pack was not found at '{packRoot}'.");
        }

        var targetFramework = typeof(RoslynCompilationValidator).Assembly
            .GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: false)
            .OfType<TargetFrameworkAttribute>()
            .SingleOrDefault()?.FrameworkName;
        if (!string.Equals(
                targetFramework,
                ".NETCoreApp,Version=v9.0",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Compilation validation requires .NET 9, but the generator " +
                $"targets '{targetFramework ?? "<unknown>"}'.");
        }

        var packVersionDirectory = Directory
            .EnumerateDirectories(packRoot, "9.0.*")
            .Select(path => new
            {
                Path = path,
                Version = ParseVersion(Path.GetFileName(path))
            })
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
        if (packVersionDirectory is null)
        {
            throw new InvalidOperationException(
                $"A .NET 9 reference assembly pack was not found at '{packRoot}'.");
        }

        var referenceAssemblyDirectory = Path.Combine(
            packVersionDirectory,
            "ref",
            "net9.0");
        if (!Directory.Exists(referenceAssemblyDirectory))
        {
            throw new InvalidOperationException(
                $".NET 9 reference assemblies were not found at " +
                $"'{referenceAssemblyDirectory}'.");
        }

        return referenceAssemblyDirectory;
    }

    private static Version? ParseVersion(string value)
    {
        return Version.TryParse(value, out var version) ? version : null;
    }
}
