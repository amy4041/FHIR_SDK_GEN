namespace MyFhirSdk.Tests.Validation;

public sealed class FhirValidatorTests
{
    [Fact]
    public void ValidateRejectsNullResource()
    {
        var validator = new FhirValidator();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    [Fact]
    public void ValidateReturnsSuccessForEmptyOptionalPatient()
    {
        var validator = new FhirValidator();

        var result = validator.Validate(new Patient());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }
}
