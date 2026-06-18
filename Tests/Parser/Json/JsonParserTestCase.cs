using MyFhirSdk.Serialization.Json;

public sealed record JsonParserTestCase(
    string FixtureFileName,
    string ResourceName,
    Action<FhirJsonParser, string> AssertParsedResource)
{
    public override string ToString()
    {
        return FixtureFileName;
    }
}
