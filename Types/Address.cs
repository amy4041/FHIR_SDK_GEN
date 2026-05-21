using System.Collections.Generic;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Types;

/// <summary>
/// FHIR R5 Address datatype for postal and physical addresses.
/// </summary>
public sealed class Address : DataType
{
    /// <summary>
    /// home | work | temp | old | billing.
    /// </summary>
    public FhirCode? Use { get; set; }

    /// <summary>
    /// postal | physical | both.
    /// </summary>
    public FhirCode? Type { get; set; }

    /// <summary>
    /// Full address as text.
    /// </summary>
    public FhirString? Text { get; set; }

    /// <summary>
    /// Street name, number, direction, P.O. Box, or similar delivery details.
    /// </summary>
    public IList<FhirString> Line { get; set; } = new List<FhirString>();

    /// <summary>
    /// City, town, village, or other community name.
    /// </summary>
    public FhirString? City { get; set; }

    /// <summary>
    /// District, county, or parish.
    /// </summary>
    public FhirString? District { get; set; }

    /// <summary>
    /// Sub-unit of a country, such as state, province, or region.
    /// </summary>
    public FhirString? State { get; set; }

    /// <summary>
    /// Postal code for the address.
    /// </summary>
    public FhirString? PostalCode { get; set; }

    /// <summary>
    /// Country name.
    /// </summary>
    public FhirString? Country { get; set; }

    /// <summary>
    /// Time period when the address is or was in use.
    /// </summary>
    public Period? Period { get; set; }
}
