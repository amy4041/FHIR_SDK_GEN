using MyFhirSdk.Core;

internal sealed record JsonFixtureTestCase(
    string FixtureFileName,
    string ResourceName,
    Func<Resource> CreateResource);
