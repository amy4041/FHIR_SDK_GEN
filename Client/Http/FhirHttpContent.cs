using System.Net.Http.Headers;
using System.Text;

namespace MyFhirSdk.Client.Http;

/// <summary>
/// Helpers for creating FHIR HTTP content.
/// </summary>
public static class FhirHttpContent
{
    /// <summary>
    /// Creates HTTP content for a FHIR JSON payload.
    /// </summary>
    public static HttpContent CreateJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(FhirHttpConstants.FhirJsonMediaType);

        return content;
    }
}
