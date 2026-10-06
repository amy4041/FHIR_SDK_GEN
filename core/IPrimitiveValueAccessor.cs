namespace MyFhirSdk.Core;

/// <summary>Provides untyped primitive value access across assembly boundaries.</summary>
/// <remarks>
/// This is a public integration contract. Implementing it alone does not register a
/// FHIR primitive or provide parser, serializer, or validator support.
/// </remarks>
public interface IPrimitiveValueAccessor
{
    /// <summary>Gets the primitive value, or null when its value type permits an absent value.</summary>
    object? UntypedValue { get; }

    /// <summary>Gets the declared value type, preserving nullable value types.</summary>
    Type ValueType { get; }

    /// <summary>Writes a type-compatible value without parsing, conversion, or format validation.</summary>
    /// <param name="value">The value to assign. Null is supported for reference and nullable value types.</param>
    /// <remarks>
    /// PrimitiveType implementations use a direct cast to their declared value type.
    /// Failed assignments preserve the previous value; assignments do not clear element metadata.
    /// </remarks>
    /// <exception cref="InvalidCastException">The value has an incompatible type.</exception>
    /// <exception cref="NullReferenceException">Null is assigned to a non-nullable value type.</exception>
    void SetUntypedValue(object? value);
}
