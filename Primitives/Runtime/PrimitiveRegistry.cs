using System.Collections.ObjectModel;

namespace MyFhirSdk.Primitives;

internal sealed class PrimitiveRegistry
{
    private readonly IReadOnlyDictionary<string, IPrimitiveDefinition> _byFhirTypeName;
    private readonly IReadOnlyDictionary<Type, IPrimitiveDefinition> _byPrimitiveType;

    private PrimitiveRegistry(IReadOnlyList<IPrimitiveDefinition> definitions)
    {
        Definitions = definitions;
        _byFhirTypeName = new ReadOnlyDictionary<string, IPrimitiveDefinition>(
            definitions.ToDictionary(
                definition => definition.FhirTypeName,
                StringComparer.Ordinal));
        _byPrimitiveType = new ReadOnlyDictionary<Type, IPrimitiveDefinition>(
            definitions.ToDictionary(definition => definition.PrimitiveType));
    }

    internal static PrimitiveRegistry Default { get; } = Create(
        ModelMetadata.R5.R5ModelProvider.Primitives.Definitions.Select(p =>
            new PrimitiveDefinition(p.FhirTypeName, p.PrimitiveType, p.ValueType, new CodecAdapter(p.Codec), new ValidatorAdapter(p.Validator))));

    internal IReadOnlyList<IPrimitiveDefinition> Definitions { get; }

    internal static PrimitiveRegistry Create(
        IEnumerable<IPrimitiveDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var materialized = definitions
            .Select(definition => definition ??
                throw new ArgumentException(
                    "Primitive definitions cannot contain null entries.",
                    nameof(definitions)))
            .OrderBy(definition => definition.FhirTypeName, StringComparer.Ordinal)
            .ToArray();

        var duplicateFhirTypeName = materialized
            .GroupBy(
                definition => definition.FhirTypeName,
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateFhirTypeName is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate FHIR primitive type name " +
                $"'{duplicateFhirTypeName.Key}'.");
        }

        var duplicatePrimitiveType = materialized
            .GroupBy(definition => definition.PrimitiveType)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePrimitiveType is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate primitive wrapper type " +
                $"'{duplicatePrimitiveType.Key.FullName}'.");
        }

        return new PrimitiveRegistry(materialized);
    }

    internal IPrimitiveDefinition GetRequired(string fhirTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fhirTypeName);

        return _byFhirTypeName.TryGetValue(fhirTypeName, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"FHIR primitive type '{fhirTypeName}' is not registered.");
    }

    internal IPrimitiveDefinition GetRequired(Type primitiveType)
    {
        ArgumentNullException.ThrowIfNull(primitiveType);

        return _byPrimitiveType.TryGetValue(primitiveType, out var definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Primitive wrapper type '{primitiveType.FullName}' is not registered.");
    }

    private sealed class CodecAdapter(ModelContracts.IPrimitiveCodec codec) : IPrimitiveCodec
    {
        public object CreatePrimitive(Type type, System.Text.Json.JsonElement? raw)
        {
            try { return codec.CreatePrimitive(type, raw); }
            catch (ModelSupport.PrimitiveCodecException error) { throw new Core.FhirSdkException(error.Message, error); }
        }
        public bool HasRawValue(object value) => codec.HasRawValue(value);
        public void WriteRawValue(System.Text.Json.Utf8JsonWriter writer, object value, bool missing) => codec.WriteRawValue(writer, value, missing);
    }
    private sealed class ValidatorAdapter(ModelContracts.IPrimitiveValidator validator) : IPrimitiveValidator
    {
        public bool IsValid(object value) => validator.IsValid(value);
        public bool IsValidValue(object? value) => validator.IsValidValue(value);
    }
}
