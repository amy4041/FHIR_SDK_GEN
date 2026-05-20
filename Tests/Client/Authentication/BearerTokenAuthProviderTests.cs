namespace MyFhirSdk.Tests.Client.Authentication;

public static class BearerTokenAuthProviderTests
{
    public static async Task ApplyAsyncAddsAuthorizationHeader()
    {
        var provider = new BearerTokenAuthProvider("secret-token");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/fhir/Patient/123");

        await provider.ApplyAsync(request).ConfigureAwait(false);

        TestAssert.IsNotNull(request.Headers.Authorization);
        TestAssert.AreEqual("Bearer", request.Headers.Authorization!.Scheme);
        TestAssert.AreEqual("secret-token", request.Headers.Authorization.Parameter);
    }

    public static void ConstructorRejectsEmptyToken()
    {
        TestAssert.Throws<ArgumentException>(() => new BearerTokenAuthProvider(" "));
    }
}
