namespace MyFhirSdk.Tests.Client.Fakes;

public sealed class FakeFhirHttpSender : IFhirHttpSender
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public IList<HttpRequestMessage> SentRequests { get; } = new List<HttpRequestMessage>();

    public void EnqueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        // The client owns/disposes requests. Keep an independent observation for assertions.
        var recorded = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers) recorded.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content is not null)
        {
            recorded.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            foreach (var header in request.Content.Headers) recorded.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        SentRequests.Add(recorded);

        if (_responses.Count > 0)
        {
            var response = _responses.Dequeue();
            response.RequestMessage ??= request;

            return response;
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
            RequestMessage = request
        };
    }
}
