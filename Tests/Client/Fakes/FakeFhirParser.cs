namespace MyFhirSdk.Tests.Client.Fakes;

public sealed class FakeFhirParser : IFhirParser
{
    private readonly Dictionary<Type, Resource> _resources = new();

    public int ParseCallCount { get; private set; }

    public string? LastJson { get; private set; }

    public Type? LastResourceType { get; private set; }

    public Exception? ExceptionToThrow { get; set; }

    public void AddResource<TResource>(TResource resource)
        where TResource : Resource
    {
        _resources[typeof(TResource)] = resource;
    }

    public TResource Parse<TResource>(string json)
        where TResource : Resource
    {
        ParseCallCount++;
        LastJson = json;
        LastResourceType = typeof(TResource);

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        if (_resources.TryGetValue(typeof(TResource), out var resource))
        {
            return (TResource)resource;
        }

        return Activator.CreateInstance<TResource>();
    }
}
