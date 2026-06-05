namespace MyFhirSdk.Tests.Validation;

public static class FhirValidatorTests
{
    public static void ValidateRejectsNullResource()
    {
        var validator = new FhirValidator();

        TestAssert.Throws<ArgumentNullException>(() => validator.Validate(null!));
    }

    public static void ValidateReturnsSuccessForEmptyOptionalPatient()
    {
        var validator = new FhirValidator();

        var result = validator.Validate(new Patient());

        TestAssert.IsTrue(result.IsValid);
        TestAssert.AreEqual(0, result.Issues.Count);
    }
}
