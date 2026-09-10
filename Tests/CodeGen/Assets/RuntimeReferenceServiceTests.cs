using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Compatibility;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Diagnostics;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Assets;

public sealed class RuntimeReferenceServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "MyFhirSdk-RuntimeReferenceTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_ValidPackageAssetCreatesDeterministicCompilationSet()
    {
        var result = ResolvePackageOwned();

        Assert.True(result.IsSuccess, Describe(result.Diagnostics));
        var references = Assert.IsType<RuntimeReferenceSet>(result.Value);
        Assert.Equal(GenerationCompatibilityMatrix.TargetFramework, references.TargetFramework);
        Assert.Equal(
            $"MyFhirSdk, Version=1.0.0.0, PublicKeyToken=null, TargetFramework={GenerationCompatibilityMatrix.TargetFramework}",
            references.LogicalAssemblyIdentity.ToString());
        Assert.Equal(
            CodeGenTestRuntime.RuntimeContract.DescriptorSha256,
            references.ContractSha256);
        Assert.Equal(
            CodeGenTestRuntime.RuntimeContract.CompilerReference.Sha256,
            references.ReferenceSha256);
        Assert.Single(references.RuntimeContractReferences);
        AssertReferenceAssembly(references.RuntimeContractReferences[0].Path);
        Assert.NotEmpty(references.TrustedPlatformReferences);
        Assert.Equal(
            references.OrderedReferences
                .OrderBy(
                    reference => reference.LogicalIdentity.ToString(),
                    StringComparer.Ordinal),
            references.OrderedReferences);
        Assert.DoesNotContain(references.TrustedPlatformReferences, reference =>
            reference.Assembly.Name == "MyFhirSdk");

        var compilation = new RoslynCompilationValidator(references).Validate([
            new GeneratedSource(
                "RuntimeConsumer.g.cs",
                "using MyFhirSdk.Core; public sealed class RuntimeConsumer : DataType { }")
        ]);
        Assert.True(compilation.IsSuccess, Describe(compilation.Diagnostics));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResolvedRuntimeReference>)references.OrderedReferences)
                .Add(references.OrderedReferences[0]));
    }

    [Fact]
    public void Resolve_MissingPackageAssetReturnsStableLogicalDiagnostic()
    {
        var result = ResolveFromToolRoot(_directory);

        AssertFailure(result, GeneratorDiagnosticCodes.RuntimeReferenceMissing);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_CorruptPackageAssetReturnsStableLogicalDiagnostic()
    {
        var path = CreatePackageAssetPath();
        File.WriteAllBytes(path, [0x46, 0x48, 0x49, 0x52]);

        var result = ResolveFromToolRoot(_directory);

        AssertFailure(result, GeneratorDiagnosticCodes.RuntimeReferenceReadFailure);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_WrongAssemblyVersionReturnsStableDiagnostic()
    {
        var path = Path.Combine(CreateDirectory(), "MyFhirSdk.dll");
        EmitRuntimeReference(path, ".NETCoreApp,Version=v9.0", "2.0.0.0");

        var result = ResolveExplicit(path);

        AssertFailure(result, GeneratorDiagnosticCodes.RuntimeReferenceIdentityMismatch);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_WrongHashReturnsStableDiagnostic()
    {
        var path = Path.Combine(CreateDirectory(), "MyFhirSdk.dll");
        File.Copy(GetPackageAssetPath(), path);
        using (var stream = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.None))
        {
            stream.WriteByte(0);
        }

        var result = ResolveExplicit(path);

        AssertFailure(result, GeneratorDiagnosticCodes.RuntimeReferenceHashMismatch);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_WrongTargetFrameworkReturnsStableDiagnostic()
    {
        var path = Path.Combine(CreateDirectory(), "MyFhirSdk.dll");
        EmitRuntimeReference(path, ".NETCoreApp,Version=v8.0");

        var result = ResolveExplicit(path);

        AssertFailure(
            result,
            GeneratorDiagnosticCodes.RuntimeReferenceTargetFrameworkMismatch);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_DuplicateRuntimeIdentityReturnsStableDiagnostic()
    {
        var first = Path.Combine(CreateDirectory(), "first.dll");
        var second = Path.Combine(_directory, "second.dll");
        File.Copy(GetPackageAssetPath(), first);
        File.Copy(GetPackageAssetPath(), second);

        var result = new RuntimeReferenceService().Resolve(
            CodeGenTestRuntime.RuntimeContract,
            [second, first],
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());

        AssertFailure(
            result,
            GeneratorDiagnosticCodes.DuplicateRuntimeReferenceIdentity);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_MissingTrustedPlatformAssemblyReturnsMissingDiagnostic()
    {
        var result = new RuntimeReferenceService().Resolve(
            CodeGenTestRuntime.RuntimeContract,
            [GetPackageAssetPath()],
            [Path.Combine(_directory, "missing-platform-assembly.dll")]);

        AssertFailure(result, GeneratorDiagnosticCodes.RuntimeReferenceMissing);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_DuplicateTrustedPlatformIdentityReturnsStableDiagnostic()
    {
        var first = Path.Combine(CreateDirectory(), "first-platform.dll");
        var second = Path.Combine(_directory, "second-platform.dll");
        File.Copy(typeof(object).Assembly.Location, first);
        File.Copy(typeof(object).Assembly.Location, second);

        var result = new RuntimeReferenceService().Resolve(
            CodeGenTestRuntime.RuntimeContract,
            [GetPackageAssetPath()],
            [second, first]);

        AssertFailure(
            result,
            GeneratorDiagnosticCodes.DuplicateRuntimeReferenceIdentity);
        AssertNoPhysicalPath(result.Diagnostics);
    }

    [Fact]
    public void Resolve_ShuffledTrustedPlatformInputProducesSameOrderedReferences()
    {
        var trustedPlatformPaths = RuntimeReferenceService.GetTrustedPlatformAssemblyPaths();
        var service = new RuntimeReferenceService();
        var first = service.Resolve(
            CodeGenTestRuntime.RuntimeContract,
            [GetPackageAssetPath()],
            trustedPlatformPaths);
        var second = service.Resolve(
            CodeGenTestRuntime.RuntimeContract,
            [GetPackageAssetPath()],
            trustedPlatformPaths.Reverse());

        Assert.True(first.IsSuccess, Describe(first.Diagnostics));
        Assert.True(second.IsSuccess, Describe(second.Diagnostics));
        Assert.Equal(
            first.Value!.OrderedReferences.Select(ReferenceSnapshot),
            second.Value!.OrderedReferences.Select(ReferenceSnapshot));
        var source = new GeneratedSource(
            "OrderInvariant.g.cs",
            "using MyFhirSdk.Core; public sealed class OrderInvariant : DataType { }");
        var firstCompilation = new RoslynCompilationValidator(first.Value).Validate([source]);
        var secondCompilation = new RoslynCompilationValidator(second.Value).Validate([source]);
        Assert.Equal(
            CompilationSnapshot(firstCompilation),
            CompilationSnapshot(secondCompilation));
    }

    [Fact]
    public void Validate_AfterRuntimeFileChanges_UsesPreviouslyValidatedImage()
    {
        var path = Path.Combine(CreateDirectory(), "MyFhirSdk.dll");
        File.Copy(GetPackageAssetPath(), path);
        var resolved = ResolveExplicit(path);
        Assert.True(resolved.IsSuccess, Describe(resolved.Diagnostics));

        File.WriteAllBytes(path, [0x46, 0x48, 0x49, 0x52]);
        var compilation = new RoslynCompilationValidator(resolved.Value!).Validate([
            new GeneratedSource(
                "ValidatedImage.g.cs",
                "using MyFhirSdk.Core; public sealed class ValidatedImage : DataType { }")
        ]);

        Assert.True(compilation.IsSuccess, Describe(compilation.Diagnostics));
    }

    private static GenerationResult<RuntimeReferenceSet?> ResolvePackageOwned() =>
        ResolveFromToolRoot(AppContext.BaseDirectory);

    private static GenerationResult<RuntimeReferenceSet?> ResolveFromToolRoot(
        string toolRoot)
    {
        var paths = new ToolAssetResolver(toolRoot).ResolveRuntimeReferencePaths(
            CodeGenTestRuntime.RuntimeContract,
            new ToolAssetOverrides());
        return new RuntimeReferenceService().Resolve(
            CodeGenTestRuntime.RuntimeContract,
            paths,
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());
    }

    private static GenerationResult<RuntimeReferenceSet?> ResolveExplicit(string path) =>
        new RuntimeReferenceService().Resolve(
            CodeGenTestRuntime.RuntimeContract,
            [path],
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());

    private string CreatePackageAssetPath()
    {
        var path = Path.Combine(
            _directory,
            "Assets",
            "RuntimeReferences",
            CodeGenTestRuntime.RuntimeContract.CompilerReference.TargetFramework,
            "MyFhirSdk.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private string CreateDirectory()
    {
        Directory.CreateDirectory(_directory);
        return _directory;
    }

    private static string GetPackageAssetPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "RuntimeReferences",
        CodeGenTestRuntime.RuntimeContract.CompilerReference.TargetFramework,
        "MyFhirSdk.dll");

    private static void EmitRuntimeReference(
        string path,
        string targetFramework,
        string assemblyVersion = "1.0.0.0")
    {
        var source = $$"""
            using System.Reflection;
            using System.Runtime.Versioning;
            [assembly: AssemblyVersion("{{assemblyVersion}}")]
            [assembly: TargetFramework("{{targetFramework}}")]
            namespace MyFhirSdk.ReferenceFixture;
            public sealed class Marker { }
            """;
        var compilation = CSharpCompilation.Create(
            "MyFhirSdk",
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths()
                .Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
    }

    private static string ReferenceSnapshot(ResolvedRuntimeReference reference) =>
        $"{reference.Kind}|{reference.LogicalIdentity}";

    private static void AssertReferenceAssembly(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var definition = metadata.GetAssemblyDefinition();
        Assert.Contains(definition.GetCustomAttributes(), handle =>
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                return false;
            }

            var constructor = metadata.GetMemberReference(
                (MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference)
            {
                return false;
            }

            var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            return metadata.GetString(type.Namespace) == "System.Runtime.CompilerServices" &&
                metadata.GetString(type.Name) == "ReferenceAssemblyAttribute";
        });
    }

    private static string[] CompilationSnapshot(
        GenerationResult<IReadOnlyList<GeneratedSource>> result) =>
    [
        $"success|{result.IsSuccess}",
        .. result.Diagnostics.Select(diagnostic =>
            $"{diagnostic.Code}|{diagnostic.SourceFile}|{diagnostic.Message}")
    ];

    private void AssertNoPhysicalPath(IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        var physicalPaths = new[]
        {
            Path.GetFullPath(_directory),
            Path.GetFullPath(AppContext.BaseDirectory),
            Path.GetFullPath(Directory.GetCurrentDirectory())
        }.Distinct(StringComparer.OrdinalIgnoreCase);
        Assert.All(diagnostics, diagnostic =>
        {
            foreach (var physicalPath in physicalPaths)
            {
                Assert.DoesNotContain(
                    physicalPath,
                    diagnostic.Message,
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    physicalPath,
                    diagnostic.SourceFile,
                    StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    private static void AssertFailure(
        GenerationResult<RuntimeReferenceSet?> result,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private static string Describe(IEnumerable<GeneratorDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"[{diagnostic.Code}] {diagnostic.Message}"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
