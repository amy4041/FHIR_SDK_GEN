namespace MyFhirSdk.Client.Http;

/// <summary>
/// Common HTTP constants used by the FHIR client.
/// </summary>
public static class FhirHttpConstants
{
    /// <summary>
    /// FHIR JSON media type.
    /// </summary>
    public const string FhirJsonMediaType = "application/fhir+json";

    /// <summary>
    /// Prefer header name.
    /// </summary>
    public const string PreferHeaderName = "Prefer";

    /// <summary>
    /// Prefer value requesting the server representation in create/update responses.
    /// </summary>
    public const string PreferReturnRepresentation = "return=representation";
}
