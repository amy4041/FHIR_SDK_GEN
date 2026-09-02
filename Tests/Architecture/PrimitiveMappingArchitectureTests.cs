using System.Reflection;
using MyFhirSdk.CodeGen.Mapping;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PrimitiveMappingArchitectureTests
{
    [Fact]
    public void CSharpTypeMapperRequiresValidatedPolicyViewWithoutStaticDictionary()
    {
        const BindingFlags staticFields =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var mapperType = typeof(CSharpTypeMapper);
        var staticPrimitiveDictionaries = mapperType
            .GetFields(staticFields)
            .Where(field => IsStringDictionary(field.FieldType))
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(staticPrimitiveDictionaries);
        Assert.Null(mapperType.GetField("PrimitiveTypeNames", staticFields));
        Assert.Null(mapperType.GetField("DefaultComplexTypeNames", staticFields));
        var constructor = Assert.Single(mapperType.GetConstructors());
        Assert.Equal(
            typeof(PrimitiveTypeMappingView),
            constructor.GetParameters()[0].ParameterType);
        Assert.Equal(
            typeof(DefinitionTypeMappingView),
            constructor.GetParameters()[1].ParameterType);
        Assert.False(constructor.GetParameters()[1].HasDefaultValue);
    }

    private static bool IsStringDictionary(Type type)
    {
        return type.GetInterfaces()
            .Append(type)
            .Where(candidate => candidate.IsGenericType)
            .Any(candidate =>
                candidate.GetGenericTypeDefinition() ==
                    typeof(IReadOnlyDictionary<,>) &&
                candidate.GetGenericArguments() is [var key, var value] &&
                key == typeof(string) &&
                value == typeof(string));
    }
}
