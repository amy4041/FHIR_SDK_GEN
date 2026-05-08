using System;
using System.Collections.Generic;

namespace MyFhirSdk.Core;

/// <summary>
/// Metadata about a resource maintained by the infrastructure.
/// </summary>
public sealed class Meta : DataType
{
    /// <summary>
    /// Version specific identifier.
    /// </summary>
    public string? VersionId { get; set; }

    /// <summary>
    /// When the resource version last changed.
    /// </summary>
    public DateTimeOffset? LastUpdated { get; set; }

    /// <summary>
    /// Identifies where the resource comes from.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Profiles this resource claims to conform to.
    /// </summary>
    public IList<string> Profile { get; set; } = new List<string>();

    /// <summary>
    /// Security labels applied to this resource.
    /// </summary>
    public IList<DataType> Security { get; set; } = new List<DataType>();

    /// <summary>
    /// Tags applied to this resource.
    /// </summary>
    public IList<DataType> Tag { get; set; } = new List<DataType>();
}
