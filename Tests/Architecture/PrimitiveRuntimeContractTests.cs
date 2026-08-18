using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PrimitiveRuntimeContractTests
{
    private static readonly IReadOnlyDictionary<Type, (string FhirTypeName, Type ValueType)>
        ExpectedDefinitions =
            new Dictionary<Type, (string, Type)>
            {
                [typeof(FhirBase64Binary)] = ("base64Binary", typeof(string)),
                [typeof(FhirBoolean)] = ("boolean", typeof(bool?)),
                [typeof(FhirCanonical)] = ("canonical", typeof(string)),
                [typeof(FhirCode)] = ("code", typeof(string)),
                [typeof(FhirDate)] = ("date", typeof(string)),
                [typeof(FhirDateTime)] = ("dateTime", typeof(string)),
                [typeof(FhirDecimal)] = ("decimal", typeof(decimal?)),
                [typeof(FhirId)] = ("id", typeof(string)),
                [typeof(FhirInstant)] = ("instant", typeof(string)),
                [typeof(FhirInteger)] = ("integer", typeof(int?)),
                [typeof(FhirInteger64)] = ("integer64", typeof(long?)),
                [typeof(FhirMarkdown)] = ("markdown", typeof(string)),
                [typeof(FhirPositiveInt)] = ("positiveInt", typeof(int?)),
                [typeof(FhirString)] = ("string", typeof(string)),
                [typeof(FhirUnsignedInt)] = ("unsignedInt", typeof(int?)),
                [typeof(FhirUri)] = ("uri", typeof(string)),
                [typeof(FhirUrl)] = ("url", typeof(string))
            };

    public static TheoryData<Type, string> CodecCases => new()
    {
        { typeof(FhirBase64Binary), "\"QQ==\"" },
        { typeof(FhirBoolean), "true" },
        { typeof(FhirCanonical), "\"https://example.test/StructureDefinition/x\"" },
        { typeof(FhirCode), "\"active\"" },
        { typeof(FhirDate), "\"2026-08-17\"" },
        { typeof(FhirDateTime), "\"2026-08-17T10:30:00Z\"" },
        { typeof(FhirDecimal), "2.50" },
        { typeof(FhirId), "\"patient-1\"" },
        { typeof(FhirInstant), "\"2026-08-17T10:30:00Z\"" },
        { typeof(FhirInteger), "-1" },
        { typeof(FhirInteger64), "\"1048576\"" },
        { typeof(FhirMarkdown), "\"hello\"" },
        { typeof(FhirPositiveInt), "1" },
        { typeof(FhirString), "\"hello\"" },
        { typeof(FhirUnsignedInt), "0" },
        { typeof(FhirUri), "\"Patient/1\"" },
        { typeof(FhirUrl), "\"https://example.test/patient/1\"" }
    };

    public static TheoryData<object, bool> ValidatorCases => new()
    {
        { new FhirBase64Binary("QQ=="), true },
        { new FhirBase64Binary("not base64"), false },
        { new FhirBoolean(false), true },
        { new FhirCanonical("https://example.test/x"), true },
        { new FhirCanonical("relative/path"), false },
        { new FhirCode("active"), true },
        { new FhirCode(" active "), false },
        { new FhirDate("2026-08-17"), true },
        { new FhirDate("2026-99-99"), false },
        { new FhirDateTime("2026-08-17T10:30:00Z"), true },
        { new FhirDateTime("2026-08-17T10:30:00"), false },
        { new FhirDecimal("2.50"), true },
        { new FhirDecimal("02.50"), false },
        { new FhirId("patient-1"), true },
        { new FhirId("Patient/1"), false },
        { new FhirInstant("2026-08-17T10:30:00Z"), true },
        { new FhirInstant("2026-08-17"), false },
        { new FhirInteger(-1), true },
        { new FhirInteger64("1048576"), true },
        { new FhirInteger64("9223372036854775808"), false },
        { new FhirMarkdown("hello"), true },
        { new FhirMarkdown(string.Empty), false },
        { new FhirPositiveInt(1), true },
        { new FhirPositiveInt(0), false },
        { new FhirString("hello"), true },
        { new FhirString(string.Empty), false },
        { new FhirUnsignedInt(0), true },
        { new FhirUnsignedInt(-1), false },
        { new FhirUri("Patient/1"), true },
        { new FhirUri("bad uri"), false },
        { new FhirUrl("https://example.test/patient/1"), true },
        { new FhirUrl("Patient/1"), false }
    };

    [Fact]
    public void DefaultRegistryContainsCompleteUniqueDefinitionMatrix()
    {
        var definitions = GetDefinitions();

        Assert.Equal(ExpectedDefinitions.Count, definitions.Length);
        Assert.Equal(
            definitions.Length,
            definitions.Select(GetFhirTypeName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            definitions.Length,
            definitions.Select(GetPrimitiveType).Distinct().Count());
        Assert.Equal(
            definitions.Select(GetFhirTypeName).Order(StringComparer.Ordinal),
            definitions.Select(GetFhirTypeName));

        foreach (var definition in definitions)
        {
            var primitiveType = GetPrimitiveType(definition);
            Assert.True(
                ExpectedDefinitions.TryGetValue(primitiveType, out var expected),
                $"Unexpected primitive definition '{primitiveType.FullName}'.");

            Assert.Equal(expected.FhirTypeName, GetFhirTypeName(definition));
            Assert.Equal(expected.ValueType, GetProperty<Type>(definition, "ValueType"));
            Assert.NotNull(GetProperty<object>(definition, "Codec"));
            Assert.NotNull(GetProperty<object>(definition, "Validator"));
        }
    }

    [Theory]
    [MemberData(nameof(CodecCases))]
    public void RegisteredCodecRoundTripsRawJson(
        Type primitiveType,
        string rawJson)
    {
        var definition = GetDefinition(primitiveType);
        var codec = GetProperty<object>(definition, "Codec");
        using var document = JsonDocument.Parse(rawJson);
        var rawElement = document.RootElement.Clone();
        var primitive = Invoke(
            codec,
            "CreatePrimitive",
            primitiveType,
            rawElement);
        Assert.NotNull(primitive);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Invoke(codec, "WriteRawValue", writer, primitive!, false);
            writer.Flush();
        }

        Assert.Equal(rawJson, Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Theory]
    [MemberData(nameof(ValidatorCases))]
    public void RegisteredValidatorPreservesPrimitiveFormatBehavior(
        object primitive,
        bool expectedIsValid)
    {
        var definition = GetDefinition(primitive.GetType());
        var validator = GetProperty<object>(definition, "Validator");

        var actual = Invoke(validator, "IsValid", primitive);

        Assert.Equal(expectedIsValid, Assert.IsType<bool>(actual));
    }

    [Fact]
    public void DuplicateRegistrationFailsDeterministically()
    {
        var definition = GetDefinitions()[0];
        var definitions = CreateDefinitionArray(definition, definition);

        var exception = Assert.Throws<TargetInvocationException>(
            () => InvokeRegistryCreate(definitions));

        var failure = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.StartsWith(
            "Duplicate FHIR primitive type name",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateWrapperRegistrationFailsDeterministically()
    {
        var definition = GetDefinitions()[0];
        var duplicate = CreateDefinition(
            "alternateTypeName",
            GetPrimitiveType(definition),
            GetProperty<Type>(definition, "ValueType"),
            GetProperty<object>(definition, "Codec"),
            GetProperty<object>(definition, "Validator"));
        var definitions = CreateDefinitionArray(definition, duplicate);

        var exception = Assert.Throws<TargetInvocationException>(
            () => InvokeRegistryCreate(definitions));

        var failure = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.StartsWith(
            "Duplicate primitive wrapper type",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRegistrationFailsDeterministically()
    {
        var registry = GetDefaultRegistry();
        var getRequired = registry
            .GetType()
            .GetMethod(
                "GetRequired",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(Type)],
                modifiers: null)
            ?? throw new InvalidOperationException(
                "PrimitiveRegistry.GetRequired(Type) was not found.");

        var exception = Assert.Throws<TargetInvocationException>(
            () => getRequired.Invoke(registry, [typeof(UnregisteredPrimitive)]));

        Assert.IsType<KeyNotFoundException>(exception.InnerException);
    }

    private static object[] GetDefinitions()
    {
        var definitions = GetDefaultRegistry()
            .GetType()
            .GetProperty(
                "Definitions",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(GetDefaultRegistry()) as IEnumerable
            ?? throw new InvalidOperationException(
                "PrimitiveRegistry.Definitions was not found.");

        return definitions.Cast<object>().ToArray();
    }

    private static object GetDefinition(Type primitiveType)
    {
        return Assert.Single(
            GetDefinitions(),
            definition => GetPrimitiveType(definition) == primitiveType);
    }

    private static object GetDefaultRegistry()
    {
        var registryType = typeof(FhirObject).Assembly.GetType(
            "MyFhirSdk.Primitives.PrimitiveRegistry",
            throwOnError: true)!;

        return registryType
            .GetProperty(
                "Default",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null)
            ?? throw new InvalidOperationException(
                "PrimitiveRegistry.Default was not found.");
    }

    private static Array CreateDefinitionArray(params object[] definitions)
    {
        var definitionInterface = typeof(FhirObject).Assembly.GetType(
            "MyFhirSdk.Primitives.IPrimitiveDefinition",
            throwOnError: true)!;
        var array = Array.CreateInstance(definitionInterface, definitions.Length);

        for (var index = 0; index < definitions.Length; index++)
        {
            array.SetValue(definitions[index], index);
        }

        return array;
    }

    private static object CreateDefinition(
        string fhirTypeName,
        Type primitiveType,
        Type valueType,
        object codec,
        object validator)
    {
        var definitionType = typeof(FhirObject).Assembly.GetType(
            "MyFhirSdk.Primitives.PrimitiveDefinition",
            throwOnError: true)!;
        var constructor = definitionType.GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();

        return constructor.Invoke(
            [fhirTypeName, primitiveType, valueType, codec, validator]);
    }

    private static void InvokeRegistryCreate(Array definitions)
    {
        var registryType = GetDefaultRegistry().GetType();
        var create = registryType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "PrimitiveRegistry.Create was not found.");

        create.Invoke(null, [definitions]);
    }

    private static string GetFhirTypeName(object definition)
    {
        return GetProperty<string>(definition, "FhirTypeName");
    }

    private static Type GetPrimitiveType(object definition)
    {
        return GetProperty<Type>(definition, "PrimitiveType");
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        var value = target
            .GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(target);

        return Assert.IsAssignableFrom<T>(value);
    }

    private static object? Invoke(
        object target,
        string methodName,
        params object?[] arguments)
    {
        var method = target
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                $"{target.GetType().Name}.{methodName} was not found.");

        return method.Invoke(target, arguments);
    }

    private sealed class UnregisteredPrimitive : PrimitiveType<string>
    {
    }
}
