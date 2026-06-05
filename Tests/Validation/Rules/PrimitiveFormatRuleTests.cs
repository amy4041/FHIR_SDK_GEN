namespace MyFhirSdk.Tests.Validation.Rules;

public static class PrimitiveFormatRuleTests
{
    public static void ValidateReportsInvalidResourceId()
    {
        var patient = new Patient { Id = "a/b" };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.id", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirDate()
    {
        var patient = new Patient
        {
            BirthDate = new FhirDate("2026-99-99")
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.birthDate", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirCode()
    {
        var patient = new Patient
        {
            Gender = new FhirCode(" female ")
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.gender", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirUri()
    {
        var patient = new Patient
        {
            ManagingOrganization = new Reference
            {
                Type = new FhirUri("bad uri")
            }
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.managingOrganization.type", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidPositiveInt()
    {
        var claim = CreateValidClaim();
        claim.Insurance =
        [
            new ClaimInsurance
            {
                Sequence = new FhirPositiveInt(0),
                Focal = new FhirBoolean(true),
                Coverage = new Reference { ReferenceValue = new FhirString("Coverage/123") }
            }
        ];

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.insurance[0].sequence", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidUnsignedInt()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Total = new FhirUnsignedInt(-1)
        };

        var result = new FhirValidator().Validate(bundle);

        TestAssert.HasIssue(result, "Bundle.total", ValidationIssueCode.PrimitiveFormat);
    }

    private static Claim CreateValidClaim()
    {
        return new Claim
        {
            Status = new FhirCode("active"),
            Type = new CodeableConcept { Text = new FhirString("Professional") },
            Use = new FhirCode("claim"),
            Patient = new Reference { ReferenceValue = new FhirString("Patient/123") },
            Created = new FhirDateTime("2026-06-05")
        };
    }
}
