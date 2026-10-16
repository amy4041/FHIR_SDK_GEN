namespace MyFhirSdk.Primitives;

internal static class PrimitiveCodecs
{
    internal static IPrimitiveCodec String { get; } = new CodecAdapter(ModelSupport.PrimitiveCodecs.String);
    internal static IPrimitiveCodec Boolean { get; } = new CodecAdapter(ModelSupport.PrimitiveCodecs.Boolean);
    internal static IPrimitiveCodec Integer { get; } = new CodecAdapter(ModelSupport.PrimitiveCodecs.Integer);
    internal static IPrimitiveCodec Decimal { get; } = new CodecAdapter(ModelSupport.PrimitiveCodecs.Decimal);
    internal static IPrimitiveCodec Integer64 { get; } = new CodecAdapter(ModelSupport.PrimitiveCodecs.Integer64);

    private sealed class CodecAdapter(ModelContracts.IPrimitiveCodec inner) : IPrimitiveCodec
    {
        public object CreatePrimitive(Type type, System.Text.Json.JsonElement? raw)
        {
            try { return inner.CreatePrimitive(type, raw); }
            catch (ModelSupport.PrimitiveCodecException error) { throw new Core.FhirSdkException(error.Message, error); }
        }
        public bool HasRawValue(object value) => inner.HasRawValue(value);
        public void WriteRawValue(System.Text.Json.Utf8JsonWriter writer, object value, bool missing) => inner.WriteRawValue(writer, value, missing);
    }
}
