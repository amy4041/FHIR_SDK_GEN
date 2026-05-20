namespace MyFhirSdk.Tests.Client.Fakes;

public sealed class FakeFhirSerializer : IFhirSerializer
{
    public string SerializedJson { get; set; } = "{\"resourceType\":\"Patient\"}";

    public int SerializeCallCount { get; private set; }

    public Resource? LastResource { get; private set; }

    public string Serialize<TResource>(TResource resource)
        where TResource : Resource
    {
        SerializeCallCount++;
        LastResource = resource;

        return SerializedJson;
    }
}
