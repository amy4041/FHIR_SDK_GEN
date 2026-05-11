using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R4 Reference datatype for pointing to another resource or logical identifier.
/// </summary>
public sealed class Reference : DataType
{
    /// <summary>
    /// Literal reference, relative, internal, or absolute URL.
    /// </summary>
    [JsonPropertyName("reference")]
    public FhirString? ReferenceValue { get; set; }

    /// <summary>
    /// Expected type of the referenced resource.
    /// </summary>
    public FhirUri? Type { get; set; }

    /// <summary>
    /// Logical reference when a literal reference is not available.
    /// </summary>
    public Identifier? Identifier { get; set; }

    /// <summary>
    /// Text alternative for the reference.
    /// </summary>
    public FhirString? Display { get; set; }
}
