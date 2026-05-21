using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 CodeableConcept datatype for a concept represented by codings and text.
/// </summary>
public sealed class CodeableConcept : DataType
{
    /// <summary>
    /// Codes defined by one or more terminology systems.
    /// </summary>
    public IList<Coding> Coding { get; set; } = new List<Coding>();

    /// <summary>
    /// Plain-text representation of the concept.
    /// </summary>
    public FhirString? Text { get; set; }
}
