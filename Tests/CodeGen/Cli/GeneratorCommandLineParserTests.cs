using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Cli;

public sealed class GeneratorCommandLineParserTests
{
    private readonly GeneratorCommandLineParser _parser = new();

    [Fact]
    public void Parse_ValidArguments_ReturnsSortedOptions()
    {
        var result = _parser.Parse(
        [
            "--input", "definitions",
            "--output", "generated",
            "--namespace", "MyFhirSdk.Generated.Types",
            "--fhir-version", "5.0.0",
            "--type", "Period",
            "--type", "Address"
        ]);

        Assert.True(result.IsSuccess);
        Assert.False(result.ShowHelp);
        Assert.Null(result.Error);
        var options = Assert.IsType<GeneratorOptions>(result.Options);
        Assert.Equal("definitions", options.InputPath);
        Assert.Equal("generated", options.OutputPath);
        Assert.Equal("MyFhirSdk.Generated.Types", options.TargetNamespace);
        Assert.Equal("5.0.0", options.FhirVersion);
        Assert.Equal(["Address", "Period"], options.TypeNames);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_Help_ReturnsHelpResult(string argument)
    {
        var result = _parser.Parse([argument]);

        Assert.True(result.ShowHelp);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Parse_DuplicateType_ReturnsStableError()
    {
        var result = _parser.Parse(
        [
            "--input", "definitions",
            "--output", "generated",
            "--namespace", "MyFhirSdk.Generated.Types",
            "--fhir-version", "5.0.0",
            "--type", "Address",
            "--type", "Address"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "FHIR type 'Address' may only be specified once.",
            result.Error);
    }
}
