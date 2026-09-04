namespace MyFhirSdk.Tests.Client.Authentication;

public sealed class NoAuthProviderTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task ApplyAsyncDoesNotMutateAuthorizationHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/fhir/Patient/123");

        await NoAuthProvider.Instance.ApplyAsync(request);

        Assert.Null(request.Headers.Authorization);
    }
}
