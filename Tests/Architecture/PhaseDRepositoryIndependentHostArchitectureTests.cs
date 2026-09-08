using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PhaseDRepositoryIndependentHostArchitectureTests
{
    [Fact]
    public void ProductionAssemblyDoesNotExposeRepositoryRootLocator()
    {
        var assembly = typeof(GeneratorCli).Assembly;

        Assert.Null(assembly.GetType("MyFhirSdk.CodeGen.Cli.RepositoryRootLocator"));
    }

    [Fact]
    public void WriterRequiresExplicitOutputSafetyContext()
    {
        var constructor = Assert.Single(
            typeof(GeneratedFileWriter).GetConstructors());

        Assert.Equal(
            [typeof(OutputSafetyContext)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void CommandLineParserRequiresExplicitToolAssetResolver()
    {
        var constructor = Assert.Single(
            typeof(GeneratorCommandLineParser).GetConstructors());

        Assert.Equal(
            [typeof(ToolAssetResolver)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }
}
