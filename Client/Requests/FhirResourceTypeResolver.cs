using MyFhirSdk.Core;
using MyFhirSdk.ModelMetadata;

namespace MyFhirSdk.Client.Requests;

/// <summary>Resolves exact registered resource types from one immutable model context.</summary>
public sealed class FhirResourceTypeResolver
{
    private readonly FhirModelContext _context;
    public FhirResourceTypeResolver(FhirModelContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));
    public string GetResourceType<TResource>() where TResource : Resource
        => _context.Metadata.GetRequiredResource(typeof(TResource)).FhirTypeName;
    public string GetResourceType(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _context.EnsureOwnership(resource);
        return _context.Metadata.GetRequiredResource(resource.GetType()).FhirTypeName;
    }
}
