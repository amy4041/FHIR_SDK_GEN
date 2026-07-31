using MyFhirSdk.CodeGen.Mapping;
using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Mapping;

public sealed class CardinalityMapperTests
{
    private readonly CardinalityMapper _mapper = new();

    [Theory]
    [InlineData(0, "1", false, false)]
    [InlineData(1, "1", false, true)]
    [InlineData(0, "*", true, false)]
    [InlineData(1, "*", true, true)]
    public void TryMap_WithSupportedCardinality_ReturnsResolvedModel(
        int min,
        string max,
        bool expectedIsCollection,
        bool expectedIsRequired)
    {
        var wasMapped = _mapper.TryMap(min, max, out var mapping);

        Assert.True(wasMapped);
        var resolvedMapping = Assert.IsType<CardinalityModel>(mapping);
        Assert.Equal(min, resolvedMapping.Min);
        Assert.Equal(max, resolvedMapping.Max);
        Assert.Equal(
            expectedIsCollection,
            resolvedMapping.IsCollection);
        Assert.Equal(
            expectedIsRequired,
            resolvedMapping.IsRequired);
    }

    [Theory]
    [InlineData(null, "1")]
    [InlineData(0, null)]
    [InlineData(0, "")]
    [InlineData(0, " ")]
    [InlineData(-1, "1")]
    [InlineData(2, "*")]
    [InlineData(0, "0")]
    [InlineData(0, "2")]
    [InlineData(0, " * ")]
    public void TryMap_WithMissingOrUnsupportedCardinality_ReturnsFalse(
        int? min,
        string? max)
    {
        var wasMapped = _mapper.TryMap(min, max, out var mapping);

        Assert.False(wasMapped);
        Assert.Null(mapping);
    }
}
