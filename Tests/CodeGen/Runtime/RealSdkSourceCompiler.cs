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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> ModelImages = new();

    internal static CSharpCompilation CreateModels(IReadOnlyList<GeneratedSource> generated, string? name = null)
    {
        var handwritten = ReadSources("Models/R5");
        return Build(name ?? $"MyFhirSdk.R5.Models.Integration.{Guid.NewGuid():N}",
            handwritten.Concat(generated.Where(s => s.FileName.StartsWith("Generated/R5/", StringComparison.Ordinal))), References());
    }

    internal static CSharpCompilation CreateR4Models(IReadOnlyList<GeneratedSource> generated)
    {
        var handwritten = new[] { "PrimitiveComposition.cs", "MetadataComposition.cs", "ValidationComposition.cs" }
            .Select(name => ReadSource("Models/R5/" + name))
            .Select(source => source with { Source = source.Source.Replace("namespace MyFhirSdk.Primitives;", "namespace MyFhirSdk.R4.Primitives;") });
        var provider = new GeneratedSource("V4FixtureProvider.cs", """
            using MyFhirSdk.Core;
            using MyFhirSdk.ModelContracts;
            using MyFhirSdk.ModelMetadata.R4;
            public static class V4FixtureProvider
            {
                private static readonly ModelIdentity Identity = new("4.0.1", "hl7.fhir.r4.core", "4.0.1", "r4-policy-v1", "v4-compile-fixture");
                public static PrimitiveProviderDefinition Primitives { get; } = new(Identity, MyFhirSdk.R4.Primitives.PrimitiveRegistry.Definitions);
                public static ValidationProviderDefinition Validation { get; } = new(Identity, new(false, true, true, false, false), GeneratedR4ValidationRules.Create().Definitions);
                public static ModelMetadataDefinition Metadata { get; } = GeneratedR4ModelMetadata.Create().Export(Identity,
                    typeof(V4FixtureProvider).Assembly.GetExportedTypes().Where(t => typeof(FhirObject).IsAssignableFrom(t)));
            }
            """);
        return Build($"MyFhirSdk.R4.Models.V4Compile.{Guid.NewGuid():N}", handwritten.Concat(generated)
            .Append(ReadSource("CodeGen/Assets/CompilerSources/R4/SimpleQuantity.cs")).Append(provider), References());
    }

    internal static CSharpCompilation Create(IReadOnlyList<GeneratedSource> generated)
    {
        var models = CreateModels(generated);
        using var stream = new MemoryStream();
        var emit = models.Emit(stream);
        if (!emit.Success) throw new InvalidOperationException(string.Join(Environment.NewLine,
            emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        var image = stream.ToArray();
        var name = $"MyFhirSdk.Generated.SdkIntegration.{Guid.NewGuid():N}";
        ModelImages[name] = image;
        var kernelFiles = typeof(MyFhirSdk.Core.FhirObject).Assembly.GetExportedTypes()
            .Select(t => "core/" + t.Name.Replace("`1", "", StringComparison.Ordinal) + ".cs").ToHashSet(StringComparer.Ordinal);
        var handwritten = new[] { "core", "Primitives", "ModelMetadata", "Serialization", "Validation", "Client", "ImplementationGuides" }
            .SelectMany(ReadSources).Where(source => !kernelFiles.Contains(source.FileName));
        return Build(name, handwritten.Concat(generated.Where(s => !s.FileName.StartsWith("Generated/R5/", StringComparison.Ordinal))),
            References().Append(MetadataReference.CreateFromImage(image)));
    }

    private static IEnumerable<MetadataReference> References()
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return RuntimeReferenceService.GetTrustedPlatformAssemblyPaths()
            .Where(path => Path.GetDirectoryName(path) == frameworkDirectory)
            .Concat(new[] { typeof(MyFhirSdk.Core.FhirObject).Assembly.Location,
                typeof(MyFhirSdk.ModelContracts.ModelIdentity).Assembly.Location,
                typeof(MyFhirSdk.ModelSupport.ModelRules).Assembly.Location })
            .Select(path => MetadataReference.CreateFromFile(path));
    }

    private static GeneratedSource ReadSource(string name) => new(name,
        File.ReadAllText(Path.Combine(RepositoryRoot(), name)));

    private static IEnumerable<GeneratedSource> ReadSources(string directory) =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), directory), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Select(path => ReadSource(Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/')));

    private static CSharpCompilation Build(string name, IEnumerable<GeneratedSource> source,
        IEnumerable<MetadataReference> references)
    {
        var sources = source.Append(new("ImplicitUsings.g.cs", """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """));
        var parse = new CSharpParseOptions(LanguageVersion.CSharp13);
        var compilation = CSharpCompilation.Create(name,
            sources.Select(s => CSharpSyntaxTree.ParseText(s.Source, parse, s.FileName)), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable, deterministic: true));
        // Shared primitive regexes now compile in ModelSupport, not in SDK sources.
        return compilation;
    }

    internal static Assembly Compile(IReadOnlyList<GeneratedSource> generated)
    {
        var compilation = Create(generated);
        using var models = new MemoryStream(ModelImages[compilation.AssemblyName!]);
        AssemblyLoadContext.Default.LoadFromStream(models);
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
