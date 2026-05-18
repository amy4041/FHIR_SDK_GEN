using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 VirtualServiceDetail datatype for virtual service contact details.
/// </summary>
public sealed class VirtualServiceDetail : DataType
{
    /// <summary>
    /// Channel type, such as Teams, Zoom, VMR, or phone.
    /// </summary>
    public Coding? ChannelType { get; set; }

    /// <summary>
    /// Contact URL used to join the virtual service.
    /// </summary>
    public FhirUrl? AddressUrl { get; set; }

    /// <summary>
    /// Contact string used to join the virtual service.
    /// </summary>
    public FhirString? AddressString { get; set; }

    /// <summary>
    /// Contact point used to join the virtual service.
    /// </summary>
    public ContactPoint? AddressContactPoint { get; set; }

    /// <summary>
    /// Extended contact detail used to join the virtual service.
    /// </summary>
    public ExtendedContactDetail? AddressExtendedContactDetail { get; set; }

    /// <summary>
    /// URLs containing alternative connection details.
    /// </summary>
    public IList<FhirUrl> AdditionalInfo { get; set; } = new List<FhirUrl>();

    /// <summary>
    /// Maximum number of supported participants.
    /// </summary>
    public FhirPositiveInt? MaxParticipants { get; set; }

    /// <summary>
    /// Session key required by the virtual service.
    /// </summary>
    public FhirString? SessionKey { get; set; }
}
