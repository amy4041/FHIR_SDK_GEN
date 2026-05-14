using MyFhirSdk.Serialization.Json;

internal sealed record JsonParserTestCase(
    string FixtureFileName,
    string ResourceName,
    Action<FhirJsonParser, string> AssertParsedResource);
