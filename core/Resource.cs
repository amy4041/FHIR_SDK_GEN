namespace MyFhirSdk.Core;

/// <summary>
/// Base type for FHIR resources.
/// </summary>
public abstract class Resource : Base
{
    /// <summary>
    /// Logical id of this resource.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Metadata about the resource.
    /// </summary>
    public Meta? Meta { get; set; }

    /// <summary>
    /// Rules under which this content was created.
    /// </summary>
    public string? ImplicitRules { get; set; }

    /// <summary>
    /// Base language of the resource content.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// FHIR resource type name emitted as resourceType in JSON.
    /// </summary>
    public abstract string ResourceType { get; }
}
