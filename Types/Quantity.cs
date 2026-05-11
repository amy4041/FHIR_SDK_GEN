using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R4 Quantity datatype for measured or counted amounts.
/// </summary>
public sealed class Quantity : DataType
{
    /// <summary>
    /// Numerical value with implicit precision.
    /// </summary>
    public FhirDecimal? Value { get; set; }

    /// <summary>
    /// &lt; | &lt;= | &gt;= | &gt;.
    /// </summary>
    public FhirCode? Comparator { get; set; }

    /// <summary>
    /// Human-readable unit.
    /// </summary>
    public FhirString? Unit { get; set; }

    /// <summary>
    /// System that defines the coded unit form.
    /// </summary>
    public FhirUri? System { get; set; }

    /// <summary>
    /// Coded form of the unit.
    /// </summary>
    public FhirCode? Code { get; set; }
}
