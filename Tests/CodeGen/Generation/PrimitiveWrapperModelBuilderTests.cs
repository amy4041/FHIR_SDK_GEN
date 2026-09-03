using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Generation;

public sealed class PrimitiveWrapperModelBuilderTests
{
    private readonly PrimitiveWrapperModelBuilder _builder = new();

    [Fact]
    public async Task Build_WithOfficialCoverage_ProducesTwentyOrderedModels()
    {
        var coverage = await LoadCoverageAsync();

        var result = _builder.Build(coverage);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            [
                "base64Binary",
                "boolean",
                "canonical",
                "code",
                "date",
                "dateTime",
                "decimal",
                "id",
                "instant",
                "integer",
                "integer64",
                "markdown",
                "oid",
                "positiveInt",
                "string",
                "time",
                "unsignedInt",
                "uri",
                "url",
                "uuid"
            ],
            result.Value.Select(model => model.FhirTypeName));
        Assert.DoesNotContain(
            result.Value,
            model => model.FhirTypeName == "xhtml");
        Assert.All(
            result.Value,
            model =>
            {
                Assert.Equal("MyFhirSdk.Primitives", model.Namespace);
                Assert.Equal($"{model.WrapperName}.g.cs", model.FileName);
                Assert.DoesNotContain(
                    "StructureDefinition-",
                    model.Documentation,
                    StringComparison.Ordinal);
            });
        Assert.Throws<NotSupportedException>(
            () => ((IList<PrimitiveWrapperModel>)result.Value).Clear());
    }

    [Fact]
    public async Task Build_MapsLiteralBehaviorAndCompatibilityConstants()
    {
        var coverage = await LoadCoverageAsync();

        var result = _builder.Build(coverage);

        var decimalModel = Assert.Single(
            result.Value,
            model => model.FhirTypeName == "decimal");
        Assert.Equal("FhirDecimal", decimalModel.WrapperName);
        Assert.Equal("decimal?", decimalModel.ClrValueType);
        Assert.Equal(
            PrimitiveWrapperLiteralKind.Decimal,
            decimalModel.LiteralKind);
        Assert.Equal("Literal", decimalModel.LiteralPropertyName);
        Assert.Equal(
            PrimitiveWrapperToStringKind.LiteralOrInvariantValue,
            decimalModel.ToStringKind);
        Assert.Equal(
            ["MaxExponentDigits", "MaxFractionDigits", "MaxIntegerDigits"],
            decimalModel.PublicConstants.Select(constant => constant.Name));
        Assert.Equal([9L, 17L, 18L], decimalModel.PublicConstants.Select(
            constant => constant.Value));

        var integer64Model = Assert.Single(
            result.Value,
            model => model.FhirTypeName == "integer64");
        Assert.Equal(
            PrimitiveWrapperLiteralKind.Integer64,
            integer64Model.LiteralKind);

        var stringModel = Assert.Single(
            result.Value,
            model => model.FhirTypeName == "string");
        var maxLength = Assert.Single(stringModel.PublicConstants);
        Assert.Equal("MaxLength", maxLength.Name);
        Assert.Equal(1048576, maxLength.Value);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PrimitiveWrapperConstantModel>)
                decimalModel.PublicConstants).Clear());
    }

    [Fact]
    public async Task Build_NormalizesOfficialDocumentationWhitespace()
    {
        var coverage = await LoadCoverageAsync();

        var result = _builder.Build(coverage);

        var dateTime = Assert.Single(
            result.Value,
            model => model.FhirTypeName == "dateTime");
        Assert.DoesNotContain("  ", dateTime.Documentation, StringComparison.Ordinal);
        Assert.StartsWith("dateTime Type:", dateTime.Documentation);
    }

    [Fact]
    public async Task Build_WithUnsupportedNamespace_ReturnsFsg0024()
    {
        using var directory = new TemporaryDirectory();
        var policy = await File.ReadAllTextAsync(GetPolicyPath());
        var policyPath = await directory.WriteAsync(
            "unsupported-namespace-policy.json",
            policy.Replace(
                "MyFhirSdk.Primitives",
                "Example.Primitives",
                StringComparison.Ordinal));
        var coverageResult = await new PrimitiveInventoryCoveragePipeline().BuildAsync(
            GetDefinitionsPath(),
            policyPath,
            "5.0.0");
        Assert.True(coverageResult.IsSuccess);

        var result = _builder.Build(
            Assert.IsType<PrimitiveInventoryPolicyCoverage>(coverageResult.Value));

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(
            GeneratorDiagnosticCodes.InvalidPrimitiveWrapperModel,
            diagnostic.Code);
        Assert.Equal(Path.GetFullPath(policyPath), diagnostic.SourceFile);
    }

    private static async Task<PrimitiveInventoryPolicyCoverage> LoadCoverageAsync()
    {
        var result = await new PrimitiveInventoryCoveragePipeline().BuildAsync(
            GetDefinitionsPath(),
            GetPolicyPath(),
            "5.0.0");

        Assert.True(result.IsSuccess);
        return Assert.IsType<PrimitiveInventoryPolicyCoverage>(result.Value);
    }

    private static string GetDefinitionsPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "StructureDefinitions",
            "Primitives",
            "R5");
    }

    private static string GetPolicyPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "primitive-generation-policy.json");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MyFhirSdk.CodeGen.Tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public async Task<string> WriteAsync(string fileName, string content)
        {
            var filePath = System.IO.Path.Combine(Path, fileName);
            await File.WriteAllTextAsync(filePath, content);
            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
