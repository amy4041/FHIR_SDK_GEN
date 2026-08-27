using MyFhirSdk.CodeGen.Mapping;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Mapping;

public sealed class CSharpTypeMapperTests
{
    private readonly CSharpTypeMapper _mapper =
        PrimitivePolicyTestContext.CreateTypeMapper();

    [Theory]
    [InlineData("boolean", "FhirBoolean")]
    [InlineData("string", "FhirString")]
    [InlineData("code", "FhirCode")]
    [InlineData("id", "FhirId")]
    [InlineData("uri", "FhirUri")]
    [InlineData("url", "FhirUrl")]
    [InlineData("canonical", "FhirCanonical")]
    [InlineData("integer", "FhirInteger")]
    [InlineData("integer64", "FhirInteger64")]
    [InlineData("decimal", "FhirDecimal")]
    [InlineData("date", "FhirDate")]
    [InlineData("dateTime", "FhirDateTime")]
    [InlineData("instant", "FhirInstant")]
    [InlineData("positiveInt", "FhirPositiveInt")]
    [InlineData("unsignedInt", "FhirUnsignedInt")]
    [InlineData("base64Binary", "FhirBase64Binary")]
    [InlineData("markdown", "FhirMarkdown")]
    public void TryMap_WithPrimitive_ReturnsPrimitiveWrapper(
        string fhirTypeCode,
        string expectedTypeName)
    {
        var wasMapped = _mapper.TryMap(fhirTypeCode, out var mapping);

        Assert.True(wasMapped);
        var resolvedMapping = Assert.IsType<CSharpTypeMapping>(mapping);
        Assert.Equal(fhirTypeCode, resolvedMapping.FhirTypeCode);
        Assert.Equal(expectedTypeName, resolvedMapping.TypeName);
        Assert.Equal(
            $"MyFhirSdk.Primitives.{expectedTypeName}",
            resolvedMapping.CSharpTypeName);
        Assert.Equal(CSharpTypeCategory.Primitive, resolvedMapping.Category);
        Assert.Equal("MyFhirSdk.Primitives", resolvedMapping.RequiredUsing);
        Assert.True(resolvedMapping.RequiresUsing);
        Assert.False(resolvedMapping.IsPreviewType);
    }

    [Fact]
    public void TryMap_WithKnownComplexType_ReturnsSdkType()
    {
        var wasMapped = _mapper.TryMap("Period", out var mapping);

        Assert.True(wasMapped);
        var resolvedMapping = Assert.IsType<CSharpTypeMapping>(mapping);
        Assert.Equal("Period", resolvedMapping.TypeName);
        Assert.Equal("MyFhirSdk.Types.Period", resolvedMapping.CSharpTypeName);
        Assert.Equal(CSharpTypeCategory.Complex, resolvedMapping.Category);
        Assert.Equal("MyFhirSdk.Types", resolvedMapping.RequiredUsing);
        Assert.True(resolvedMapping.RequiresUsing);
        Assert.False(resolvedMapping.IsPreviewType);
    }

    [Fact]
    public void PrimitiveMappings_AreDerivedFromSupportedValidatedPolicyEntries()
    {
        var mappings = PrimitivePolicyTestContext.GetMappingView().Mappings;

        Assert.Equal(17, mappings.Count);
        Assert.Equal(
            mappings.OrderBy(mapping => mapping.FhirTypeName, StringComparer.Ordinal),
            mappings);
        Assert.Contains(
            mappings,
            mapping => mapping is
            {
                FhirTypeName: "integer64",
                WrapperName: "FhirInteger64",
                Namespace: "MyFhirSdk.Primitives"
            });
        Assert.DoesNotContain(
            mappings,
            mapping => mapping.FhirTypeName is "oid" or "time" or "uuid" or "xhtml");
    }

    [Fact]
    public void TryMap_WithPreviewType_ReturnsPreviewNamespace()
    {
        IReadOnlySet<string> previewTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "HumanName",
                "Period"
            };

        var wasMapped = _mapper.TryMap(
            "Period",
            previewTypes,
            "MyFhirSdk.GeneratorFixtures.Types",
            out var mapping);

        Assert.True(wasMapped);
        var resolvedMapping = Assert.IsType<CSharpTypeMapping>(mapping);
        Assert.Equal(
            "MyFhirSdk.GeneratorFixtures.Types.Period",
            resolvedMapping.CSharpTypeName);
        Assert.Equal(
            "MyFhirSdk.GeneratorFixtures.Types",
            resolvedMapping.RequiredUsing);
        Assert.Equal(CSharpTypeCategory.Complex, resolvedMapping.Category);
        Assert.True(resolvedMapping.IsPreviewType);
    }

    [Fact]
    public void TryMap_WithFuturePreviewType_DoesNotRequireSdkWhitelist()
    {
        IReadOnlySet<string> previewTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "FutureType"
            };

        var wasMapped = _mapper.TryMap(
            "FutureType",
            previewTypes,
            "MyFhirSdk.GeneratorFixtures.Types",
            out var mapping);

        Assert.True(wasMapped);
        var resolvedMapping = Assert.IsType<CSharpTypeMapping>(mapping);
        Assert.Equal(
            "MyFhirSdk.GeneratorFixtures.Types.FutureType",
            resolvedMapping.CSharpTypeName);
        Assert.True(resolvedMapping.IsPreviewType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknownType")]
    [InlineData("String")]
    [InlineData("oid")]
    [InlineData("time")]
    [InlineData("uuid")]
    [InlineData("xhtml")]
    public void TryMap_WithUnknownType_ReturnsFalse(string? fhirTypeCode)
    {
        var wasMapped = _mapper.TryMap(fhirTypeCode, out var mapping);

        Assert.False(wasMapped);
        Assert.Null(mapping);
    }
}
