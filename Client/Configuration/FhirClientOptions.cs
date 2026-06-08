namespace MyFhirSdk.Client.Configuration;

/// <summary>
/// Runtime options for the FHIR client.
/// </summary>
public sealed class FhirClientOptions
{
    /// <summary>
    /// Base FHIR endpoint, such as https://server.example.org/fhir.
    /// </summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>
    /// HTTP timeout applied to the supplied HttpClient.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether create and update requests validate the resource before sending HTTP.
    /// </summary>
    public bool ValidateBeforeSend { get; init; } = false;
}
