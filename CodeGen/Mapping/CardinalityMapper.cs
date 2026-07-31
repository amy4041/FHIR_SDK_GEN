using System.Diagnostics.CodeAnalysis;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class CardinalityMapper
{
    public bool TryMap(
        int? min,
        string? max,
        [NotNullWhen(true)] out CardinalityModel? mapping)
    {
        mapping = null;

        if (min is not (0 or 1) ||
            max is not ("1" or "*"))
        {
            return false;
        }

        mapping = new CardinalityModel(
            min.Value,
            max,
            isCollection: max == "*",
            isRequired: min == 1);
        return true;
    }
}
