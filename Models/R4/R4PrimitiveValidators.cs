using MyFhirSdk.ModelContracts;
using MyFhirSdk.ModelSupport;
using Shared = MyFhirSdk.ModelSupport.PrimitiveValidators;

namespace MyFhirSdk.R4.Primitives;

// Release-specific regexes from the locked R4 primitive StructureDefinitions.
// Algorithms remain in ModelSupport; normalization never changes the wire/model literal.
internal static class R4PrimitiveValidators
{
    internal static IPrimitiveValidator Base64Binary { get; } = PrimitiveValidationPolicies.WithPattern(
        @"(\s*([0-9a-zA-Z\+/=]){4}\s*)+", Shared.Base64Binary,
        text => string.Concat(text.Where(c => !char.IsWhiteSpace(c))));
    internal static IPrimitiveValidator Code { get; } = PrimitiveValidationPolicies.WithPattern(@"[^\s]+(\s[^\s]+)*");
    internal static IPrimitiveValidator DateTime { get; } = PrimitiveValidationPolicies.WithPattern(
        @"([0-9]([0-9]([0-9][1-9]|[1-9]0)|[1-9]00)|[1-9]000)(-(0[1-9]|1[0-2])(-(0[1-9]|[1-2][0-9]|3[0-1])(T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]+)?(Z|(\+|-)((0[0-9]|1[0-3]):[0-5][0-9]|14:00)))?)?)?",
        Shared.DateTime, NormalizeFraction);
    internal static IPrimitiveValidator Decimal { get; } = PrimitiveValidationPolicies.WithPattern(
        @"-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?", useLiteral: true);
    internal static IPrimitiveValidator Instant { get; } = PrimitiveValidationPolicies.WithPattern(
        @"([0-9]([0-9]([0-9][1-9]|[1-9]0)|[1-9]00)|[1-9]000)-(0[1-9]|1[0-2])-(0[1-9]|[1-2][0-9]|3[0-1])T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]+)?(Z|(\+|-)((0[0-9]|1[0-3]):[0-5][0-9]|14:00))",
        Shared.Instant, NormalizeFraction);
    internal static IPrimitiveValidator Time { get; } = PrimitiveValidationPolicies.WithPattern(
        @"([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]+)?", Shared.Time, NormalizeFraction);

    internal static IPrimitiveValidator Boolean => Shared.Boolean;
    internal static IPrimitiveValidator Canonical => Shared.Canonical;
    internal static IPrimitiveValidator Date => Shared.Date;
    internal static IPrimitiveValidator Id => Shared.Id;
    internal static IPrimitiveValidator Integer => Shared.Integer;
    internal static IPrimitiveValidator Markdown => Shared.Markdown;
    internal static IPrimitiveValidator Oid => Shared.Oid;
    internal static IPrimitiveValidator PositiveInt => Shared.PositiveInt;
    internal static IPrimitiveValidator String => Shared.String;
    internal static IPrimitiveValidator UnsignedInt => Shared.UnsignedInt;
    internal static IPrimitiveValidator Uri => Shared.Uri;
    internal static IPrimitiveValidator Url => Shared.Url;
    internal static IPrimitiveValidator Uuid => Shared.Uuid;

    private static string NormalizeFraction(string text)
    {
        var start = text.IndexOf('.');
        if (start < 0) return text;
        var end = start + 1;
        while (end < text.Length && char.IsAsciiDigit(text[end])) end++;
        return end - start <= 10 ? text : text[..(start + 10)] + text[end..];
    }
}
