namespace MyFhirSdk.Tests.Validation.Rules;

public static class ChoiceElementRuleTests
{
    public static void ValidateReportsPatientDeceasedChoiceConflict()
    {
        var patient = new Patient
        {
            DeceasedBoolean = new FhirBoolean(false),
            DeceasedDateTime = new FhirDateTime("2026-06-05")
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.deceased[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsPatientMultipleBirthChoiceConflict()
    {
        var patient = new Patient
        {
            MultipleBirthBoolean = new FhirBoolean(true),
            MultipleBirthInteger = new FhirInteger(1)
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.multipleBirth[x]", ValidationIssueCode.ChoiceElement);
    }

    public static void ValidateReportsPractitionerDeceasedChoiceConflict()
    {
        var practitioner = new Practitioner
        {
            DeceasedBoolean = new FhirBoolean(false),
            DeceasedDateTime = new FhirDateTime("2026-06-05")
        };

        var result = new FhirValidator().Validate(practitioner);

        TestAssert.HasIssue(result, "Practitioner.deceased[x]", ValidationIssueCode.ChoiceElement);
    }
}
