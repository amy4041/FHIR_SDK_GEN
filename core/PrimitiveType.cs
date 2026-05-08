namespace MyFhirSdk.Core;

/// <summary>
/// Base type for FHIR primitive datatypes that carry a simple value.
/// </summary>
/// <typeparam name="T">The underlying .NET value type used by the primitive wrapper.</typeparam>
public abstract class PrimitiveType<T> : DataType
{
    protected PrimitiveType()
    {
    }

    protected PrimitiveType(T? value)
    {
        Value = value;
    }

    /// <summary>
    /// Raw primitive value.
    /// </summary>
    public T? Value { get; set; }

    public bool HasValue => Value is not null;

    public override string ToString()
    {
        return Value?.ToString() ?? string.Empty;
    }
}
