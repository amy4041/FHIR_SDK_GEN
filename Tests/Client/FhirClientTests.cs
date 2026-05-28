namespace MyFhirSdk.Tests.Client;

public static class FhirClientTests
{
    public static async Task ReadAsyncSendsRequestAndParsesResponse()
    {
        var patient = new Patient { Id = "123" };
        var parser = new FakeFhirParser();
        parser.AddResource(patient);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"123\"}"));
        var client = CreateClient(sender, parser, authProvider: new BearerTokenAuthProvider("token"));

        var actual = await client.ReadAsync<Patient>("123").ConfigureAwait(false);

        TestAssert.AreSame(patient, actual!);
        TestAssert.AreEqual(1, sender.SentRequests.Count);
        var request = sender.SentRequests[0];
        TestAssert.AreEqual(HttpMethod.Get, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient/123", request.RequestUri!.AbsoluteUri);
        TestAssert.AreEqual("Bearer", request.Headers.Authorization!.Scheme);
        TestAssert.AreEqual("token", request.Headers.Authorization.Parameter);
    }

    public static async Task ReadAsyncReturnsNullForNotFound()
    {
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.NotFound, "{\"resourceType\":\"OperationOutcome\"}"));
        var parser = new FakeFhirParser();
        var client = CreateClient(sender, parser);

        var actual = await client.ReadAsync<Patient>("missing").ConfigureAwait(false);

        TestAssert.IsNull(actual);
        TestAssert.AreEqual(0, parser.ParseCallCount);
    }

    public static async Task CreateAsyncSerializesAndSendsResource()
    {
        var resource = new Patient();
        var created = new Patient { Id = "created" };
        var serializer = new FakeFhirSerializer
        {
            SerializedJson = "{\"resourceType\":\"Patient\"}"
        };
        var parser = new FakeFhirParser();
        parser.AddResource(created);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.Created, "{\"resourceType\":\"Patient\",\"id\":\"created\"}"));
        var client = CreateClient(sender, parser, serializer);

        var actual = await client.CreateAsync(resource).ConfigureAwait(false);

        TestAssert.AreSame(created, actual);
        TestAssert.AreEqual(1, serializer.SerializeCallCount);
        TestAssert.AreSame(resource, serializer.LastResource!);
        TestAssert.AreEqual(1, sender.SentRequests.Count);

        var request = sender.SentRequests[0];
        TestAssert.AreEqual(HttpMethod.Post, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient", request.RequestUri!.AbsoluteUri);
        TestAssert.AreEqual(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        TestAssert.AreEqual("{\"resourceType\":\"Patient\"}", await request.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static async Task UpdateAsyncSerializesAndSendsResource()
    {
        var resource = new Patient { Id = "updated" };
        var updated = new Patient { Id = "updated" };
        var serializer = new FakeFhirSerializer
        {
            SerializedJson = "{\"resourceType\":\"Patient\",\"id\":\"updated\"}"
        };
        var parser = new FakeFhirParser();
        parser.AddResource(updated);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"updated\"}"));
        var client = CreateClient(sender, parser, serializer);

        var actual = await client.UpdateAsync(resource).ConfigureAwait(false);

        TestAssert.AreSame(updated, actual);
        TestAssert.AreEqual(1, serializer.SerializeCallCount);
        TestAssert.AreSame(resource, serializer.LastResource!);
        TestAssert.AreEqual(1, sender.SentRequests.Count);

        var request = sender.SentRequests[0];
        TestAssert.AreEqual(HttpMethod.Put, request.Method);
        TestAssert.AreEqual("https://example.org/fhir/Patient/updated", request.RequestUri!.AbsoluteUri);
        TestAssert.AreEqual(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        TestAssert.IsTrue(
            request.Headers.TryGetValues(FhirHttpConstants.PreferHeaderName, out var values)
            && values.Contains(FhirHttpConstants.PreferReturnRepresentation),
            "Expected update request to prefer return=representation.");
        TestAssert.AreEqual(
            "{\"resourceType\":\"Patient\",\"id\":\"updated\"}",
            await request.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static async Task UpdateAsyncRequiresResourceId()
    {
        var resource = new Patient();
        var parser = new FakeFhirParser();
        var sender = new FakeFhirHttpSender();
        var client = CreateClient(sender, parser);

        await TestAssert.ThrowsAsync<ArgumentException>(() => client.UpdateAsync(resource)).ConfigureAwait(false);

        TestAssert.AreEqual(0, sender.SentRequests.Count);
    }

    public static async Task SearchAsyncSendsStructuredSearchQuery()
    {
        var bundle = new Bundle();
        var parser = new FakeFhirParser();
        parser.AddResource(bundle);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Bundle\"}"));
        var client = CreateClient(sender, parser);
        var query = FhirSearchQuery.Create()
            .Where("name", "John")
            .Count(10);

        var actual = await client.SearchAsync<Patient>(query).ConfigureAwait(false);

        TestAssert.AreSame(bundle, actual);
        TestAssert.AreEqual(1, sender.SentRequests.Count);
        TestAssert.AreEqual("https://example.org/fhir/Patient?name=John&_count=10", sender.SentRequests[0].RequestUri!.AbsoluteUri);
    }

    private static FhirClient CreateClient(
        FakeFhirHttpSender sender,
        FakeFhirParser parser,
        FakeFhirSerializer? serializer = null,
        IAuthProvider? authProvider = null)
    {
        return new FhirClient(
            serializer ?? new FakeFhirSerializer(),
            new FhirRequestBuilder(
                new FhirResourceTypeResolver(),
                new FhirRequestUriBuilder(new Uri("https://example.org/fhir"))),
            sender,
            new FhirResponseHandler(parser),
            authProvider);
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };
    }
}
