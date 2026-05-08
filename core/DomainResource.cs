using System.Collections.Generic;

namespace MyFhirSdk.Core;

/// <summary>
/// Base type for FHIR resources with narrative, contained resources, and extensions.
/// </summary>
public abstract class DomainResource : Resource
{
    /// <summary>
    /// Text summary of the resource, for human interpretation.
    /// </summary>
    public Narrative? Text { get; set; }

    /// <summary>
    /// Contained inline resources.
    /// </summary>
    public IList<Resource> Contained { get; set; } = new List<Resource>();

    /// <summary>
    /// Additional content defined by implementations.
    /// </summary>
    public IList<Extension> Extension { get; set; } = new List<Extension>();

    /// <summary>
    /// Extensions that cannot be ignored.
    /// </summary>
    public IList<Extension> ModifierExtension { get; set; } = new List<Extension>();
}
