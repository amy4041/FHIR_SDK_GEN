using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class MvpResourceJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Resources", "bundle-search-result.json"),
            "Bundle",
            CreateBundleSearchResult),
        new JsonFixtureTestCase(
            Path.Combine("Resources", "claim-professional.json"),
            "Claim",
            CreateProfessionalClaim),
        new JsonFixtureTestCase(
            Path.Combine("Resources", "coverage-primary.json"),
            "Coverage",
            CreatePrimaryCoverage),
        new JsonFixtureTestCase(
            Path.Combine("Resources", "encounter-ambulatory.json"),
            "Encounter",
            CreateAmbulatoryEncounter),
        new JsonFixtureTestCase(
            Path.Combine("Resources", "organization-clinic.json"),
            "Organization",
            CreateClinicOrganization),
        new JsonFixtureTestCase(
            Path.Combine("Resources", "practitioner-primary.json"),
            "Practitioner",
            CreatePrimaryPractitioner)
    ];

    private static Resource CreateBundleSearchResult()
    {
        return new Bundle
        {
            Id = "bundle-search-result",
            Type = new FhirCode("searchset"),
            Timestamp = new FhirInstant("2026-06-10T03:00:00Z"),
            Total = new FhirUnsignedInt(1),
            Link =
            {
                new BundleLink
                {
                    Relation = new FhirCode("self"),
                    Url = new FhirUri("https://fhir.example.org/Patient?family=Chalmers")
                }
            },
            Entry =
            {
                new BundleEntry
                {
                    FullUrl = new FhirUri("https://fhir.example.org/Patient/patient-simple"),
                    Resource = new Patient
                    {
                        Id = "patient-simple",
                        Active = new FhirBoolean(true),
                        Name =
                        {
                            new HumanName
                            {
                                Family = new FhirString("Chalmers"),
                                Given =
                                {
                                    new FhirString("Peter")
                                }
                            }
                        }
                    },
                    Search = new BundleEntrySearch
                    {
                        Mode = new FhirCode("match"),
                        Score = new FhirDecimal("1.0")
                    }
                }
            }
        };
    }

    private static Resource CreateProfessionalClaim()
    {
        return new Claim
        {
            Id = "claim-professional",
            Identifier =
            {
                Identifier("http://payer.example.org/claims", "CLM-1001")
            },
            Status = new FhirCode("active"),
            Type = Concept(
                "http://terminology.hl7.org/CodeSystem/claim-type",
                "professional",
                "Professional"),
            Use = new FhirCode("claim"),
            Patient = Reference("Patient/patient-simple", "Peter Chalmers"),
            Created = new FhirDateTime("2026-06-10"),
            Insurer = Reference("Organization/acme-payer", "ACME Health Plan"),
            Provider = Reference("Practitioner/practitioner-primary", "Dr Alice Ng"),
            Priority = Concept(
                "http://terminology.hl7.org/CodeSystem/processpriority",
                "normal",
                "Normal"),
            Insurance =
            {
                new ClaimInsurance
                {
                    Sequence = new FhirPositiveInt(1),
                    Focal = new FhirBoolean(true),
                    Coverage = Reference("Coverage/coverage-primary")
                }
            },
            Item =
            {
                new ClaimItem
                {
                    Sequence = new FhirPositiveInt(1),
                    ProductOrService = Concept(
                        "http://example.org/fhir/CodeSystem/services",
                        "office-visit",
                        "Office visit",
                        "Office visit"),
                    ServicedDate = new FhirDate("2026-06-09"),
                    Quantity = new SimpleQuantity
                    {
                        Value = new FhirDecimal("1"),
                        Unit = new FhirString("visit"),
                        System = new FhirUri("http://unitsofmeasure.org"),
                        Code = new FhirCode("{visit}")
                    },
                    UnitPrice = Money("125.00", "USD"),
                    Net = Money("125.00", "USD")
                }
            },
            Total = Money("125.00", "USD")
        };
    }

    private static Resource CreatePrimaryCoverage()
    {
        return new Coverage
        {
            Id = "coverage-primary",
            Identifier =
            {
                Identifier("http://payer.example.org/member-id", "MB-123")
            },
            Status = new FhirCode("active"),
            Kind = new FhirCode("insurance"),
            Type = Concept(
                "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                "EHCPOL",
                "extended healthcare"),
            SubscriberId =
            {
                Identifier("http://payer.example.org/subscriber-id", "SUB-456")
            },
            Beneficiary = Reference("Patient/patient-simple", "Peter Chalmers"),
            Period = new Period
            {
                Start = new FhirDateTime("2026-01-01"),
                End = new FhirDateTime("2026-12-31")
            },
            Insurer = Reference("Organization/acme-payer", "ACME Health Plan"),
            Class =
            {
                new CoverageClass
                {
                    Type = Concept(
                        "http://terminology.hl7.org/CodeSystem/coverage-class",
                        "group",
                        "Group"),
                    Value = Identifier("http://payer.example.org/groups", "GRP-77"),
                    Name = new FhirString("Gold Plan")
                }
            },
            Order = new FhirPositiveInt(1),
            Network = new FhirString("preferred"),
            Subrogation = new FhirBoolean(false)
        };
    }

    private static Resource CreateAmbulatoryEncounter()
    {
        return new Encounter
        {
            Id = "encounter-ambulatory",
            Identifier =
            {
                Identifier("http://hospital.example.org/encounters", "ENC-20260609-1")
            },
            Status = new FhirCode("finished"),
            Class =
            {
                Concept(
                    "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                    "AMB",
                    "ambulatory")
            },
            Type =
            {
                Concept(
                    "http://snomed.info/sct",
                    "185349003",
                    "Encounter for check up")
            },
            Subject = Reference("Patient/patient-simple", "Peter Chalmers"),
            ServiceProvider = Reference("Organization/acme-clinic", "ACME Clinic"),
            Participant =
            {
                new EncounterParticipant
                {
                    Type =
                    {
                        Concept(
                            "http://terminology.hl7.org/CodeSystem/v3-ParticipationType",
                            "PPRF",
                            "primary performer")
                    },
                    Actor = Reference("Practitioner/practitioner-primary", "Dr Alice Ng")
                }
            },
            ActualPeriod = new Period
            {
                Start = new FhirDateTime("2026-06-09T09:00:00Z"),
                End = new FhirDateTime("2026-06-09T09:30:00Z")
            }
        };
    }

    private static Resource CreateClinicOrganization()
    {
        return new Organization
        {
            Id = "organization-clinic",
            Identifier =
            {
                Identifier("http://example.org/organizations", "ACME-CLINIC")
            },
            Active = new FhirBoolean(true),
            Type =
            {
                Concept(
                    "http://terminology.hl7.org/CodeSystem/organization-type",
                    "prov",
                    "Healthcare Provider",
                    "Healthcare Provider")
            },
            Name = new FhirString("ACME Clinic"),
            Alias =
            {
                new FhirString("ACME Family Care")
            },
            PartOf = Reference("Organization/acme-health", "ACME Health Network")
        };
    }

    private static Resource CreatePrimaryPractitioner()
    {
        return new Practitioner
        {
            Id = "practitioner-primary",
            Identifier =
            {
                Identifier("http://example.org/licenses", "MD-12345")
            },
            Active = new FhirBoolean(true),
            Name =
            {
                new HumanName
                {
                    Use = new FhirCode("official"),
                    Family = new FhirString("Ng"),
                    Given =
                    {
                        new FhirString("Alice")
                    },
                    Prefix =
                    {
                        new FhirString("Dr")
                    }
                }
            },
            Telecom =
            {
                new ContactPoint
                {
                    System = new FhirCode("email"),
                    Value = new FhirString("alice.ng@example.org"),
                    Use = new FhirCode("work")
                }
            },
            Gender = new FhirCode("female"),
            BirthDate = new FhirDate("1980-04-12"),
            Qualification =
            {
                new PractitionerQualification
                {
                    Identifier =
                    {
                        Identifier("http://example.org/board-certifications", "BC-987")
                    },
                    Code = Concept(
                        "http://terminology.hl7.org/CodeSystem/v2-0360",
                        "MD",
                        "Doctor of Medicine"),
                    Issuer = Reference("Organization/acme-board", "ACME Medical Board")
                }
            }
        };
    }

    private static Identifier Identifier(string system, string value)
    {
        return new Identifier
        {
            System = new FhirUri(system),
            Value = new FhirString(value)
        };
    }

    private static CodeableConcept Concept(
        string system,
        string code,
        string display,
        string? text = null)
    {
        var concept = new CodeableConcept
        {
            Text = text is null ? null : new FhirString(text)
        };

        concept.Coding.Add(new Coding
        {
            System = new FhirUri(system),
            Code = new FhirCode(code),
            Display = new FhirString(display)
        });

        return concept;
    }

    private static Reference Reference(string reference, string? display = null)
    {
        return new Reference
        {
            ReferenceValue = new FhirString(reference),
            Display = display is null ? null : new FhirString(display)
        };
    }

    private static Money Money(string value, string currency)
    {
        return new Money
        {
            Value = new FhirDecimal(value),
            Currency = new FhirCode(currency)
        };
    }
}
