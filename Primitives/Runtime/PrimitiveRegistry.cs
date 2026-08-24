using System.Collections.ObjectModel;

namespace MyFhirSdk.Primitives;

internal sealed partial class PrimitiveRegistry
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

    internal static PrimitiveRegistry Default { get; } = Create(CreateDefinitions());

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

    private static IPrimitiveDefinition[] CreateDefinitions()
    {
        var definitions = new List<IPrimitiveDefinition>();
        AddGeneratedDefinitions(definitions);

        if (definitions.Count == 0)
        {
            AddHandwrittenDefinitions(definitions);
        }

        return definitions.ToArray();
    }

    static partial void AddGeneratedDefinitions(
        List<IPrimitiveDefinition> definitions);

    private static void AddHandwrittenDefinitions(
        List<IPrimitiveDefinition> definitions)
    {
        definitions.AddRange(
        [
            Define<FhirBase64Binary, string>(
                "base64Binary",
                PrimitiveCodecs.String,
                PrimitiveValidators.Base64Binary),
            Define<FhirBoolean, bool?>(
                "boolean",
                PrimitiveCodecs.Boolean,
                PrimitiveValidators.Boolean),
            Define<FhirCanonical, string>(
                "canonical",
                PrimitiveCodecs.String,
                PrimitiveValidators.Canonical),
            Define<FhirCode, string>(
                "code",
                PrimitiveCodecs.String,
                PrimitiveValidators.Code),
            Define<FhirDate, string>(
                "date",
                PrimitiveCodecs.String,
                PrimitiveValidators.Date),
            Define<FhirDateTime, string>(
                "dateTime",
                PrimitiveCodecs.String,
                PrimitiveValidators.DateTime),
            Define<FhirDecimal, decimal?>(
                "decimal",
                PrimitiveCodecs.Decimal,
                PrimitiveValidators.Decimal),
            Define<FhirId, string>(
                "id",
                PrimitiveCodecs.String,
                PrimitiveValidators.Id),
            Define<FhirInstant, string>(
                "instant",
                PrimitiveCodecs.String,
                PrimitiveValidators.Instant),
            Define<FhirInteger, int?>(
                "integer",
                PrimitiveCodecs.Integer,
                PrimitiveValidators.Integer),
            Define<FhirInteger64, long?>(
                "integer64",
                PrimitiveCodecs.Integer64,
                PrimitiveValidators.Integer64),
            Define<FhirMarkdown, string>(
                "markdown",
                PrimitiveCodecs.String,
                PrimitiveValidators.Markdown),
            Define<FhirPositiveInt, int?>(
                "positiveInt",
                PrimitiveCodecs.Integer,
                PrimitiveValidators.PositiveInt),
            Define<FhirString, string>(
                "string",
                PrimitiveCodecs.String,
                PrimitiveValidators.String),
            Define<FhirUnsignedInt, int?>(
                "unsignedInt",
                PrimitiveCodecs.Integer,
                PrimitiveValidators.UnsignedInt),
            Define<FhirUri, string>(
                "uri",
                PrimitiveCodecs.String,
                PrimitiveValidators.Uri),
            Define<FhirUrl, string>(
                "url",
                PrimitiveCodecs.String,
                PrimitiveValidators.Url)
        ]);
    }

    private static IPrimitiveDefinition Define<TPrimitive, TValue>(
        string fhirTypeName,
        IPrimitiveCodec codec,
        IPrimitiveValidator validator)
    {
        return new PrimitiveDefinition(
            fhirTypeName,
            typeof(TPrimitive),
            typeof(TValue),
            codec,
            validator);
    }
}
