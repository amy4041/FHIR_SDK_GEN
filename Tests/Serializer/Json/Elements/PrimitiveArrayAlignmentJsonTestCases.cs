using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class PrimitiveArrayAlignmentJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Elements", "primitive-array-given-alignment.json"),
            "Patient",
            CreatePatientWithGivenAlignment)
    ];

    private static Resource CreatePatientWithGivenAlignment()
    {
        return new Patient
        {
            Id = "patient-given-alignment",
            Contact =
            {
                new PatientContact
                {
                    Name = new HumanName
                    {
                        Family = new FhirString("Lin"),
                        Given =
                        {
                            new FhirString("Amy"),
                            new FhirString
                            {
                                Extension =
                                {
                                    new Extension
                                    {
                                        Url = "http://hl7.org/fhir/StructureDefinition/iso21090-EN-qualifier",
                                        Value = new FhirCode("MID")
                                    }
                                }
                            },
                            new FhirString("Cara")
                        }
                    }
                }
            }
        };
    }
}
