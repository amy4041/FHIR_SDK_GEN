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
    [InlineData("oid", "FhirOid")]
    [InlineData("time", "FhirTime")]
    [InlineData("uuid", "FhirUuid")]
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
    }

    [Fact]
    public void PrimitiveMappings_AreDerivedFromSupportedValidatedPolicyEntries()
    {
        var mappings = PrimitivePolicyTestContext.GetMappingView().Mappings;

        Assert.Equal(20, mappings.Count);
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
            mapping => mapping.FhirTypeName == "xhtml");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknownType")]
    [InlineData("String")]
    [InlineData("xhtml")]
    public void TryMap_WithUnknownType_ReturnsFalse(string? fhirTypeCode)
    {
        var wasMapped = _mapper.TryMap(fhirTypeCode, out var mapping);

        Assert.False(wasMapped);
        Assert.Null(mapping);
    }
}
