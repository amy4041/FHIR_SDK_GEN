using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MyFhirSdk.Primitives;

internal static partial class PrimitiveValidators
{
    private const int MaximumStringLength = 1024 * 1024;

    internal static IPrimitiveValidator Base64Binary { get; } =
        ForValue(ValidateBase64Binary);

    internal static IPrimitiveValidator Boolean { get; } =
        ForValue(static value => value is null or bool);

    internal static IPrimitiveValidator Canonical { get; } =
        ForValue(ValidateCanonical);

    internal static IPrimitiveValidator Code { get; } =
        ForValue(ValidateCode);

    internal static IPrimitiveValidator Date { get; } =
        ForValue(ValidateDate);

    internal static IPrimitiveValidator DateTime { get; } =
        ForValue(ValidateDateTime);

    internal static IPrimitiveValidator Decimal { get; } =
        ForLiteralOrValue(ValidateDecimal);

    internal static IPrimitiveValidator Id { get; } =
        ForValue(ValidateId);

    internal static IPrimitiveValidator Instant { get; } =
        ForValue(ValidateInstant);

    internal static IPrimitiveValidator Integer { get; } =
        ForValue(static value => value is null or int);

    internal static IPrimitiveValidator Integer64 { get; } =
        ForLiteralOrValue(ValidateInteger64);

    internal static IPrimitiveValidator Markdown { get; } =
        ForValue(ValidateStringLike);

    internal static IPrimitiveValidator Oid { get; } =
        ForValue(ValidateOid);

    internal static IPrimitiveValidator PositiveInt { get; } =
        ForValue(static value => value is null || value is int and > 0);

    internal static IPrimitiveValidator String { get; } =
        ForValue(ValidateStringLike);

    internal static IPrimitiveValidator Time { get; } =
        ForValue(ValidateTime);

    internal static IPrimitiveValidator UnsignedInt { get; } =
        ForValue(static value => value is null || value is int and >= 0);

    internal static IPrimitiveValidator Uri { get; } =
        ForValue(ValidateUri);

    internal static IPrimitiveValidator Url { get; } =
        ForValue(ValidateUrl);

    internal static IPrimitiveValidator Uuid { get; } =
        ForValue(ValidateUuid);

    private static IPrimitiveValidator ForValue(Func<object?, bool> validate)
    {
        return new PrimitiveValidator(
            primitive => PrimitiveValueAccess.GetAccessor(primitive).UntypedValue,
            validate);
    }

    private static IPrimitiveValidator ForLiteralOrValue(
        Func<object?, bool> validate)
    {
        return new PrimitiveValidator(GetLiteralOrValue, validate);
    }

    private static object? GetLiteralOrValue(object primitive)
    {
        var literal = primitive
            .GetType()
            .GetProperty("Literal", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(primitive) as string;

        return literal ?? PrimitiveValueAccess.GetAccessor(primitive).UntypedValue;
    }

    private static bool ValidateBase64Binary(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text &&
            Base64BinaryRegex().IsMatch(text) &&
            Convert.TryFromBase64String(text, new byte[text.Length], out _);
    }

    private static bool ValidateCanonical(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text || !NoWhitespaceRegex().IsMatch(text))
        {
            return false;
        }

        var uriPart = text.Split('|', 2)[0];
        return uriPart.Length == 0 ||
            uriPart.StartsWith('#') ||
            System.Uri.TryCreate(uriPart, UriKind.Absolute, out _);
    }

    private static bool ValidateCode(object? value)
    {
        return value is null ||
            value is string text && CodeRegex().IsMatch(text);
    }

    private static bool ValidateDate(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text ||
            !DateRegex().IsMatch(text) ||
            text.StartsWith("0000", StringComparison.Ordinal))
        {
            return false;
        }

