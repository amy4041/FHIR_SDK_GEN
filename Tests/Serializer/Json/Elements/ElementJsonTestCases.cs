using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class ElementJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Elements", "element-id-and-extension.json"),
            "Patient",
            CreateElementIdAndExtensionPatient)
    ];

    // 測 非 primitive 的 Element / DataType metadata

    private static Resource CreateElementIdAndExtensionPatient()
    {
        return new Patient
        {
            Id = "patient-element-metadata",
            Extension =
            {
                new Extension
                {
                    Url = "http://example.org/fhir/StructureDefinition/patient-note",
                    Value = new FhirString("front desk")
                }
            },
            Name =
            {
                new HumanName
                {
                    Id = "name-element",
                    Extension =
                    {
                        new Extension
                        {
                            Url = "http://example.org/fhir/StructureDefinition/name-source",
                            Value = new FhirString("passport")
                        }
                    },
                    Family = new FhirString("Lin")
                }
            }
        };
    }
}
