using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 Money datatype for an amount of economic utility in a recognized currency.
/// </summary>
public sealed class Money : DataType
{
    /// <summary>
    /// Numerical value with implicit precision.
    /// </summary>
    public FhirDecimal? Value { get; set; }

    /// <summary>
    /// ISO 4217 currency code.
    /// </summary>
    public FhirCode? Currency { get; set; }
}
