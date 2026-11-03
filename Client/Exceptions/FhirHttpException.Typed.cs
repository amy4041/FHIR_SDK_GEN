using System.Net;
using MyFhirSdk.Core;

namespace MyFhirSdk.Client.Exceptions;

/// <summary>An HTTP failure with a parsed version-owned OperationOutcome when one is available.</summary>
public sealed class FhirHttpException<TOperationOutcome> : FhirClientException where TOperationOutcome : Resource
{
    public FhirHttpException(FhirHttpException httpError, TOperationOutcome? operationOutcome)
        : base((httpError ?? throw new ArgumentNullException(nameof(httpError))).Message, httpError)
    {
        HttpError = httpError;
        OperationOutcome = operationOutcome;
    }
    public FhirHttpException HttpError { get; }
    public HttpStatusCode StatusCode => HttpError.StatusCode;
    public string? ReasonPhrase => HttpError.ReasonPhrase;
    public string? ResponseBody => HttpError.ResponseBody;
    public HttpMethod? Method => HttpError.Method;
    public Uri? RequestUri => HttpError.RequestUri;
    public TOperationOutcome? OperationOutcome { get; }
}
