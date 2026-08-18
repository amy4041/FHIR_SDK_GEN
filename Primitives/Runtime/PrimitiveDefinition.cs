using MyFhirSdk.Core;

namespace MyFhirSdk.Primitives;

internal sealed class PrimitiveDefinition : IPrimitiveDefinition
{
    internal PrimitiveDefinition(
        string fhirTypeName,
        Type primitiveType,
        Type valueType,
        IPrimitiveCodec codec,
        IPrimitiveValidator validator)
    {
        if (string.IsNullOrWhiteSpace(fhirTypeName))
        {
            throw new ArgumentException(
                "FHIR primitive type name is required.",
                nameof(fhirTypeName));
        }

        FhirTypeName = fhirTypeName;
        PrimitiveType = primitiveType ?? throw new ArgumentNullException(nameof(primitiveType));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
        Validator = validator ?? throw new ArgumentNullException(nameof(validator));

        if (!PrimitiveValueAccess.IsPrimitiveType(primitiveType))
        {
            throw new ArgumentException(
                $"'{primitiveType.FullName}' does not derive from PrimitiveType<T>.",
                nameof(primitiveType));
        }

        var declaredValueType = PrimitiveValueAccess.GetDeclaredValueType(primitiveType);
        if (declaredValueType != valueType)
        {
            throw new ArgumentException(
                $"Primitive '{primitiveType.FullName}' declares value type " +
                $"'{declaredValueType.FullName}', not '{valueType.FullName}'.",
                nameof(valueType));
        }
    }

    public string FhirTypeName { get; }

    public Type PrimitiveType { get; }

    public Type ValueType { get; }

    public IPrimitiveCodec Codec { get; }

    public IPrimitiveValidator Validator { get; }
}

internal static class PrimitiveValueAccess
{
    internal static IPrimitiveValueAccessor GetAccessor(object primitive)
    {
        return primitive as IPrimitiveValueAccessor
            ?? throw new ArgumentException(
                $"'{primitive.GetType().FullName}' is not a FHIR primitive.",
                nameof(primitive));
    }

    internal static bool IsPrimitiveType(Type type)
    {
        return FindPrimitiveBaseType(type) is not null;
    }

    internal static Type GetDeclaredValueType(Type primitiveType)
    {
        var baseType = FindPrimitiveBaseType(primitiveType)
            ?? throw new ArgumentException(
                $"'{primitiveType.FullName}' does not derive from PrimitiveType<T>.",
                nameof(primitiveType));

        return baseType.GetGenericArguments()[0];
    }

    private static Type? FindPrimitiveBaseType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(PrimitiveType<>))
            {
                return current;
            }
        }

        return null;
    }
}
