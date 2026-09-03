using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

internal static class ExtensionValueNewPrimitiveJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Elements", "extension-value-new-primitives.json"),
            "Patient",
            CreatePatient)
    ];

    private static Resource CreatePatient()
    {
        return new Patient
        {
            Id = "patient-new-primitives",
            Extension =
            {
                CreateExtension("oid", new FhirOid("urn:oid:1.2.840.10008")),
                CreateExtension("time", new FhirTime("23:59:60.123456789")),
                CreateExtension(
                    "uuid",
                    new FhirUuid("urn:uuid:123e4567-e89b-12d3-a456-426614174000"))
            }
        };
    }

    private static Extension CreateExtension(string name, IFhirExtensionValue value)
    {
        return new Extension
        {
            Url = $"http://example.org/fhir/StructureDefinition/{name}",
            Value = value
        };
    }
}
