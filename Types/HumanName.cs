using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 HumanName datatype for representing names of people or animals.
/// </summary>
public sealed class HumanName : DataType
{
    /// <summary>
    /// usual | official | temp | nickname | anonymous | old | maiden.
    /// </summary>
    public FhirCode? Use { get; set; }

    /// <summary>
    /// Complete name as text.
    /// </summary>
    public FhirString? Text { get; set; }

    /// <summary>
    /// Family name, usually the last name.
    /// </summary>
    public FhirString? Family { get; set; }

    /// <summary>
    /// Given names, including first and middle names.
    /// </summary>
    public IList<FhirString> Given { get; set; } = new List<FhirString>();

    /// <summary>
    /// Prefixes such as titles or honorifics.
    /// </summary>
    public IList<FhirString> Prefix { get; set; } = new List<FhirString>();

    /// <summary>
    /// Suffixes such as degrees or generation labels.
    /// </summary>
    public IList<FhirString> Suffix { get; set; } = new List<FhirString>();

    /// <summary>
    /// Time period when the name is or was in use.
    /// </summary>
    public Period? Period { get; set; }
}
