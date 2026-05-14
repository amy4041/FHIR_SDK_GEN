using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class PatientJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Resources", "patient-simple.json"),
            "Patient",
            CreateSimplePatient),
        new JsonFixtureTestCase(
            Path.Combine("Resources", "patient-list-name.json"),
            "Patient",
            CreatePatientWithListName)
    ];

    private static Resource CreateSimplePatient()
    {
        return new Patient
        {
            Id = "patient-simple",
            Active = new FhirBoolean(true),
            Gender = new FhirCode("male"),
            BirthDate = new FhirDate("1974-12-25"),
            Identifier =
            {
                new Identifier
                {
                    System = new FhirUri("http://hospital.example.org/patients"),
                    Value = new FhirString("MRN-12345")
                }
            },
            Name =
            {
                new HumanName
                {
                    Use = new FhirCode("official"),
                    Family = new FhirString("Chalmers"),
                    Given =
                    {
                        new FhirString("Peter"),
                        new FhirString("James")
                    }
                }
            },
            Telecom =
            {
                new ContactPoint
                {
                    System = new FhirCode("phone"),
                    Value = new FhirString("555-0100"),
                    Use = new FhirCode("home")
                }
            },
            Address =
            {
                new Address
                {
                    Use = new FhirCode("home"),
                    Line =
                    {
                        new FhirString("534 Erewhon St")
                    },
                    City = new FhirString("PleasantVille"),
                    State = new FhirString("Vic"),
                    PostalCode = new FhirString("3999")
                }
            }
        };
    }

    private static Resource CreatePatientWithListName()
    {
        return new Patient
        {
            Id = "patient-list-name",
            Name =
            [
                new HumanName
                {
                    Family = new FhirString("F"),

                    Given =
                    [
                        new FhirString("John")
                    ]
                }
            ]
        };
    }
}
