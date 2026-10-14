using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.Core;
using System.Runtime.CompilerServices;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

public sealed class KernelSdkIntegrationTests
{
    private static readonly Lazy<Task<CSharpCompilation>> Sdk = new(async () =>
        RealSdkSourceCompiler.Create(await RealSdkSourceCompiler.GeneratedSourcesAsync()));

    [Fact]
    public void DeployedSdkDoesNotGrantProductionGeneratorFriendAccess()
    {
        var friends = typeof(MyFhirSdk.Primitives.FhirString).Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
            .Cast<InternalsVisibleToAttribute>().Select(attribute => attribute.AssemblyName).ToArray();
        Assert.Equal(["MyFhirSdk.Architecture.Tests"], friends);
    }

    [Fact]
    public void PackagedAuxiliarySourceIsTheRealSdkSourceWithPinnedHash()
    {
        var source = ModelCompilerSourceAsset.Read();
        var real = File.ReadAllText(Path.Combine(RealSdkSourceCompiler.RepositoryRoot(), "Types", "SimpleQuantity.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        Assert.Equal(real, source.Source);
        Assert.Equal("simple-quantity-source-v1", ModelCompilerSourceAsset.Version);
    }

    [Fact]
    public void AuxiliarySourceRejectsDriftAndNormalizesPlatformNewlines()
    {
        var source = ModelCompilerSourceAsset.Read();
        Assert.Equal(source, ModelCompilerSourceAsset.ValidateContent(source.Source.Replace("\n", "\r\n")));
        Assert.Throws<InvalidOperationException>(() => ModelCompilerSourceAsset.ValidateContent(
            source.Source.Replace("SimpleQuantity : Quantity", "SimpleQuantity : object")));
    }

    [Fact]
    public async Task FreshFullGeneratedOutputCompilesWithRealSdkImplementations()
    {
        Assert.Equal(852, (await RealSdkSourceCompiler.GeneratedSourcesAsync()).Count);
        var compilation = await Sdk.Value;
        Assert.Empty(Errors(compilation));
        Assert.Equal(["MyFhirSdk.ModelContracts", "MyFhirSdk.Runtime"], compilation.ReferencedAssemblyNames
            .Where(identity => identity.Name.StartsWith("MyFhirSdk")).Select(identity => identity.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ActualModelValidatorCompilesFreshModelsWithKernelAndRealAuxiliarySourcesOnly()
    {
        var references = CodeGenTestRuntime.RuntimeReferences;
        Assert.Equal("MyFhirSdk.Runtime", Assert.Single(references.RuntimeContractReferences).Assembly.Name);
        var generated = await RealSdkSourceCompiler.GeneratedSourcesAsync();
        var models = generated.Where(source => source.FileName.StartsWith("Generated/R5/Types/", StringComparison.Ordinal)
            || source.FileName.StartsWith("Generated/R5/Resources/", StringComparison.Ordinal)).ToArray();
        var wrappers = generated.Where(source => source.FileName.StartsWith("Generated/R5/Primitives/", StringComparison.Ordinal)
            && !source.FileName.EndsWith("PrimitiveRegistry.Composition.g.cs", StringComparison.Ordinal)).ToArray();
        Assert.Equal(829, models.Length);
        Assert.Equal(20, wrappers.Length);
        var validator = new RoslynCompilationValidator(references);
        Assert.False(validator.WithAdditionalSources(wrappers).Validate(models).IsSuccess);
        var result = validator.WithAdditionalSources(wrappers.Append(ModelCompilerSourceAsset.Read()).ToArray()).Validate(models);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(models, result.Value);
    }

    [Theory]
    [InlineData("ModelMetadata/ImmutableModelMetadataProvider.cs", "ImmutableModelMetadataProvider", "MetadataProviderAfterDrift", "Generated/R5/ModelMetadata/R5ModelMetadata.g.cs")]
    [InlineData("Validation/Rules/RequiredFieldRule.cs", " For(", " ForAfterDrift(", "Generated/R5/ModelMetadata/R5ValidationRules.g.cs")]
    [InlineData("Primitives/Runtime/PrimitiveRegistry.cs", "Define<TPrimitive, TValue>(", "DefineAfterDrift<TPrimitive, TValue>(", "Generated/R5/Primitives/PrimitiveRegistry.Composition.g.cs")]
    public async Task RealSdkContractDriftFailsAtFreshGeneratedComposition(
        string path, string original, string replacement, string generatedPath)
    {
        var compilation = await Sdk.Value;
        var tree = compilation.SyntaxTrees.Single(tree => tree.FilePath == path);
        var source = tree.GetText().ToString();
        Assert.Contains(original, source);
        var mutated = CSharpSyntaxTree.ParseText(source.Replace(original, replacement, StringComparison.Ordinal),
            (CSharpParseOptions)tree.Options, path);
        Assert.Contains(Errors(compilation.ReplaceSyntaxTree(tree, mutated)),
            error => error.Location.SourceTree?.FilePath == generatedPath);
    }

    [Fact]
    public async Task MissingFreshCompositionIsNotFilledByCommittedGeneratedSources()
    {
        var compilation = await Sdk.Value;
        var tree = compilation.SyntaxTrees.Single(tree => tree.FilePath == "Generated/R5/ModelMetadata/R5ModelMetadata.g.cs");
        Assert.Contains(Errors(compilation.RemoveSyntaxTrees(tree)), error =>
            error.GetMessage().Contains("GeneratedR5ModelMetadata"));
    }

    private static Diagnostic[] Errors(CSharpCompilation compilation) =>
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
}
