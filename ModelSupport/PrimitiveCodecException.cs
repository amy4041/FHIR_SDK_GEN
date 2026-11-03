namespace MyFhirSdk.ModelSupport;

/// <summary>A primitive JSON conversion failure, independent of SDK engines.</summary>
public sealed class PrimitiveCodecException : FormatException
{
    public PrimitiveCodecException(string message) : base(message) { }
    public PrimitiveCodecException(string message, Exception innerException) : base(message, innerException) { }
}
