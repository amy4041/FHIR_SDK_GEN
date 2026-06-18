using MyFhirSdk.Core;

public sealed record JsonFixtureTestCase(
    string FixtureFileName,
    string ResourceName,
    Func<Resource> CreateResource)
{
    public override string ToString()
    {
        return FixtureFileName;
    }
}
