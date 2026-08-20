using System.Globalization;
using System.Reflection;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PrimitiveWrapperBaselineTests
{
    private static readonly IReadOnlyDictionary<Type, Type> ExpectedWrappers =
        new Dictionary<Type, Type>
        {
            [typeof(FhirBase64Binary)] = typeof(string),
            [typeof(FhirBoolean)] = typeof(bool?),
            [typeof(FhirCanonical)] = typeof(string),
            [typeof(FhirCode)] = typeof(string),
            [typeof(FhirDate)] = typeof(string),
            [typeof(FhirDateTime)] = typeof(string),
            [typeof(FhirDecimal)] = typeof(decimal?),
            [typeof(FhirId)] = typeof(string),
            [typeof(FhirInstant)] = typeof(string),
            [typeof(FhirInteger)] = typeof(int?),
            [typeof(FhirInteger64)] = typeof(long?),
            [typeof(FhirMarkdown)] = typeof(string),
            [typeof(FhirPositiveInt)] = typeof(int?),
            [typeof(FhirString)] = typeof(string),
            [typeof(FhirUnsignedInt)] = typeof(int?),
            [typeof(FhirUri)] = typeof(string),
            [typeof(FhirUrl)] = typeof(string)
        };

    private static readonly IReadOnlyDictionary<Type, string[]> ExpectedDeclaredMethods =
        new Dictionary<Type, string[]>
        {
            [typeof(FhirBoolean)] = [nameof(ToString)],
            [typeof(FhirDecimal)] = [nameof(ToString)],
            [typeof(FhirInteger)] = [nameof(ToString)],
            [typeof(FhirInteger64)] = [nameof(ToString)],
            [typeof(FhirPositiveInt)] = [nameof(ToString)],
            [typeof(FhirUnsignedInt)] = [nameof(ToString)]
        };

    [Fact]
    public void RuntimeContainsExactlyTheSeventeenApprovedPrimitiveWrappers()
    {
        var actualWrappers = typeof(FhirString).Assembly
            .GetExportedTypes()
            .Where(type =>
                string.Equals(
                    type.Namespace,
                    "MyFhirSdk.Primitives",
                    StringComparison.Ordinal) &&
                IsPrimitiveWrapper(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var expectedWrappers = ExpectedWrappers.Keys
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedWrappers, actualWrappers);
    }

    [Fact]
    public void PrimitiveWrappersPreserveApprovedTypeAndConstructorShape()
    {
        foreach (var (wrapperType, valueType) in ExpectedWrappers)
        {
            Assert.True(wrapperType.IsPublic, wrapperType.FullName);
            Assert.True(wrapperType.IsSealed, wrapperType.FullName);
            Assert.Equal(
                typeof(PrimitiveType<>).MakeGenericType(valueType),
                wrapperType.BaseType);

            var expectedConstructorParameters = new List<Type[]>
            {
                Type.EmptyTypes,
                new[] { valueType }
            };
            if (wrapperType == typeof(FhirDecimal) ||
                wrapperType == typeof(FhirInteger64))
            {
                expectedConstructorParameters.Add([typeof(string)]);
            }

            var actualConstructorParameters = wrapperType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(constructor => constructor
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .ToArray())
                .ToArray();

            Assert.Equal(
                expectedConstructorParameters.Count,
                actualConstructorParameters.Length);
            Assert.All(
                expectedConstructorParameters,
                expected => Assert.Contains(
                    actualConstructorParameters,
                    actual => actual.SequenceEqual(expected)));
        }
    }

    [Fact]
    public void PrimitiveWrappersPreserveApprovedDeclaredPublicMembers()
    {
        foreach (var wrapperType in ExpectedWrappers.Keys)
        {
            var expectedMethods = ExpectedDeclaredMethods.TryGetValue(
                wrapperType,
                out var methods)
                ? methods
                : [];
            var actualMethods = wrapperType
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                expectedMethods.Order(StringComparer.Ordinal),
                actualMethods);
            Assert.DoesNotContain(
                actualMethods,
                methodName => string.Equals(
                    methodName,
                    "IsValid",
                    StringComparison.Ordinal));

            var literalProperty = wrapperType.GetProperty(
                "Literal",
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            if (wrapperType == typeof(FhirDecimal) ||
                wrapperType == typeof(FhirInteger64))
            {
                Assert.NotNull(literalProperty);
                Assert.Equal(typeof(string), literalProperty.PropertyType);
                Assert.NotNull(literalProperty.GetMethod);
                Assert.Null(literalProperty.SetMethod);
            }
            else
            {
                Assert.Null(literalProperty);
            }
        }
    }

    [Fact]
    public void PrimitiveWrappersPreserveApprovedPublicConstants()
    {
        Assert.Equal(1024 * 1024, FhirString.MaxLength);
        Assert.Equal(1024 * 1024, FhirMarkdown.MaxLength);
        Assert.Equal(18, FhirDecimal.MaxIntegerDigits);
        Assert.Equal(17, FhirDecimal.MaxFractionDigits);
        Assert.Equal(9, FhirDecimal.MaxExponentDigits);

        var actualConstants = ExpectedWrappers.Keys
            .SelectMany(type => type.GetFields(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            .Where(field => field.IsLiteral)
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "FhirDecimal.MaxExponentDigits",
                "FhirDecimal.MaxFractionDigits",
                "FhirDecimal.MaxIntegerDigits",
                "FhirMarkdown.MaxLength",
                "FhirString.MaxLength"
            },
            actualConstants);
    }

    [Fact]
    public void DecimalLiteralConstructorPreservesRawRepresentationAndValue()
    {
        var decimalValue = new FhirDecimal("1.20e2");
        var invalidDecimal = new FhirDecimal("not-a-decimal");

        Assert.Equal(120m, decimalValue.Value);
        Assert.Equal("1.20e2", decimalValue.Literal);
        Assert.Equal("1.20e2", decimalValue.ToString());
        Assert.Null(invalidDecimal.Value);
        Assert.Equal("not-a-decimal", invalidDecimal.Literal);
        Assert.Equal("not-a-decimal", invalidDecimal.ToString());
    }

    [Fact]
    public void Integer64LiteralConstructorPreservesRawRepresentationAndValue()
    {
        var integerValue = new FhirInteger64("00123");
        var overflow = new FhirInteger64("9223372036854775808");

        Assert.Equal(123L, integerValue.Value);
        Assert.Equal("00123", integerValue.Literal);
        Assert.Equal("00123", integerValue.ToString());
        Assert.Null(overflow.Value);
        Assert.Equal("9223372036854775808", overflow.Literal);
        Assert.Equal("9223372036854775808", overflow.ToString());
    }

    [Fact]
    public void NumericWrappersPreserveInvariantToStringBehavior()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal("true", new FhirBoolean(true).ToString());
            Assert.Equal("false", new FhirBoolean(false).ToString());
            Assert.Equal(string.Empty, new FhirBoolean(null).ToString());
            Assert.Equal("1.50", new FhirDecimal(1.50m).ToString());
            Assert.Equal("-42", new FhirInteger(-42).ToString());
            Assert.Equal("42", new FhirInteger64(42L).ToString());
            Assert.Equal("1", new FhirPositiveInt(1).ToString());
            Assert.Equal("0", new FhirUnsignedInt(0).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static bool IsPrimitiveWrapper(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(PrimitiveType<>))
            {
                return true;
            }
        }

        return false;
    }
}
