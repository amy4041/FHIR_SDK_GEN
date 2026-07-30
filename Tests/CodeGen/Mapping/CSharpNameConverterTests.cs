using MyFhirSdk.CodeGen.Mapping;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Mapping;

public sealed class CSharpNameConverterTests
{
    private readonly CSharpNameConverter _converter = new();

    [Theory]
    [InlineData("HumanName", "HumanName")]
    [InlineData("humanName", "HumanName")]
    [InlineData("postal-code", "PostalCode")]
    [InlineData("postal_code", "PostalCode")]
    [InlineData("FHIRPath", "FHIRPath")]
    [InlineData("123name", "_123name")]
    public void ConvertTypeName_WithConvertibleName_ReturnsLegalPascalCase(
        string input,
        string expected)
    {
        var result = _converter.ConvertTypeName(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(CSharpNameConversionFailure.None, result.Failure);
        Assert.Equal(expected, result.Name);
    }

    [Theory]
    [InlineData("family", "Family")]
    [InlineData("postalCode", "PostalCode")]
    [InlineData("HumanName.given", "Given")]
    [InlineData("class", "Class")]
    [InlineData("some property", "SomeProperty")]
    public void ConvertPropertyName_WithConvertibleName_ReturnsPropertyName(
        string input,
        string expected)
    {
        var result = _converter.ConvertPropertyName(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("HumanName.")]
    public void ConvertName_WithInvalidName_ReturnsInvalidIdentifier(string? input)
    {
        var result = _converter.ConvertPropertyName(input);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Name);
        Assert.Equal(
            CSharpNameConversionFailure.InvalidIdentifier,
            result.Failure);
    }

    [Fact]
    public void ConvertPropertyName_WhenConvertedNameAlreadyExists_ReturnsConflict()
    {
        IReadOnlySet<string> existingNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Family"
            };

        var result = _converter.ConvertPropertyName(
            "HumanName.family",
            existingNames);

        Assert.False(result.IsSuccess);
        Assert.Equal("Family", result.Name);
        Assert.Equal(CSharpNameConversionFailure.Conflict, result.Failure);
    }

    [Fact]
    public void ConvertPropertyName_UsesOrdinalConflictComparison()
    {
        IReadOnlySet<string> existingNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "family"
            };

        var result = _converter.ConvertPropertyName(
            "HumanName.family",
            existingNames);

        Assert.True(result.IsSuccess);
        Assert.Equal("Family", result.Name);
    }
}
