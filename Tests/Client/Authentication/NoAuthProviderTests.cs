namespace MyFhirSdk.Tests.Client.Authentication;

public static class NoAuthProviderTests
{
    public static async Task ApplyAsyncDoesNotMutateAuthorizationHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/fhir/Patient/123");

        await NoAuthProvider.Instance.ApplyAsync(request).ConfigureAwait(false);

        TestAssert.IsNull(request.Headers.Authorization);
    }
}
