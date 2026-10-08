using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

// Test-only integration compilation. The tool never ships or discovers SDK sources.
internal static class RealSdkSourceCompiler
{
    private static readonly Lazy<Task<IReadOnlyList<GeneratedSource>>> Generated = new(GenerateAsync);

    internal static Task<IReadOnlyList<GeneratedSource>> GeneratedSourcesAsync() => Generated.Value;

    private static async Task<IReadOnlyList<GeneratedSource>> GenerateAsync()
    {
        string Policy(string name) => Path.Combine(AppContext.BaseDirectory, "Policy", name);
        var package = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz");
        var output = Path.Combine(AppContext.BaseDirectory, ".unused-k1-integration-output");
        var models = await CodeGenTestRuntime.CreateModelPipeline().BuildAsync(new(
            package, output, "hl7.fhir.r5.core", "5.0.0", "5.0.0",
            Policy("primitive-generation-policy.json"), Policy("r5-model-ownership-policy.json"),
            new ModelIrPolicyPaths(Policy("r5-model-naming-policy.json"), Policy("r5-backbone-policy.json"),
                Policy("r5-choice-open-type-policy.json")),
            Policy("r5-validation-capability-policy.json"), [], ModelGenerationPipeline.DefaultCodeGenVersion));
        var primitives = await CodeGenTestRuntime.CreatePrimitivePipeline().BuildAsync(new(
            package, Policy("primitive-generation-policy.json"), output, "5.0.0", "hl7.fhir.r5.core", "5.0.0",
            PrimitiveGenerationPipeline.DefaultCodeGenVersion));
        if (!models.IsSuccess || models.Value is null || !primitives.IsSuccess || primitives.Value is null)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                models.Diagnostics.Concat(primitives.Diagnostics).Select(d => d.Message)));
        return models.Value.Sources.Concat(primitives.Value.Sources.Select(source =>
            source with { FileName = "Generated/R5/Primitives/" + source.FileName })).ToArray();
    }

    internal static CSharpCompilation Create(IReadOnlyList<GeneratedSource> generated)
    {
        var root = RepositoryRoot();
        var kernelFiles = typeof(MyFhirSdk.Core.FhirObject).Assembly.GetExportedTypes()
            .Select(type => "core/" + type.Name.Replace("`1", "", StringComparison.Ordinal) + ".cs")
            .ToHashSet(StringComparer.Ordinal);
        var handwritten = new[] { "core", "Types", "Primitives", "ModelMetadata", "Serialization",
                "Validation", "Client", "ImplementationGuides" }
            .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Select(path => new GeneratedSource(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)));
        handwritten = handwritten.Where(source => !kernelFiles.Contains(source.FileName));
        var sources = handwritten.Concat(generated).Append(new("ImplicitUsings.g.cs", """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """)).OrderBy(source => source.FileName, StringComparer.Ordinal).ToArray();
        if (sources.Select(source => source.FileName).Distinct(StringComparer.Ordinal).Count() != sources.Length)
            throw new ArgumentException("SDK integration compile items must have one owner per source.");
        var parse = new CSharpParseOptions(LanguageVersion.CSharp13);
        // Only actual framework assemblies; testhost's application TPA cannot fill missing SDK types.
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var compilation = CSharpCompilation.Create($"MyFhirSdk.Generated.SdkIntegration.{Guid.NewGuid():N}",
            sources.Select(source => CSharpSyntaxTree.ParseText(source.Source, parse, source.FileName)),
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths()
                .Where(path => Path.GetDirectoryName(path) == frameworkDirectory)
                .Append(typeof(MyFhirSdk.Core.FhirObject).Assembly.Location)
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable, deterministic: true));
        var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "K1Analyzers",
            "System.Text.RegularExpressions.Generator.dll"));
        var type = assembly.GetType("System.Text.RegularExpressions.Generator.RegexGenerator", throwOnError: true)!;
        var generator = (IIncrementalGenerator)Activator.CreateInstance(type)!;
        CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
        return (CSharpCompilation)output;
    }

    internal static Assembly Compile(IReadOnlyList<GeneratedSource> generated)
    {
        var compilation = Create(generated);
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine,
            result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MyFhirSdk.sln"))) return directory.FullName;
        throw new InvalidOperationException("SDK integration tests require their source checkout.");
    }
}
