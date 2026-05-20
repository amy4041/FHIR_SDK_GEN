namespace MyFhirSdk.Client.Requests;

/// <summary>
/// Builds absolute FHIR REST request URIs from the configured base endpoint.
/// </summary>
public sealed class FhirRequestUriBuilder
{
    private readonly Uri _baseAddress;

    /// <summary>
    /// Creates a URI builder.
    /// </summary>
    public FhirRequestUriBuilder(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("FHIR base address must be absolute.", nameof(baseAddress));
        }

        _baseAddress = EnsureTrailingSlash(baseAddress);
    }

    /// <summary>
    /// Builds a resource type endpoint URI, such as /Patient.
    /// </summary>
    public Uri BuildResourceTypeUri(string resourceType)
    {
        ValidateResourceType(resourceType);

        return new Uri(_baseAddress, Uri.EscapeDataString(resourceType));
    }

    /// <summary>
    /// Builds a resource instance endpoint URI, such as /Patient/123.
    /// </summary>
    public Uri BuildResourceInstanceUri(string resourceType, string id)
    {
        ValidateResourceType(resourceType);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("FHIR resource id cannot be empty.", nameof(id));
        }

        var relativePath = $"{Uri.EscapeDataString(resourceType)}/{Uri.EscapeDataString(id)}";
        return new Uri(_baseAddress, relativePath);
    }

    /// <summary>
    /// Builds a resource search endpoint URI with an optional query string.
    /// </summary>
    public Uri BuildSearchUri(string resourceType, string? query)
    {
        var uri = BuildResourceTypeUri(resourceType);
        var normalizedQuery = NormalizeQuery(query);

        if (normalizedQuery.Length == 0)
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Query = normalizedQuery
        };

        return builder.Uri;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var builder = new UriBuilder(uri);

        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }

    private static string NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        return query.Trim().TrimStart('?');
    }

    private static void ValidateResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            throw new ArgumentException("FHIR resource type cannot be empty.", nameof(resourceType));
        }
    }
}
