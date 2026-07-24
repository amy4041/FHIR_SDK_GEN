using System.Collections.Generic;

namespace MyFhirSdk.Core;

/// <summary>
/// Base type for FHIR datatypes that can carry modifier extensions.
/// </summary>
public abstract class BackboneType : DataType
{
    /// <summary>
    /// Extensions that cannot be ignored even if unrecognized.
    /// </summary>
    public IList<Extension> ModifierExtension { get; set; } = new List<Extension>();
}
