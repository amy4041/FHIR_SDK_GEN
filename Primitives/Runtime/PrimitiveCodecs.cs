using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

internal static class PrimitiveCodecs
{
    internal static IPrimitiveCodec String { get; } = new StandardPrimitiveCodec(
        JsonValueKind.String,
        element => element.GetString(),
        (writer, value) => writer.WriteStringValue((string)value));

    internal static IPrimitiveCodec Boolean { get; } = new StandardPrimitiveCodec(
        JsonValueKind.True,
        element => element.GetBoolean(),
        (writer, value) => writer.WriteBooleanValue((bool)value),
        JsonValueKind.False);

    internal static IPrimitiveCodec Integer { get; } = new StandardPrimitiveCodec(
        JsonValueKind.Number,
        element => element.GetInt32(),
        (writer, value) => writer.WriteNumberValue((int)value));

    internal static IPrimitiveCodec Decimal { get; } = new LiteralPrimitiveCodec(
        JsonValueKind.Number,
        writeAsJsonString: false);

    internal static IPrimitiveCodec Integer64 { get; } = new LiteralPrimitiveCodec(
        JsonValueKind.String,
        writeAsJsonString: true);
}

internal sealed class StandardPrimitiveCodec : IPrimitiveCodec
{
    private readonly JsonValueKind _expectedKind;
    private readonly JsonValueKind? _alternateKind;
    private readonly Func<JsonElement, object?> _read;
    private readonly Action<Utf8JsonWriter, object> _write;

    internal StandardPrimitiveCodec(
        JsonValueKind expectedKind,
        Func<JsonElement, object?> read,
        Action<Utf8JsonWriter, object> write,
        JsonValueKind? alternateKind = null)
    {
        _expectedKind = expectedKind;
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _alternateKind = alternateKind;
    }

    public object CreatePrimitive(Type primitiveType, JsonElement? rawElement)
    {
        var primitive = Activator.CreateInstance(primitiveType)
            ?? throw new FhirSdkException(
                $"Could not create an instance of '{primitiveType.FullName}'.");

        if (rawElement is null || rawElement.Value.ValueKind == JsonValueKind.Null)
        {
            return primitive;
        }

        EnsureExpectedKind(rawElement.Value);
        PrimitiveValueAccess.GetAccessor(primitive).SetUntypedValue(_read(rawElement.Value));
        return primitive;
    }

    public bool HasRawValue(object primitive)
    {
        var value = PrimitiveValueAccess.GetAccessor(primitive).UntypedValue;
        return value is not null && (value is not string text || text.Length > 0);
    }

    public void WriteRawValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var value = PrimitiveValueAccess.GetAccessor(primitive).UntypedValue;
        if (value is null || value is string { Length: 0 })
        {
            if (writeNullWhenMissing)
            {
                writer.WriteNullValue();
            }

            return;
        }

        _write(writer, value);
    }

    private void EnsureExpectedKind(JsonElement element)
    {
        if (element.ValueKind != _expectedKind &&
            element.ValueKind != _alternateKind)
        {
            throw new FhirSdkException(
                $"FHIR primitive value must be JSON {_expectedKind}.");
        }
    }
}

internal sealed class LiteralPrimitiveCodec : IPrimitiveCodec
{
    private readonly JsonValueKind _expectedKind;
    private readonly bool _writeAsJsonString;

    internal LiteralPrimitiveCodec(
        JsonValueKind expectedKind,
        bool writeAsJsonString)
    {
        _expectedKind = expectedKind;
        _writeAsJsonString = writeAsJsonString;
    }

    public object CreatePrimitive(Type primitiveType, JsonElement? rawElement)
    {
        if (rawElement is null || rawElement.Value.ValueKind == JsonValueKind.Null)
        {
            return Activator.CreateInstance(primitiveType)
                ?? throw new FhirSdkException(
                    $"Could not create an instance of '{primitiveType.FullName}'.");
        }

        if (rawElement.Value.ValueKind != _expectedKind)
        {
            throw new FhirSdkException(
                $"FHIR primitive value must be JSON {_expectedKind}.");
        }

        var literal = _expectedKind == JsonValueKind.String
            ? rawElement.Value.GetString()
            : rawElement.Value.GetRawText();

        return Activator.CreateInstance(primitiveType, literal)
            ?? throw new FhirSdkException(
                $"Could not create an instance of '{primitiveType.FullName}'.");
    }

    public bool HasRawValue(object primitive)
    {
        return !string.IsNullOrEmpty(GetLiteral(primitive)) ||
            PrimitiveValueAccess.GetAccessor(primitive).UntypedValue is not null;
    }

    public void WriteRawValue(
        Utf8JsonWriter writer,
        object primitive,
        bool writeNullWhenMissing)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var literal = GetLiteral(primitive) ?? FormatValue(primitive);
        if (string.IsNullOrEmpty(literal))
        {
            if (writeNullWhenMissing)
            {
                writer.WriteNullValue();
            }

            return;
        }

        if (_writeAsJsonString)
        {
            writer.WriteStringValue(literal);
        }
        else
        {
            writer.WriteRawValue(literal);
        }
    }

    private static string? GetLiteral(object primitive)
    {
        return primitive
            .GetType()
            .GetProperty("Literal", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(primitive) as string;
    }

    private static string? FormatValue(object primitive)
    {
        return PrimitiveValueAccess.GetAccessor(primitive).UntypedValue switch
        {
            decimal value => value.ToString(CultureInfo.InvariantCulture),
            long value => value.ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }
}
