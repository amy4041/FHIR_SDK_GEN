using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class PrimitiveJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Primitives", "primitive-values-and-metadata.json"),
            "Patient",
            CreatePrimitiveValuesAndMetadataPatient)
    ];

    private static Resource CreatePrimitiveValuesAndMetadataPatient()
    {
        return new Patient
        {
            Id = "patient-primitive-metadata",
            Active = new FhirBoolean(true)
            {
                Id = "active-element",
                Extension =
                {
                    new Extension
                    {
                        Url = "http://example.org/fhir/StructureDefinition/source",
                        Value = new FhirString("registration")
                    }
                }
            },
            Gender = new FhirCode("male"),
            BirthDate = new FhirDate("1974-12-25")
        };
    }
}
