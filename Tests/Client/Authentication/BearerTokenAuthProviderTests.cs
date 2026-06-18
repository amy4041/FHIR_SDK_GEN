namespace MyFhirSdk.Tests.Client.Authentication;

public sealed class BearerTokenAuthProviderTests
{
    [Fact]
    public async Task ApplyAsyncAddsAuthorizationHeader()
    {
        var provider = new BearerTokenAuthProvider("secret-token");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/fhir/Patient/123");

        await provider.ApplyAsync(request);

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void ConstructorRejectsEmptyToken()
    {
        Assert.Throws<ArgumentException>(() => new BearerTokenAuthProvider(" "));
    }
}
