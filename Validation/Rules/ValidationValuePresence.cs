using System.Collections;
using MyFhirSdk.Core;

namespace MyFhirSdk.Validation.Rules;

internal static class ValidationValuePresence
{
    public static bool IsPresent(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return text.Length > 0;
        }

        if (IsPrimitive(value))
        {
            return HasPrimitiveValue(value)
                || value is Element { Extension.Count: > 0 };
        }

        if (value is IEnumerable values && value is not string)
        {
            return values.Cast<object?>().Any(item => item is not null);
        }

        return true;
    }

    private static bool IsPrimitive(object value)
    {
        var type = value.GetType();
        while (type is not null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PrimitiveType<>))
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    private static bool HasPrimitiveValue(object value)
    {
        return value.GetType().GetProperty(nameof(PrimitiveType<object>.HasValue))?.GetValue(value) is true;
    }
}