        return ValidatePartialDate(text);
    }

    private static bool ValidateDateTime(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text ||
            !DateTimeRegex().IsMatch(text) ||
            text.StartsWith("0000", StringComparison.Ordinal))
        {
            return false;
        }

        return !text.Contains('T')
            ? ValidatePartialDate(text)
            : TryParseDateTimeOffsetAllowingLeapSecond(text);
    }

    private static bool ValidateDecimal(object? value)
    {
        var literal = value switch
        {
            string text => text,
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            null => null,
            _ => string.Empty
        };

        return literal is null || DecimalRegex().IsMatch(literal);
    }

    private static bool ValidateId(object? value)
    {
        return value is null ||
            value is string text && IdRegex().IsMatch(text);
    }

    private static bool ValidateInstant(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text &&
            InstantRegex().IsMatch(text) &&
            TryParseDateTimeOffsetAllowingLeapSecond(text);
    }

    private static bool ValidateInteger64(object? value)
    {
        var literal = value switch
        {
            string text => text,
            long number => number.ToString(CultureInfo.InvariantCulture),
            null => null,
            _ => string.Empty
        };

        return literal is null ||
            Integer64Regex().IsMatch(literal) &&
            long.TryParse(
                literal,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _);
    }

    private static bool ValidateOid(object? value)
    {
        return value is null ||
            value is string text && OidRegex().IsMatch(text);
    }

    private static bool ValidateStringLike(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text ||
            text.Length is 0 or > MaximumStringLength)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (character < 32 &&
                character is not '\t' and not '\r' and not '\n')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateTime(object? value)
    {
        return value is null ||
            value is string text && TimeRegex().IsMatch(text);
    }

    private static bool ValidateUri(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text &&
            NoWhitespaceRegex().IsMatch(text) &&
            (text.Length == 0 ||
                System.Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out _));
    }

    private static bool ValidateUrl(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text &&
            NoWhitespaceRegex().IsMatch(text) &&
            (text.Length == 0 ||
                System.Uri.TryCreate(text, UriKind.Absolute, out _));
    }

    private static bool ValidateUuid(object? value)
    {
        return value is null ||
            value is string text && UuidRegex().IsMatch(text);
    }

    private static bool ValidatePartialDate(string value)
    {
        return value.Length switch
        {
            4 => true,
            7 => DateOnly.TryParseExact(
                value + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _),
            10 => DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _),
            _ => false
        };
    }

    private static bool TryParseDateTimeOffsetAllowingLeapSecond(string value)
    {
        return DateTimeOffset.TryParse(
            NormalizeForDotNetDateTimeOffset(value),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }

    private static string NormalizeForDotNetDateTimeOffset(string value)
    {
        var normalized = value.Contains(":60", StringComparison.Ordinal)
            ? value.Replace(":60", ":59", StringComparison.Ordinal)
            : value;

        var fractionStart = normalized.IndexOf('.', StringComparison.Ordinal);
        if (fractionStart < 0)
        {
            return normalized;
        }

        var fractionEnd = normalized.IndexOfAny(['Z', '+', '-'], fractionStart);
        if (fractionEnd < 0 || fractionEnd - fractionStart <= 8)
        {
            return normalized;
        }

        return normalized[..(fractionStart + 8)] + normalized[fractionEnd..];
    }

    [GeneratedRegex(@"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$")]
    private static partial Regex Base64BinaryRegex();

    [GeneratedRegex(@"^[^\s]+( [^\s]+)*$")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[0-9]{4}(-(0[1-9]|1[0-2])(-(0[1-9]|[12][0-9]|3[01]))?)?$")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^[0-9]{4}(-(0[1-9]|1[0-2])(-(0[1-9]|[12][0-9]|3[01])(T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]{1,9})?(Z|[+-]((0[0-9]|1[0-3]):[0-5][0-9]|14:00)))?)?)?$")]
    private static partial Regex DateTimeRegex();

    [GeneratedRegex(@"^-?(0|[1-9][0-9]{0,17})(\.[0-9]{1,17})?([eE][+-]?[0-9]{1,9})?$")]
    private static partial Regex DecimalRegex();

    [GeneratedRegex(@"^[A-Za-z0-9\-\.]{1,64}$")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]{1,9})?(Z|[+-]((0[0-9]|1[0-3]):[0-5][0-9]|14:00))$")]
    private static partial Regex InstantRegex();

    [GeneratedRegex(@"^[0]|[-+]?[1-9][0-9]*$")]
    private static partial Regex Integer64Regex();

    [GeneratedRegex(@"^urn:oid:[0-2](\.(0|[1-9][0-9]*))+$")]
    private static partial Regex OidRegex();

    [GeneratedRegex(@"^([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]{1,9})?$")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"^\S*$")]
    private static partial Regex NoWhitespaceRegex();

    [GeneratedRegex(@"^urn:uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex UuidRegex();
}

internal sealed class PrimitiveValidator : IPrimitiveValidator
{
    private readonly Func<object, object?> _selectValue;
    private readonly Func<object?, bool> _validate;

    internal PrimitiveValidator(
        Func<object, object?> selectValue,
        Func<object?, bool> validate)
    {
        _selectValue = selectValue ??
            throw new ArgumentNullException(nameof(selectValue));
        _validate = validate ?? throw new ArgumentNullException(nameof(validate));
    }

    public bool IsValid(object primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        return _validate(_selectValue(primitive));
    }

    public bool IsValidValue(object? value)
    {
        return _validate(value);
    }
}
