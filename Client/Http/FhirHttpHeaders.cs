using System.Net.Http.Headers;

namespace MyFhirSdk.Client.Http;

/// <summary>
/// Helpers for applying standard FHIR HTTP headers.
/// </summary>
public static class FhirHttpHeaders
{
    /// <summary>
    /// Creates an Accept header for FHIR JSON.
    /// </summary>
    public static MediaTypeWithQualityHeaderValue FhirJson => new(FhirHttpConstants.FhirJsonMediaType);

    /// <summary>
    /// Adds Accept: application/fhir+json to a request.
    /// </summary>
    public static void AddFhirJsonAccept(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Accept.Add(FhirJson);
    }

    /// <summary>
    /// Adds Prefer: return=representation to a request.
    /// </summary>
    public static void AddReturnRepresentationPrefer(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.TryAddWithoutValidation(
            FhirHttpConstants.PreferHeaderName,
            FhirHttpConstants.PreferReturnRepresentation);
    }
}
