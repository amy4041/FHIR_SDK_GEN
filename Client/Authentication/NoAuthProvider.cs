namespace MyFhirSdk.Client.Authentication;

/// <summary>
/// Authentication provider for public or unauthenticated FHIR endpoints.
/// </summary>
public sealed class NoAuthProvider : IAuthProvider
{
    /// <summary>
    /// Shared no-op provider instance.
    /// </summary>
    public static NoAuthProvider Instance { get; } = new();

    /// <inheritdoc />
    public Task ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.CompletedTask;
    }
}
