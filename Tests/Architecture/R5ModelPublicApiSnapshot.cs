using System.Reflection;
using System.Text.Json.Serialization;

namespace MyFhirSdk.Tests.Architecture;

internal static class R5ModelPublicApiSnapshot
{
    private static readonly IReadOnlySet<string> CoreModelTypeNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "MyFhirSdk.Core.BackboneElement",
            "MyFhirSdk.Core.BackboneType",
            "MyFhirSdk.Core.Base",
            "MyFhirSdk.Core.DataType",
            "MyFhirSdk.Core.DomainResource",
            "MyFhirSdk.Core.Element",
            "MyFhirSdk.Core.Extension",
            "MyFhirSdk.Core.FhirObject",
            "MyFhirSdk.Core.IFhirExtensionValue",
            "MyFhirSdk.Core.Meta",
            "MyFhirSdk.Core.Narrative",
            "MyFhirSdk.Core.Resource"
        };

    internal static IReadOnlyList<Type> GetSurfaceTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetExportedTypes()
            .Where(IsR5ModelSurface)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    internal static string Create(IEnumerable<Type> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lines = new List<string>();
        foreach (var type in source.OrderBy(
                     type => type.FullName,
                     StringComparer.Ordinal))
        {
            lines.Add(FormatType(type));
            lines.AddRange(GetDeclaredApiLines(type));
        }

        return string.Join('\n', lines);
    }

    private static bool IsR5ModelSurface(Type type)
    {
        return type.Namespace is "MyFhirSdk.Types" or "MyFhirSdk.Resources" ||
            type.FullName is not null && CoreModelTypeNames.Contains(type.FullName);
    }

    private static string FormatType(Type type)
    {
        var baseType = type.BaseType is null
            ? "-"
            : FormatTypeName(type.BaseType);
        var interfaces = type
            .GetInterfaces()
            .Where(@interface => @interface.IsVisible)
            .Select(@interface => FormatTypeName(@interface))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return
            $"TYPE {GetTypeAccessibility(type)} {GetTypeKind(type)} " +
            $"{type.FullName} | Base={baseType} | " +
            $"Interfaces={(interfaces.Length == 0 ? "-" : string.Join(", ", interfaces))}";
    }

    private static IReadOnlyList<string> GetDeclaredApiLines(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;
        var nullability = new NullabilityInfoContext();
        var lines = new List<string>();

        lines.AddRange(type
            .GetConstructors(flags)
            .Where(IsExternallyVisible)
            .Select(constructor => FormatConstructor(constructor, nullability)));
        lines.AddRange(type
            .GetProperties(flags)
            .Where(IsExternallyVisible)
            .Select(property => FormatProperty(property, nullability)));
        lines.AddRange(type
            .GetMethods(flags)
            .Where(method =>
                !method.IsSpecialName &&
                IsExternallyVisible(method))
            .Select(method => FormatMethod(method, nullability)));
        lines.AddRange(type
            .GetFields(flags)
            .Where(IsExternallyVisible)
            .Select(field => FormatField(field, nullability)));
        lines.AddRange(type
            .GetEvents(flags)
            .Where(IsExternallyVisible)
            .Select(@event => FormatEvent(@event, nullability)));

        return lines
            .OrderBy(line => line, StringComparer.Ordinal)
            .Select(line => $"  {line}")
            .ToArray();
    }

    private static string FormatConstructor(
        ConstructorInfo constructor,
        NullabilityInfoContext nullability)
    {
        var parameters = constructor
            .GetParameters()
            .Select(parameter => FormatParameter(parameter, nullability));

        return
            $"CONSTRUCTOR {GetMemberAccessibility(constructor)} " +
            $".ctor({string.Join(", ", parameters)})";
    }

    private static string FormatProperty(
        PropertyInfo property,
        NullabilityInfoContext nullability)
    {
        var propertyNullability = nullability.Create(property);
        var indexParameters = property
            .GetIndexParameters()
            .Select(parameter => FormatParameter(parameter, nullability))
            .ToArray();
        var propertyName = indexParameters.Length == 0
            ? property.Name
            : $"{property.Name}[{string.Join(", ", indexParameters)}]";
        var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?
            .Name ?? "-";

        return
            $"PROPERTY {FormatTypeName(property.PropertyType, propertyNullability)} " +
            $"{propertyName} | get={GetAccessorAccessibility(property.GetMethod)} | " +
            $"set={GetAccessorAccessibility(property.SetMethod)} | " +
            $"dispatch={GetDispatchKind(property)} | JsonName={jsonName}";
    }

    private static string FormatMethod(
        MethodInfo method,
        NullabilityInfoContext nullability)
    {
        var parameters = method
            .GetParameters()
            .Select(parameter => FormatParameter(parameter, nullability));
        var genericArguments = method.IsGenericMethodDefinition
            ? $"<{string.Join(", ", method.GetGenericArguments().Select(type => type.Name))}>"
            : string.Empty;

        return
            $"METHOD {GetMemberAccessibility(method)} " +
            $"{FormatTypeName(method.ReturnType, nullability.Create(method.ReturnParameter))} " +
            $"{method.Name}{genericArguments}({string.Join(", ", parameters)}) | " +
            $"dispatch={GetDispatchKind(method)}";
    }

    private static string FormatField(
        FieldInfo field,
        NullabilityInfoContext nullability)
    {
        var modifiers = field.IsLiteral
            ? "const"
            : field.IsInitOnly
                ? "readonly"
                : "mutable";

        return
            $"FIELD {GetMemberAccessibility(field)} {modifiers} " +
            $"{FormatTypeName(field.FieldType, nullability.Create(field))} {field.Name}";
    }

    private static string FormatEvent(
        EventInfo @event,
        NullabilityInfoContext nullability)
    {
        var accessor = @event.AddMethod ?? @event.RemoveMethod;
        var eventType = @event.EventHandlerType ?? typeof(void);

        return
            $"EVENT {GetAccessorAccessibility(accessor)} " +
            $"{FormatTypeName(eventType, nullability.Create(@event))} {@event.Name}";
    }

    private static string FormatParameter(
        ParameterInfo parameter,
        NullabilityInfoContext nullability)
    {
        var defaultValue = parameter.HasDefaultValue
            ? $" = {FormatDefaultValue(parameter.DefaultValue)}"
            : string.Empty;

        return
            $"{FormatTypeName(parameter.ParameterType, nullability.Create(parameter))} " +
            $"{parameter.Name}{defaultValue}";
    }

    private static string FormatTypeName(
        Type type,
        NullabilityInfo? nullability = null)
    {
        if (type.IsByRef)
        {
            return $"ref {FormatTypeName(type.GetElementType()!, nullability?.ElementType)}";
        }

        var nullableValueType = Nullable.GetUnderlyingType(type);
        if (nullableValueType is not null)
        {
            return $"{FormatTypeName(nullableValueType)}?";
        }

        if (type.IsArray)
        {
            var arrayName =
                $"{FormatTypeName(type.GetElementType()!, nullability?.ElementType)}[]";
            return AppendReferenceNullability(arrayName, type, nullability);
        }

        string name;
        if (type.IsGenericParameter)
        {
            name = type.Name;
        }
        else if (type.IsGenericType)
        {
            var definitionName =
                type.GetGenericTypeDefinition().FullName ?? type.Name;
            var arityMarker = definitionName.IndexOf('`');
            if (arityMarker >= 0)
            {
                definitionName = definitionName[..arityMarker];
            }

            var arguments = type.GetGenericArguments();
            var argumentNullability = nullability?.GenericTypeArguments;
            var formattedArguments = arguments.Select(
                (argument, index) => FormatTypeName(
                    argument,
                    argumentNullability is not null && index < argumentNullability.Length
                        ? argumentNullability[index]
                        : null));
            name = $"{definitionName}<{string.Join(", ", formattedArguments)}>";
        }
        else
        {
            name = type.FullName ?? type.Name;
        }

        return AppendReferenceNullability(name, type, nullability);
    }

    private static string AppendReferenceNullability(
        string name,
        Type type,
        NullabilityInfo? nullability)
    {
        return !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable
            ? $"{name}?"
            : name;
    }

    private static string FormatDefaultValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => $"\"{text.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
            char character => $"'{character}'",
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture) ??
                "null"
        };
    }

    private static bool IsExternallyVisible(MethodBase method)
    {
        return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    private static bool IsExternallyVisible(FieldInfo field)
    {
        return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
    }

    private static bool IsExternallyVisible(PropertyInfo property)
    {
        return IsExternallyVisible(property.GetMethod) ||
            IsExternallyVisible(property.SetMethod);
    }

    private static bool IsExternallyVisible(EventInfo @event)
    {
        return IsExternallyVisible(@event.AddMethod) ||
            IsExternallyVisible(@event.RemoveMethod);
    }

    private static bool IsExternallyVisible(MethodInfo? method)
    {
        return method is not null && IsExternallyVisible((MethodBase)method);
    }

    private static string GetAccessorAccessibility(MethodInfo? accessor)
    {
        return accessor is null || !IsExternallyVisible(accessor)
            ? "-"
            : GetMemberAccessibility(accessor);
    }

    private static string GetMemberAccessibility(MethodBase method)
    {
        if (method.IsPublic)
        {
            return "public";
        }

        if (method.IsFamilyOrAssembly)
        {
            return "protected internal";
        }

        return method.IsFamily ? "protected" : "non-public";
    }

    private static string GetMemberAccessibility(FieldInfo field)
    {
        if (field.IsPublic)
        {
            return "public";
        }

        if (field.IsFamilyOrAssembly)
        {
            return "protected internal";
        }

        return field.IsFamily ? "protected" : "non-public";
    }

    private static string GetDispatchKind(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        return accessor is null ? "none" : GetDispatchKind(accessor);
    }

    private static string GetDispatchKind(MethodInfo method)
    {
        if (method.IsAbstract)
        {
            return "abstract";
        }

        if (method.GetBaseDefinition() != method)
        {
            return "override";
        }

        return method.IsVirtual ? "virtual" : "none";
    }

    private static string GetTypeAccessibility(Type type)
    {
        return type.IsPublic || type.IsNestedPublic ? "public" : "non-public";
    }

    private static string GetTypeKind(Type type)
    {
        if (type.IsInterface)
        {
            return "interface";
        }

        if (type.IsEnum)
        {
            return "enum";
        }

        if (type.IsAbstract && type.IsSealed)
        {
            return "static class";
        }

        if (type.IsAbstract)
        {
            return "abstract class";
        }

        return type.IsSealed ? "sealed class" : "class";
    }
}
