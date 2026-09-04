using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Cli;

public sealed class GeneratorCommandLineParserTests
{
    private readonly GeneratorCommandLineParser _parser = new();

    [Fact]
    public void Parse_WithoutMode_ReturnsStableError()
    {
        var result = _parser.Parse(
        [
            "--input", "definitions",
            "--output", "generated",
            "--namespace", "MyFhirSdk.Generated.Types",
            "--fhir-version", "5.0.0",
            "--policy", "primitive-policy.json",
            "--type", "Period",
            "--type", "Address"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Required option '--mode' was not provided.", result.Error);
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
    public void Parse_RemovedDatatypePreviewMode_ReturnsStableError()
    {
        var result = _parser.Parse(
        [
            "--mode", "datatype-preview"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Unknown generator mode 'datatype-preview'. Expected 'primitive' or 'model'.",
            result.Error);
    }

    [Fact]
    public void Parse_PrimitiveMode_ReturnsPrimitiveOptions()
    {
        var result = _parser.Parse(
        [
            "--mode", "primitive",
            "--input", "definitions",
            "--policy", "policy.json",
            "--output", "Generated/R5/Primitives",
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PrimitiveGenerationOptions>(
            result.PrimitiveOptions);
        Assert.Equal("definitions", options.DefinitionsPath);
        Assert.Equal("policy.json", options.PolicyPath);
        Assert.Equal("hl7.fhir.r5.core", options.FhirPackageId);
        Assert.Equal("5.0.0", options.FhirPackageVersion);
        Assert.Equal("1.0.0", options.CodeGenVersion);
    }

    [Fact]
    public void Parse_PrimitiveModeWithoutPolicy_ReturnsStableError()
    {
        var result = _parser.Parse(
        [
            "--mode", "primitive",
            "--input", "definitions",
            "--output", "Generated/R5/Primitives",
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Required option '--policy' was not provided.", result.Error);
    }

    [Fact]
    public void Parse_ModelMode_ReturnsSortedSelectedScopeOptions()
    {
        var result = _parser.Parse([
            "--mode", "model", "--input", "r5.tgz", "--output", "generated",
            "--fhir-version", "5.0.0", "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0",
            "--canonical", "http://hl7.org/fhir/StructureDefinition/Patient",
            "--canonical", "http://hl7.org/fhir/StructureDefinition/Address"]);

        Assert.True(result.IsSuccess, result.Error);
        var options = Assert.IsType<ModelGenerationOptions>(result.ModelOptions);
        Assert.Equal("r5.tgz", options.PackagePath);
        Assert.Equal([
            "http://hl7.org/fhir/StructureDefinition/Address",
            "http://hl7.org/fhir/StructureDefinition/Patient"], options.SelectedCanonicals);
    }

    [Fact]
    public void Parse_UnknownMode_ReturnsStableError()
    {
        var result = _parser.Parse(["--mode", "automatic"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown generator mode 'automatic'", result.Error);
    }
}
