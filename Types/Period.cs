using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R4 Period datatype for a start and end date/time.
/// </summary>
public sealed class Period : DataType
{
    /// <summary>
    /// Start time with inclusive boundary.
    /// </summary>
    public FhirDateTime? Start { get; set; }

    /// <summary>
    /// End time with inclusive boundary, if not ongoing.
    /// </summary>
    public FhirDateTime? End { get; set; }
}
