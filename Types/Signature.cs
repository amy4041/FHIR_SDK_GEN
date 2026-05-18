using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 Signature datatype for digital or electronic signatures.
/// </summary>
public sealed class Signature : DataType
{
    /// <summary>
    /// Indication of the reason the entity signed the object.
    /// </summary>
    public IList<Coding> Type { get; set; } = new List<Coding>();

    /// <summary>
    /// When the signature was created.
    /// </summary>
    public FhirInstant? When { get; set; }

    /// <summary>
    /// Who signed.
    /// </summary>
    public Reference? Who { get; set; }

    /// <summary>
    /// The party represented.
    /// </summary>
    public Reference? OnBehalfOf { get; set; }

    /// <summary>
    /// Technical format of the signed resources.
    /// </summary>
    public FhirCode? TargetFormat { get; set; }

    /// <summary>
    /// Technical format of the signature.
    /// </summary>
    public FhirCode? SigFormat { get; set; }

    /// <summary>
    /// Actual signature content.
    /// </summary>
    public FhirBase64Binary? Data { get; set; }
}
