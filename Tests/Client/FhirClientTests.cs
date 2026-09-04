namespace MyFhirSdk.Tests.Client;

public sealed class FhirClientTests
{
    [Fact]
    public async global::System.Threading.Tasks.Task ReadAsyncSendsRequestAndParsesResponse()
    {
        var patient = new Patient { Id = "123" };
        var parser = new FakeFhirParser();
        parser.AddResource(patient);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"123\"}"));
        var client = CreateClient(sender, parser, authProvider: new BearerTokenAuthProvider("token"));

        var actual = await client.ReadAsync<Patient>("123");

        Assert.Same(patient, actual!);
        Assert.Single(sender.SentRequests);
        var request = sender.SentRequests[0];
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.org/fhir/Patient/123", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ReadAsyncReturnsNullForNotFound()
    {
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.NotFound, "{\"resourceType\":\"OperationOutcome\"}"));
        var parser = new FakeFhirParser();
        var client = CreateClient(sender, parser);

        var actual = await client.ReadAsync<Patient>("missing");

        Assert.Null(actual);
        Assert.Equal(0, parser.ParseCallCount);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CreateAsyncSerializesAndSendsResource()
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

        var actual = await client.CreateAsync(resource);

        Assert.Same(created, actual);
        Assert.Equal(1, serializer.SerializeCallCount);
        Assert.Same(resource, serializer.LastResource!);
        Assert.Single(sender.SentRequests);

        var request = sender.SentRequests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://example.org/fhir/Patient", request.RequestUri!.AbsoluteUri);
        Assert.Equal(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"resourceType\":\"Patient\"}", await request.Content.ReadAsStringAsync());
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CreateAsyncDoesNotValidateWhenValidationDisabled()
    {
        var resource = new Patient { Id = "bad/id" };
        var created = new Patient { Id = "created" };
        var serializer = new FakeFhirSerializer
        {
            SerializedJson = "{\"resourceType\":\"Patient\",\"id\":\"bad/id\"}"
        };
        var parser = new FakeFhirParser();
        parser.AddResource(created);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.Created, "{\"resourceType\":\"Patient\",\"id\":\"created\"}"));
        var validator = new FakeFhirValidator
        {
            ExceptionToThrow = new InvalidOperationException("Validation should not run.")
        };
        var client = CreateClient(sender, parser, serializer, validateBeforeSend: false, validator: validator);

        var actual = await client.CreateAsync(resource);

        Assert.Same(created, actual);
        Assert.Equal(0, validator.ValidateCallCount);
        Assert.Equal(1, serializer.SerializeCallCount);
        Assert.Single(sender.SentRequests);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task CreateAsyncValidatesBeforeSendingWhenEnabled()
    {
        var resource = new Patient();
        var serializer = new FakeFhirSerializer();
        var parser = new FakeFhirParser();
        var sender = new FakeFhirHttpSender();
        var result = CreateFailedValidationResult("Patient.id");
        var validator = new FakeFhirValidator
        {
            Result = result
        };
        var client = CreateClient(sender, parser, serializer, validateBeforeSend: true, validator: validator);

        var exception = await Assert.ThrowsAsync<FhirValidationException>(() => client.CreateAsync(resource));

        Assert.Same(result, exception.Result);
        Assert.Equal(1, validator.ValidateCallCount);
        Assert.Same(resource, validator.LastResource!);
        Assert.Equal(0, serializer.SerializeCallCount);
        Assert.Empty(sender.SentRequests);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task UpdateAsyncSerializesAndSendsResource()
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

        var actual = await client.UpdateAsync(resource);

        Assert.Same(updated, actual);
        Assert.Equal(1, serializer.SerializeCallCount);
        Assert.Same(resource, serializer.LastResource!);
        Assert.Single(sender.SentRequests);

        var request = sender.SentRequests[0];
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://example.org/fhir/Patient/updated", request.RequestUri!.AbsoluteUri);
        Assert.Equal(FhirHttpConstants.FhirJsonMediaType, request.Content!.Headers.ContentType!.MediaType);
        Assert.True(
            request.Headers.TryGetValues(FhirHttpConstants.PreferHeaderName, out var values)
            && values.Contains(FhirHttpConstants.PreferReturnRepresentation),
            "Expected update request to prefer return=representation.");
        Assert.Equal(
            "{\"resourceType\":\"Patient\",\"id\":\"updated\"}",
            await request.Content.ReadAsStringAsync());
    }

    [Fact]
    public async global::System.Threading.Tasks.Task UpdateAsyncValidatesBeforeSendingWhenEnabled()
    {
        var resource = new Patient { Id = "updated" };
        var serializer = new FakeFhirSerializer();
        var parser = new FakeFhirParser();
        var sender = new FakeFhirHttpSender();
        var result = CreateFailedValidationResult("Patient.id");
        var validator = new FakeFhirValidator
        {
            Result = result
        };
        var client = CreateClient(sender, parser, serializer, validateBeforeSend: true, validator: validator);

        var exception = await Assert.ThrowsAsync<FhirValidationException>(() => client.UpdateAsync(resource));

        Assert.Same(result, exception.Result);
        Assert.Equal(1, validator.ValidateCallCount);
        Assert.Same(resource, validator.LastResource!);
        Assert.Equal(0, serializer.SerializeCallCount);
        Assert.Empty(sender.SentRequests);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task UpdateAsyncRequiresResourceId()
    {
        var resource = new Patient();
        var parser = new FakeFhirParser();
        var sender = new FakeFhirHttpSender();
        var client = CreateClient(sender, parser);

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateAsync(resource));

        Assert.Empty(sender.SentRequests);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ReadAsyncDoesNotValidateWhenValidationEnabled()
    {
        var patient = new Patient { Id = "123" };
        var parser = new FakeFhirParser();
        parser.AddResource(patient);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"123\"}"));
        var validator = new FakeFhirValidator
        {
            ExceptionToThrow = new InvalidOperationException("Read should not validate a resource body.")
        };
        var client = CreateClient(sender, parser, validateBeforeSend: true, validator: validator);

        var actual = await client.ReadAsync<Patient>("123");

        Assert.Same(patient, actual!);
        Assert.Equal(0, validator.ValidateCallCount);
        Assert.Single(sender.SentRequests);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SearchAsyncSendsStructuredSearchQuery()
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

        var actual = await client.SearchAsync<Patient>(query);

        Assert.Same(bundle, actual);
        Assert.Single(sender.SentRequests);
        Assert.Equal("https://example.org/fhir/Patient?name=John&_count=10", sender.SentRequests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SearchAsyncSendsRawSearchQuery()
    {
        var bundle = new Bundle();
        var parser = new FakeFhirParser();
        parser.AddResource(bundle);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Bundle\"}"));
        var client = CreateClient(sender, parser);

        var actual = await client.SearchAsync<Patient>("name=John");

        Assert.Same(bundle, actual);
        Assert.Single(sender.SentRequests);
        var request = sender.SentRequests[0];
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://example.org/fhir/Patient?name=John", request.RequestUri!.AbsoluteUri);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task SearchAsyncDoesNotValidateWhenValidationEnabled()
    {
        var bundle = new Bundle();
        var parser = new FakeFhirParser();
        parser.AddResource(bundle);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Bundle\"}"));
        var validator = new FakeFhirValidator
        {
            ExceptionToThrow = new InvalidOperationException("Search should not validate a resource body.")
        };
        var client = CreateClient(sender, parser, validateBeforeSend: true, validator: validator);

        var actual = await client.SearchAsync<Patient>("name=John");

        Assert.Same(bundle, actual);
        Assert.Equal(0, validator.ValidateCallCount);
        Assert.Single(sender.SentRequests);
    }

    [Fact]
    public async global::System.Threading.Tasks.Task ConstructorUsesNoAuthProviderWhenAuthProviderIsNull()
    {
        var patient = new Patient { Id = "123" };
        var parser = new FakeFhirParser();
        parser.AddResource(patient);
        var sender = new FakeFhirHttpSender();
        sender.EnqueueResponse(CreateResponse(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"123\"}"));
        var client = CreateClient(sender, parser, authProvider: null);

        var actual = await client.ReadAsync<Patient>("123");

        Assert.Same(patient, actual!);
        Assert.Single(sender.SentRequests);
        Assert.Null(sender.SentRequests[0].Headers.Authorization);
    }

    private static FhirClient CreateClient(
        FakeFhirHttpSender sender,
        FakeFhirParser parser,
        FakeFhirSerializer? serializer = null,
        IAuthProvider? authProvider = null,
        bool validateBeforeSend = false,
        IFhirValidator? validator = null)
    {
        return new FhirClient(
            serializer ?? new FakeFhirSerializer(),
            new FhirRequestBuilder(
                new FhirResourceTypeResolver(),
                new FhirRequestUriBuilder(new Uri("https://example.org/fhir"))),
            sender,
            new FhirResponseHandler(parser),
            authProvider,
            validateBeforeSend,
            validator);
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };
    }

    private static ValidationResult CreateFailedValidationResult(string path)
    {
        return new ValidationResult(new[]
        {
            new ValidationIssue
            {
                Path = path,
                Code = ValidationIssueCode.PrimitiveFormat,
                Severity = ValidationSeverity.Error,
                Message = path + " is invalid."
            }
        });
    }
}
