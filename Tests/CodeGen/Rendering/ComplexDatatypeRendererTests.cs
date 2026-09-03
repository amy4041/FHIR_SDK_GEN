using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Tests.Generation;
using MyFhirSdk.CodeGen.Tests.Ir;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Rendering;

public sealed class ComplexDatatypeRendererTests
{
    [Fact]
    public async Task Render_OfficialPeriod_MatchesGoldenSource()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Period");
        var period = Assert.Single(ir.Declarations);

        var source = new ComplexDatatypeRenderer().Render(period);

        var goldenPath = Path.Combine(
            AppContext.BaseDirectory,
            "GoldenFiles",
            "R5",
            "ComplexDatatypes",
            "Period.golden.cs.txt");
        Assert.Equal(
            Normalize(await File.ReadAllTextAsync(goldenPath)),
            Normalize(source));
        Assert.DoesNotContain('\r', source);
    }

    [Fact]
    public async Task Render_OfficialReference_EmitsApprovedJsonNameOverride()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Reference");
        var reference = Assert.Single(ir.Declarations, declaration =>
            declaration.FhirName == "Reference");

        var source = new ComplexDatatypeRenderer().Render(reference);

        Assert.Contains("using System.Text.Json.Serialization;", source, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"reference\")]", source, StringComparison.Ordinal);
        Assert.Contains("public FhirString? ReferenceValue { get; set; }", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_DerivedDatatype_PreservesExtensibilityAndSealedLeaf()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Duration");
        var quantity = Assert.Single(ir.Declarations, declaration => declaration.FhirName == "Quantity");
        var duration = Assert.Single(ir.Declarations, declaration => declaration.FhirName == "Duration");
        var renderer = new ComplexDatatypeRenderer();

        var quantitySource = renderer.Render(quantity);
        var durationSource = renderer.Render(duration);

        Assert.Contains("public class Quantity : DataType", quantitySource, StringComparison.Ordinal);
        Assert.Contains("public sealed class Duration : Quantity", durationSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_OrdinaryChoice_EmitsNullableAlternativeProperties()
    {
        var ir = await ModelIrBuilderTests.BuildChoiceDatatypeIrAsync();
        var declaration = Assert.Single(ir.Declarations);

        var source = new ComplexDatatypeRenderer().Render(declaration);

        Assert.Contains("public FhirBoolean? ValueBoolean { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("public FhirString? ValueString { get; set; }", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Value[x]", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_SelfReferenceAndContentReference_UsesResolvedClrTypes()
    {
        var ir = await ModelIrBuilderTests.BuildRecursiveDatatypeIrAsync();
        var declaration = Assert.Single(ir.Declarations);

        var source = new ComplexDatatypeRenderer().Render(declaration);

        Assert.Contains("public Recursive? Child { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("public FhirString? Alias { get; set; }", source, StringComparison.Ordinal);
        Assert.DoesNotContain("#Recursive.value", source, StringComparison.Ordinal);
        var result = new ComplexDatatypeGenerationPipeline().Generate(ir);
        Assert.True(result.IsSuccess, ComplexDatatypeTestContext.Describe(result.Diagnostics));
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
