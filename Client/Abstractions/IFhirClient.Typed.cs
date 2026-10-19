using MyFhirSdk.Client.Search;
using MyFhirSdk.Core;

namespace MyFhirSdk.Client.Abstractions;

/// <summary>A context-bound REST client with version-owned Bundle and OperationOutcome types.</summary>
public interface IFhirClient<TBundle, TOperationOutcome> where TBundle : Resource where TOperationOutcome : Resource
{
    Task<TResource?> ReadAsync<TResource>(string id, CancellationToken cancellationToken = default) where TResource : Resource;
    Task<TResource> CreateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default) where TResource : Resource;
    Task<TResource> UpdateAsync<TResource>(TResource resource, CancellationToken cancellationToken = default) where TResource : Resource;
    Task<TBundle> SearchAsync<TResource>(string query, CancellationToken cancellationToken = default) where TResource : Resource;
    Task<TBundle> SearchAsync<TResource>(FhirSearchQuery query, CancellationToken cancellationToken = default) where TResource : Resource;
    Task<TOperationOutcome?> DeleteAsync<TResource>(string id, CancellationToken cancellationToken = default) where TResource : Resource;
}
