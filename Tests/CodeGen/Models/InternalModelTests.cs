using MyFhirSdk.CodeGen.Models;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Models;

public sealed class InternalModelTests
{
    [Fact]
    public void CardinalityModel_PreservesResolvedCardinality()
    {
        var cardinality = new CardinalityModel(
            min: 1,
            max: "*",
            isCollection: true,
            isRequired: true);

        Assert.Equal(1, cardinality.Min);
        Assert.Equal("*", cardinality.Max);
        Assert.True(cardinality.IsCollection);
        Assert.True(cardinality.IsRequired);
    }

    [Fact]
    public void CardinalityModel_RejectsInvalidRequiredValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CardinalityModel(-1, "1", false, false));
        Assert.Throws<ArgumentException>(
            () => new CardinalityModel(0, "", false, false));
    }
}
