using System.Reflection;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.Core;
using MyFhirSdk.Serialization.Json;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PhaseCModelGenerationArchitectureTests
{
    [Fact]
    public void CodeGenAssemblyDoesNotContainRemovedPreviewPipeline()
    {
        var assembly = typeof(GeneratorCli).Assembly;

        Assert.Null(assembly.GetType("MyFhirSdk.CodeGen.Generation.FhirSdkGenerator"));
        Assert.Null(assembly.GetType("MyFhirSdk.CodeGen.Generation.GeneratorOptions"));
        Assert.Null(assembly.GetType("MyFhirSdk.CodeGen.Parsing.StructureDefinitionParser"));
        Assert.Null(assembly.GetType("MyFhirSdk.CodeGen.Rendering.CSharpClassRenderer"));
    }

    [Fact]
    public void TypeMapperOnlyAcceptsValidatedMappingViews()
    {
        var method = Assert.Single(
            typeof(CSharpTypeMapper).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            candidate => candidate.Name == nameof(CSharpTypeMapper.TryMap));

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.True(parameters[1].IsOut);
    }

    [Fact]
    public void CliRejectsRemovedPreviewMode()
    {
        var result = new GeneratorCommandLineParser().Parse(
            ["--mode", "datatype-preview"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Unknown generator mode 'datatype-preview'. Expected 'primitive' or 'model'.",
            result.Error);
    }

    [Fact]
    public void DefaultGeneratedProviderRejectsUnregisteredResourceType()
    {
        var exception = Assert.Throws<FhirSdkException>(() =>
            new FhirJsonParser().Parse<UnregisteredResource>(
                "{\"resourceType\":\"UnregisteredResource\"}"));

        Assert.Contains("is not registered", exception.Message, StringComparison.Ordinal);
    }

    private sealed class UnregisteredResource : Resource
    {
        public override string ResourceType => nameof(UnregisteredResource);
    }
}
