using MyFhirSdk.Core;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 CodeableReference datatype for representing either a concept or a resource reference.
/// </summary>
public sealed class CodeableReference : DataType
{
    /// <summary>
    /// Reference to a concept by class.
    /// </summary>
    public CodeableConcept? Concept { get; set; }

    /// <summary>
    /// Reference to a resource by instance.
    /// </summary>
    public Reference? Reference { get; set; }
}
