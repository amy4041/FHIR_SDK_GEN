using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class ExtensionValueQuantityJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Elements", "extension-value-quantity.json"),
            "Patient",
            CreatePatientWithValueQuantityExtension)
    ];

    private static Resource CreatePatientWithValueQuantityExtension()
    {
        return new Patient
        {
            Id = "patient-value-quantity-extension",
            Extension =
            {
                new Extension
                {
                    Url = "http://example.org/fhir/StructureDefinition/body-weight",
                    Value = new SimpleQuantity
                    {
                        Value = new FhirDecimal("72.50"),
                        Unit = new FhirString("kg"),
                        System = new FhirUri("http://unitsofmeasure.org"),
                        Code = new FhirCode("kg")
                    }
                }
            }
        };
    }
}
