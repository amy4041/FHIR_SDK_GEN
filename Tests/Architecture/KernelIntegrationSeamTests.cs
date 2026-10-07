using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata.R5;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using System.Reflection;

namespace MyFhirSdk.Tests.Architecture;

// K1 feasibility checks only: these in-memory assemblies are not canonical K2/K4 assets.
public sealed class KernelIntegrationSeamTests
{
    private static readonly string[] KernelFiles =
    [
        "FhirObject", "IFhirExtensionValue", "Base", "Element", "BackboneElement",
        "BackboneType", "DataType", "PrimitiveType", "Resource", "DomainResource",
        "Extension", "Meta", "Narrative", "IPrimitiveValueAccessor"
    ];

    private static readonly Lazy<CSharpCompilation> Kernel = new(() =>
        Compile("K1.Kernel", KernelFiles.Select(name => Read($"core/{name}.cs"))));

    private static readonly Lazy<MetadataReference> KernelReference = new(() =>
    {
        using var stream = new MemoryStream();
        var result = Kernel.Value.Emit(stream);
        Assert.True(result.Success, Describe(result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    });

    private static readonly Lazy<CSharpCompilation> Sdk = new(() =>
    {
        var generatorAssembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,
            "K1Analyzers", "System.Text.RegularExpressions.Generator.dll"));
        var generatorType = generatorAssembly.GetType(
            "System.Text.RegularExpressions.Generator.RegexGenerator", throwOnError: true)!;
        var generator = (IIncrementalGenerator)Activator.CreateInstance(generatorType)!;
        CSharpGeneratorDriver.Create([generator.AsSourceGenerator()],
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp13))
            .RunGeneratorsAndUpdateCompilation(Compile("K1.Sdk", SdkSources(), KernelReference.Value),
                out var compilation, out var diagnostics);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return (CSharpCompilation)compilation;
    });

    [Fact]
    public void ApprovedKernelCompilesWithPlatformReferencesOnly()
    {
        Assert.Empty(Errors(Kernel.Value));
        Assert.Equal(14, Kernel.Value.Assembly.GlobalNamespace
            .GetNamespaceMembers().Single(n => n.Name == "MyFhirSdk")
            .GetNamespaceMembers().Single(n => n.Name == "Core")
            .GetTypeMembers().Length);
        Assert.All(Kernel.Value.ReferencedAssemblyNames,
            identity => Assert.DoesNotContain("MyFhirSdk", identity.Name));
    }

    [Theory]
    [InlineData("MyFhirSdk.Resources.Patient")]
    [InlineData("MyFhirSdk.Types.Quantity")]
    [InlineData("MyFhirSdk.Primitives.FhirString")]
    [InlineData("MyFhirSdk.ModelMetadata.R5.R5ModelMetadataProvider")]
    [InlineData("MyFhirSdk.Client.FhirClient")]
    [InlineData("MyFhirSdk.ImplementationGuides.TwCore.TwCorePackage")]
    [InlineData("MyFhirSdk.CodeGen.Generation.ModelGenerationPipeline")]
    public void KernelIsolationRejectsSdkOrCodeGenDependency(string typeName)
    {
        var coupled = Kernel.Value.AddSyntaxTrees(Parse("ForbiddenDependency.cs",
            $"namespace MyFhirSdk.Core; public class Coupled {{ public global::{typeName}? Value; }}"));
        Assert.Contains(Errors(coupled), error => error.Id is "CS0234" or "CS0246");
    }

    [Fact]
    public void GeneratedModelsRequireSdkOwnedSimpleQuantitySource()
    {
        var sources = Sources("Generated/R5/Types")
            .Concat(Sources("Generated/R5/Resources"))
            .Concat(Sources("Generated/R5/Primitives")
                .Where(tree => !tree.FilePath.EndsWith("PrimitiveRegistry.Composition.g.cs")));
        var compilation = Compile("K1.GeneratedModels", sources, KernelReference.Value);
        // 829 datatype/resource declarations + 20 wrappers; metadata/registry stay SDK-owned.
        Assert.Equal(849, compilation.SyntaxTrees.Count() - 1);
        var errors = Errors(compilation);
        Assert.NotEmpty(errors);
        Assert.All(errors, error =>
        {
            Assert.Equal("CS0246", error.Id);
            Assert.Contains("SimpleQuantity", error.GetMessage());
        });
        // Real SDK source, not a validation stub or a second metadata reference.
        Assert.Empty(Errors(compilation.AddSyntaxTrees(Read("Types/SimpleQuantity.cs"))));
    }

    [Fact]
    public void CurrentFullBatchMetadataRequiresSdkDeclarationsBeyondKernel()
    {
        var compilation = Compile("MyFhirSdk.Generated.CompilationValidation",
            Sources("Generated/R5/Types")
                .Concat(Sources("Generated/R5/Resources"))
                .Concat(Sources("Generated/R5/Primitives")
                    .Where(tree => !tree.FilePath.EndsWith("PrimitiveRegistry.Composition.g.cs")))
                .Concat(Sources("Generated/R5/ModelMetadata")), KernelReference.Value);
        var errors = Errors(compilation);
        Assert.Contains(errors, error => error.Location.SourceTree?.FilePath
            == "Generated/R5/ModelMetadata/R5ModelMetadata.g.cs"
            && error.GetMessage().Contains("ImmutableModelMetadataProvider"));
        Assert.Contains(errors, error => error.Location.SourceTree?.FilePath
            == "Generated/R5/ModelMetadata/R5ValidationRules.g.cs"
            && error.Id == "CS0234");
    }

    [Fact]
    public void RealSdkSourcesCompileAcrossKernelBoundaryWithoutProductionFriendAccess()
    {
        Assert.Empty(Errors(Sdk.Value));
        using var stream = new MemoryStream();
        var result = Sdk.Value.Emit(stream);
        Assert.True(result.Success, Describe(result.Diagnostics));
        Assert.DoesNotContain(Sdk.Value.Assembly.GetAttributes(), attribute =>
            attribute.AttributeClass?.Name == nameof(System.Runtime.CompilerServices.InternalsVisibleToAttribute));
    }

    [Fact]
    public void RealRegistryContractDriftFailsGeneratedCompositionCompilation()
    {
        var original = Sdk.Value.SyntaxTrees.Single(tree =>
            tree.FilePath == "Primitives/Runtime/PrimitiveRegistry.cs");
        var source = original.GetText().ToString();
        const string declaration = "Define<TPrimitive, TValue>(";
        Assert.Contains(declaration, source);
        var drifted = Sdk.Value.ReplaceSyntaxTree(original, Parse(original.FilePath,
            source.Replace(declaration, "DefineAfterDrift<TPrimitive, TValue>(", StringComparison.Ordinal)));
        Assert.Contains(Errors(drifted), error => error.Id == "CS0103"
            && error.Location.SourceTree?.FilePath
                == "Generated/R5/Primitives/PrimitiveRegistry.Composition.g.cs"
            && error.GetMessage().Contains("Define"));
    }

    [Fact]
    public void DefaultEnginesUseSdkOwnedR5AndPrimitiveComposition()
    {
        foreach (var engine in new object[] { new FhirJsonParser(), new FhirJsonSerializer(), new FhirValidator() })
        {
            var fields = engine.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            var registry = Assert.Single(fields, field => field.FieldType == typeof(PrimitiveRegistry));
            Assert.Same(PrimitiveRegistry.Default, registry.GetValue(engine));
            var provider = Assert.Single(fields, field => field.Name is "_metadataProvider" or "_ruleProvider");
            Assert.Same(R5ModelMetadataProvider.Default, provider.GetValue(engine));
        }
        Assert.Equal(typeof(FhirObject).Assembly, typeof(PrimitiveRegistry).Assembly);
        Assert.Equal(typeof(PrimitiveRegistry).Assembly, typeof(FhirString).Assembly);
    }

    private static IEnumerable<SyntaxTree> SdkSources() =>
        new[] { "core", "Primitives", "Generated", "Types", "ModelMetadata", "Serialization",
            "Validation", "Client", "ImplementationGuides" }
            .SelectMany(Sources)
            .Where(tree => !KernelFiles.Any(name => tree.FilePath == $"core/{name}.cs"));

    private static CSharpCompilation Compile(string name, IEnumerable<SyntaxTree> sources,
        MetadataReference? kernel = null) => CSharpCompilation.Create(name,
            sources.Append(Parse("ImplicitUsings.g.cs", """
                global using System;
                global using System.Collections.Generic;
                global using System.IO;
                global using System.Linq;
                global using System.Net.Http;
                global using System.Threading;
                global using System.Threading.Tasks;
                """)),
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths()
                // testhost's TPA list also contains application/test assemblies.
                .Where(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(typeof(object).Assembly.Location))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .Concat(kernel is null ? [] : new[] { kernel }),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable, deterministic: true));

    private static SyntaxTree Parse(string path, string source) =>
        CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp13), path);

    private static SyntaxTree Read(string path) =>
        Parse(path, File.ReadAllText(Path.Combine(RepositoryRoot(), path)));

    private static IEnumerable<SyntaxTree> Sources(string directory) =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), directory), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .Select(path => Read(Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/')));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MyFhirSdk.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate K1 test source repository.");
    }

    private static Diagnostic[] Errors(CSharpCompilation compilation) =>
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

    private static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
}
