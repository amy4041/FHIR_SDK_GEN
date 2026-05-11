using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Types;

namespace MyFhirSdk.Resources;

/// <summary>
/// FHIR R4 Bundle resource for a collection of resources.
/// </summary>
public sealed class Bundle : Resource
{
    /// <inheritdoc />
    [JsonPropertyName("resourceType")]
    public override string ResourceType => "Bundle";

    /// <summary>
    /// Persistent identifier for this bundle.
    /// </summary>
    public Identifier? Identifier { get; set; }

    /// <summary>
    /// document | message | transaction | transaction-response | batch | batch-response | history | searchset | collection.
    /// </summary>
    public FhirCode? Type { get; set; }

    /// <summary>
    /// Time when this bundle was assembled.
    /// </summary>
    public FhirInstant? Timestamp { get; set; }

    /// <summary>
    /// Total number of matching resources when this is a searchset.
    /// </summary>
    public FhirInteger? Total { get; set; }

    /// <summary>
    /// Links related to this bundle.
    /// </summary>
    public IList<BundleLink> Link { get; set; } = new List<BundleLink>();

    /// <summary>
    /// Entries contained in this bundle.
    /// </summary>
    public IList<BundleEntry> Entry { get; set; } = new List<BundleEntry>();
}
