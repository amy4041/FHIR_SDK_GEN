using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Loading;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Cli;

public sealed class GeneratorCommandLineParserTests
{
    [Fact]
    public void Parse_CompilerSourceOverride_IsExplicitAndCannotBeRepeated()
    {
        var result = _parser.Parse([
            "--mode", "model", "--input", "package.tgz", "--output", "output",
            "--fhir-version", "5.0.0", "--package-id", "hl7.fhir.r5.core", "--package-version", "5.0.0",
            "--compiler-source", "external/SimpleQuantity.cs"]);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("external/SimpleQuantity.cs", result.AssetOverrides!.CompilerSourcePath);
        var duplicate = _parser.Parse([
            "--mode", "model", "--compiler-source", "first.cs", "--compiler-source", "second.cs"]);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("Option '--compiler-source' may only be specified once.", duplicate.Error);
        var primitive = _parser.Parse(["--mode", "primitive", "--compiler-source", "SimpleQuantity.cs"]);
        Assert.False(primitive.IsSuccess);
        Assert.Equal("Option '--compiler-source' is only valid in model mode.", primitive.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Parse_EmptyCompilerSourceOverride_IsRejected(string value)
    {
        var result = _parser.Parse(["--mode", "model", "--compiler-source", value]);
        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--compiler-source' requires a non-empty path.", result.Error);
    }

    private readonly GeneratorCommandLineParser _parser = new(
        new ToolAssetResolver(AppContext.BaseDirectory));

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
        var definitions = Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "StructureDefinitions", "Primitives", "R5");
        var result = _parser.Parse(
        [
            "--mode", "primitive",
            "--input", definitions,
            "--policy", "policy.json",
            "--output", "Generated/R5/Primitives",
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PrimitiveGenerationOptions>(
            result.PrimitiveOptions);
        Assert.Equal(definitions, options.DefinitionsPath);
        Assert.Equal(PrimitiveDefinitionInputKind.Directory, options.InputKind);
        Assert.Equal("policy.json", options.PolicyPath);
        Assert.Equal("hl7.fhir.r5.core", options.FhirPackageId);
        Assert.Equal("5.0.0", options.FhirPackageVersion);
        Assert.Equal("1.3.1", options.CodeGenVersion);
    }

    [Fact]
    public void Parse_PrimitiveModeWithoutPolicy_UsesPackagedReleasePolicy()
    {
        var result = _parser.Parse(
        [
            "--mode", "primitive",
            "--input", Path.Combine(AppContext.BaseDirectory, "Fixtures", "StructureDefinitions", "Primitives", "R5"),
            "--output", "Generated/R5/Primitives",
            "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0"
        ]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.EndsWith("primitive-generation-policy.json", result.PrimitiveOptions!.PolicyPath);
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
    public void Parse_ModelAssetOverrides_AreExplicitAndPolicyRootIsNotMixed()
    {
        var result = _parser.Parse([
            "--mode", "model", "--input", "r5.tgz", "--output", "generated",
            "--fhir-version", "5.0.0", "--package-id", "hl7.fhir.r5.core",
            "--package-version", "5.0.0", "--policy-root", "custom-policy",
            "--runtime-contract", "contract.json",
            "--runtime-reference", "z.dll", "--runtime-reference", "a.dll"]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("custom-policy", result.AssetOverrides!.PolicyRoot);
        Assert.Equal("contract.json", result.AssetOverrides.RuntimeContractPath);
        Assert.Equal(["z.dll", "a.dll"], result.AssetOverrides.RuntimeReferences);
        Assert.All(
            new[]
            {
                result.ModelOptions!.PrimitivePolicyPath,
                result.ModelOptions.OwnershipPolicyPath,
                result.ModelOptions.ValidationPolicyPath
            },
            path => Assert.Contains("custom-policy", path, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_PrimitivePolicyRoot_ReturnsStableError()
    {
        var result = _parser.Parse([
            "--mode", "primitive", "--input", "definitions", "--policy", "policy.json",
            "--output", "generated", "--fhir-version", "5.0.0",
            "--package-id", "hl7.fhir.r5.core", "--package-version", "5.0.0",
            "--policy-root", "custom-policy"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--policy-root' is only valid in model mode.", result.Error);
    }

    [Fact]
    public void Parse_UnknownMode_ReturnsStableError()
    {
        var result = _parser.Parse(["--mode", "automatic"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown generator mode 'automatic'", result.Error);
    }
}
