using System.Collections.Generic;

namespace MyFhirSdk.Core;

/// <summary>
/// Base type for complex elements defined as part of a resource definition.
/// </summary>
public abstract class BackboneElement : Element
{
    /// <summary>
    /// Extensions that cannot be ignored even if unrecognized.
    /// </summary>
    public IList<Extension> ModifierExtension { get; set; } = new List<Extension>();
}
