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

    public static void ValidateReportsInvalidFhirMarkdown()
    {
        var organization = new Organization
        {
            Description = new FhirMarkdown("")
        };

        var result = new FhirValidator().Validate(organization);

        TestAssert.HasIssue(result, "Organization.description", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirUrl()
    {
        var practitioner = new Practitioner
        {
            Photo =
            [
                new Attachment
                {
                    Url = new FhirUrl("Patient/123")
                }
            ]
        };

        var result = new FhirValidator().Validate(practitioner);

        TestAssert.HasIssue(result, "Practitioner.photo[0].url", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirCanonical()
    {
        var patient = new Patient
        {
            Extension =
            [
                new Extension
                {
                    Value = new FhirCanonical("relative/path")
                }
            ]
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.extension[0].value", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirId()
    {
        var patient = new Patient
        {
            Extension =
            [
                new Extension
                {
                    Value = new FhirId("a/b")
                }
            ]
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.HasIssue(result, "Patient.extension[0].value", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirDateTime()
    {
        var claim = CreateValidClaim();
        claim.Created = new FhirDateTime("2026-06-05T10:30:00");

        var result = new FhirValidator().Validate(claim);

        TestAssert.HasIssue(result, "Claim.created", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirInstant()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Timestamp = new FhirInstant("2026-06-05")
        };

        var result = new FhirValidator().Validate(bundle);

        TestAssert.HasIssue(result, "Bundle.timestamp", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirDecimal()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Entry =
            [
                new BundleEntry
                {
                    Search = new BundleEntrySearch
                    {
                        Score = new FhirDecimal("01.20")
                    }
                }
            ]
        };

        var result = new FhirValidator().Validate(bundle);

        TestAssert.HasIssue(result, "Bundle.entry[0].search.score", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateReportsInvalidFhirInteger64()
    {
        var practitioner = new Practitioner
        {
            Photo =
            [
                new Attachment
                {
                    Size = new FhirInteger64("9223372036854775808")
                }
            ]
        };

        var result = new FhirValidator().Validate(practitioner);

        TestAssert.HasIssue(result, "Practitioner.photo[0].size", ValidationIssueCode.PrimitiveFormat);
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

    public static void ValidateReportsInvalidFhirBase64Binary()
    {
        var practitioner = new Practitioner
        {
            Photo =
            [
                new Attachment
                {
                    Data = new FhirBase64Binary("not base64")
                }
            ]
        };

        var result = new FhirValidator().Validate(practitioner);

        TestAssert.HasIssue(result, "Practitioner.photo[0].data", ValidationIssueCode.PrimitiveFormat);
    }

    public static void ValidateDoesNotReportIssueForValidBooleanAndInteger()
    {
        var patient = new Patient
        {
            Active = new FhirBoolean(false),
            MultipleBirthInteger = new FhirInteger(1)
        };

        var result = new FhirValidator().Validate(patient);

        TestAssert.IsTrue(result.IsValid);
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
