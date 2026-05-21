using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 Identifier datatype for business identifiers assigned to a resource.
/// </summary>
public sealed class Identifier : DataType
{
    /// <summary>
    /// usual | official | temp | secondary | old.
    /// </summary>
    public FhirCode? Use { get; set; }

    /// <summary>
    /// Description of the identifier type.
    /// </summary>
    public CodeableConcept? Type { get; set; }

    /// <summary>
    /// Namespace for the identifier value.
    /// </summary>
    public FhirUri? System { get; set; }

    /// <summary>
    /// Identifier value unique within the system.
    /// </summary>
    public FhirString? Value { get; set; }

    /// <summary>
    /// Time period when the identifier is valid.
    /// </summary>
    public Period? Period { get; set; }

    /// <summary>
    /// Organization that issued or manages the identifier.
    /// </summary>
    public Reference? Assigner { get; set; }
}
