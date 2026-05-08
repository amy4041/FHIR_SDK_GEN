using System.Collections.Generic;

namespace MyFhirSdk.Core;

/// <summary>
/// Base type for all elements contained inside a resource.
/// </summary>
public abstract class Element : Base
{
    /// <summary>
    /// Unique id for inter-element references within the containing resource.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Additional content defined by implementations.
    /// </summary>
    public IList<Extension> Extension { get; set; } = new List<Extension>();
}
