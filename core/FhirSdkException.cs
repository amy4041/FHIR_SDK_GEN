using System;

namespace MyFhirSdk.Core;

/// <summary>
/// Base exception type for SDK-specific failures.
/// </summary>
public class FhirSdkException : Exception
{
    public FhirSdkException()
    {
    }

    public FhirSdkException(string message)
        : base(message)
    {
    }

    public FhirSdkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
