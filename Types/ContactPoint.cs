using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 ContactPoint datatype for phone, email, URL, or other contact details.
/// </summary>
public sealed class ContactPoint : DataType
{
    /// <summary>
    /// phone | fax | email | pager | url | sms | other.
    /// </summary>
    public FhirCode? System { get; set; }

    /// <summary>
    /// Contact value, such as a phone number, email address, or URL.
    /// </summary>
    public FhirString? Value { get; set; }

    /// <summary>
    /// home | work | temp | old | mobile.
    /// </summary>
    public FhirCode? Use { get; set; }

    /// <summary>
    /// Preferred order of use, where 1 is the highest preference.
    /// </summary>
    public FhirInteger? Rank { get; set; }

    /// <summary>
    /// Time period when the contact point is or was in use.
    /// </summary>
    public Period? Period { get; set; }
}
