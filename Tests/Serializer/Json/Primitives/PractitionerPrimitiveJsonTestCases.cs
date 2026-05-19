using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;

internal static class PractitionerPrimitiveJsonTestCases
{
    public static IReadOnlyList<JsonFixtureTestCase> All { get; } =
    [
        new JsonFixtureTestCase(
            Path.Combine("Primitives", "practitioner-integer64-decimal-literal.json"),
            "Practitioner",
            CreatePractitionerWithPhotoPrimitives)
    ];

    private static Resource CreatePractitionerWithPhotoPrimitives()
    {
        return new Practitioner
        {
            Id = "practitioner-integer64-decimal-literal",
            Photo =
            {
                new Attachment
                {
                    ContentType = new FhirCode("image/jpeg"),
                    Size = new FhirInteger64("1048576"),
                    Duration = new FhirDecimal("2.50")
                }
            }
        };
    }
}
