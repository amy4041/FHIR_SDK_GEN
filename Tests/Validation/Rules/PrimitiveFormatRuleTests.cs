namespace MyFhirSdk.Tests.Validation.Rules;

public sealed class PrimitiveFormatRuleTests
{
    [Fact]
    public void ValidateReportsInvalidResourceId()
    {
        var patient = new Patient { Id = "a/b" };

        var result = new FhirValidator().Validate(patient);

        ValidationAssert.HasIssue(result, "Patient.id", ValidationIssueCode.PrimitiveFormat);
        var issue = Assert.Single(
            result.Issues,
            issue => issue.Path == "Patient.id" &&
                issue.Code == ValidationIssueCode.PrimitiveFormat);
        Assert.Equal("Patient.id has invalid FHIR id format.", issue.Message);
    }

    [Fact]
    public void ValidateReportsInvalidElementIdWithCompatiblePathAndMessage()
    {
        var patient = new Patient
        {
            Extension =
            [
                new Extension { Id = "a/b" }
            ]
        };

        var result = new FhirValidator().Validate(patient);

        var issue = Assert.Single(
            result.Issues,
            issue => issue.Path == "Patient.extension[0].id" &&
                issue.Code == ValidationIssueCode.PrimitiveFormat);
        Assert.Equal(
            "Patient.extension[0].id has invalid FHIR id format.",
            issue.Message);
    }

    [Fact]
    public void ValidateReportsInvalidFhirDate()
    {
        var patient = new Patient
        {
            BirthDate = new FhirDate("2026-99-99")
        };

        var result = new FhirValidator().Validate(patient);

        ValidationAssert.HasIssue(result, "Patient.birthDate", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirCode()
    {
        var patient = new Patient
        {
            Gender = new FhirCode(" female ")
        };

        var result = new FhirValidator().Validate(patient);

        ValidationAssert.HasIssue(result, "Patient.gender", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirUri()
    {
        var patient = new Patient
        {
            ManagingOrganization = new Reference
            {
                Type = new FhirUri("bad uri")
            }
        };

        var result = new FhirValidator().Validate(patient);

        ValidationAssert.HasIssue(result, "Patient.managingOrganization.type", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirMarkdown()
    {
        var organization = new Organization
        {
            Description = new FhirMarkdown("")
        };

        var result = new FhirValidator().Validate(organization);

        ValidationAssert.HasIssue(result, "Organization.description", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirUrl()
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

        ValidationAssert.HasIssue(result, "Practitioner.photo[0].url", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirCanonical()
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

        ValidationAssert.HasIssue(result, "Patient.extension[0].value", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirId()
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

        ValidationAssert.HasIssue(result, "Patient.extension[0].value", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirDateTime()
    {
        var claim = CreateValidClaim();
        claim.Created = new FhirDateTime("2026-06-05T10:30:00");

        var result = new FhirValidator().Validate(claim);

        ValidationAssert.HasIssue(result, "Claim.created", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirInstant()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Timestamp = new FhirInstant("2026-06-05")
        };

        var result = new FhirValidator().Validate(bundle);

        ValidationAssert.HasIssue(result, "Bundle.timestamp", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirDecimal()
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

        ValidationAssert.HasIssue(result, "Bundle.entry[0].search.score", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirInteger64()
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

        ValidationAssert.HasIssue(result, "Practitioner.photo[0].size", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidPositiveInt()
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

        ValidationAssert.HasIssue(result, "Claim.insurance[0].sequence", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidUnsignedInt()
    {
        var bundle = new Bundle
        {
            Type = new FhirCode("searchset"),
            Total = new FhirUnsignedInt(-1)
        };

        var result = new FhirValidator().Validate(bundle);

        ValidationAssert.HasIssue(result, "Bundle.total", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateReportsInvalidFhirBase64Binary()
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

        ValidationAssert.HasIssue(result, "Practitioner.photo[0].data", ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateAcceptsValidFhirBase64Binary()
    {
        var practitioner = new Practitioner
        {
            Photo =
            [
                new Attachment
                {
                    Data = new FhirBase64Binary("QQ==")
                }
            ]
        };

        var result = new FhirValidator().Validate(practitioner);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateReportsInvalidOidTimeAndUuidFormats()
    {
        var patient = new Patient
        {
            Extension =
            {
                new Extension { Url = "urn:test:oid", Value = new FhirOid("urn:oid:1.02.3") },
                new Extension { Url = "urn:test:time", Value = new FhirTime("24:00:00") },
                new Extension
                {
                    Url = "urn:test:uuid",
                    Value = new FhirUuid("123e4567-e89b-12d3-a456-426614174000")
                }
            }
        };

        var result = new FhirValidator().Validate(patient);

        ValidationAssert.HasIssue(
            result,
            "Patient.extension[0].value",
            ValidationIssueCode.PrimitiveFormat);
        ValidationAssert.HasIssue(
            result,
            "Patient.extension[1].value",
            ValidationIssueCode.PrimitiveFormat);
        ValidationAssert.HasIssue(
            result,
            "Patient.extension[2].value",
            ValidationIssueCode.PrimitiveFormat);
    }

    [Fact]
    public void ValidateAcceptsValidOidTimeAndUuidFormats()
    {
        var patient = new Patient
        {
            Extension =
            {
                new Extension { Url = "urn:test:oid", Value = new FhirOid("urn:oid:1.2.840.10008") },
                new Extension { Url = "urn:test:time", Value = new FhirTime("23:59:60.123456789") },
                new Extension
                {
                    Url = "urn:test:uuid",
                    Value = new FhirUuid("urn:uuid:123e4567-e89b-12d3-a456-426614174000")
                }
            }
        };

        var result = new FhirValidator().Validate(patient);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateDoesNotReportIssueForValidBooleanAndInteger()
    {
        var patient = new Patient
        {
            Active = new FhirBoolean(false),
            MultipleBirthInteger = new FhirInteger(1)
        };

        var result = new FhirValidator().Validate(patient);

        Assert.True(result.IsValid);
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
