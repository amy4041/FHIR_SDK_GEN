using MyFhirSdk.Core;
using MyFhirSdk.Resources;

public sealed class FhirJsonParserCharacterizationTests
{
    [Fact]
    public void Parse_AbstractResource_ResolvesConcreteResourceType()
    {
        const string json = """
            {
              "resourceType": "Patient",
              "id": "patient-1"
            }
            """;

        var resource = new FhirJsonParser().Parse<Resource>(json);

        var patient = Assert.IsType<Patient>(resource);
        Assert.Equal("patient-1", patient.Id);
    }

    [Fact]
    public void Parse_MetadataOnlyPrimitive_PreservesElementMetadataWithoutRawValue()
    {
        const string json = """
            {
              "resourceType": "Patient",
              "_active": {
                "id": "active-metadata"
              }
            }
            """;

        var patient = new FhirJsonParser().Parse<Patient>(json);

        Assert.NotNull(patient.Active);
        Assert.Null(patient.Active.Value);
        Assert.Equal("active-metadata", patient.Active.Id);
    }

    [Fact]
    public void Parse_Integer64FromJsonNumber_ThrowsFhirSdkException()
    {
        const string json = """
            {
              "resourceType": "Practitioner",
              "photo": [
                {
                  "size": 1048576
                }
              ]
            }
            """;

        var exception = Assert.Throws<FhirSdkException>(
            () => new FhirJsonParser().Parse<Practitioner>(json));

        Assert.Contains(
            "FHIR integer64 values must be JSON strings.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DecimalFromJsonString_ThrowsFhirSdkException()
    {
        const string json = """
            {
              "resourceType": "Practitioner",
              "photo": [
                {
                  "duration": "2.50"
                }
              ]
            }
            """;

        var exception = Assert.Throws<FhirSdkException>(
            () => new FhirJsonParser().Parse<Practitioner>(json));

        Assert.Contains(
            "FHIR decimal values must be JSON numbers.",
            exception.Message,
            StringComparison.Ordinal);
    }
}
