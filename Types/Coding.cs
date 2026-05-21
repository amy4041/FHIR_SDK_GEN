using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 Coding datatype for a code defined by a terminology system.
/// </summary>
public sealed class Coding : DataType
{
    /// <summary>
    /// Identity of the terminology system.
    /// </summary>
    public FhirUri? System { get; set; }

    /// <summary>
    /// Version of the terminology system.
    /// </summary>
    public FhirString? Version { get; set; }

    /// <summary>
    /// Symbol in syntax defined by the system.
    /// </summary>
    public FhirCode? Code { get; set; }

    /// <summary>
    /// Representation defined by the system.
    /// </summary>
    public FhirString? Display { get; set; }

    /// <summary>
    /// Whether this coding was chosen directly by the user.
    /// </summary>
    public FhirBoolean? UserSelected { get; set; }
}
