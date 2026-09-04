namespace MyFhirSdk.Tests.Client.Fakes;

public sealed class FakeFhirHttpSender : IFhirHttpSender
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public IList<HttpRequestMessage> SentRequests { get; } = new List<HttpRequestMessage>();

    public void EnqueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);

        if (_responses.Count > 0)
        {
            var response = _responses.Dequeue();
            response.RequestMessage ??= request;

            return System.Threading.Tasks.Task.FromResult(response);
        }

        return System.Threading.Tasks.Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
            RequestMessage = request
        });
    }
}
