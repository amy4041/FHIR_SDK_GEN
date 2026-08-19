using System.Reflection;
using MyFhirSdk.Core;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PublicApiSnapshotTests
{
    [Fact]
    public void PublicApiMatchesApprovedBaseline()
    {
        var approvedPath = Path.Combine(
            AppContext.BaseDirectory,
            "ApprovedPublicApi.txt");
        var approved = NormalizeNewlines(File.ReadAllText(approvedPath)).TrimEnd();
        var actual = CreateSnapshot(typeof(FhirObject).Assembly);

        if (string.Equals(approved, "PENDING", StringComparison.Ordinal))
        {
            Assert.Fail(
                "ApprovedPublicApi.txt has not been initialized.\n" + actual);
        }

        Assert.Equal(approved, actual);
    }

    [Fact]
    public void PrimitiveValidationImplementationIsNotPublicApi()
    {
        var assembly = typeof(FhirObject).Assembly;
        var exportedPrimitiveTypes = assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == "MyFhirSdk.Primitives")
            .ToArray();

        Assert.DoesNotContain(
            exportedPrimitiveTypes,
            type => type.Name == "IFhirValidatablePrimitive");
        Assert.All(
            exportedPrimitiveTypes,
            type => Assert.Null(type.GetMethod(
                "IsValid",
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)));
    }

    private static string CreateSnapshot(Assembly assembly)
    {
        var lines = new List<string>();

        foreach (var type in assembly
                     .GetExportedTypes()
                     .Where(IsRuntimeSurface)
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            lines.Add(FormatType(type));

            foreach (var member in GetDeclaredPublicMembers(type))
            {
                lines.Add($"  {member.MemberType.ToString().ToUpperInvariant()} {member}");
            }
        }

        return string.Join('\n', lines);
    }

    private static bool IsRuntimeSurface(Type type)
    {
        var typeNamespace = type.Namespace ?? string.Empty;

        return typeNamespace == "MyFhirSdk.Core" ||
            typeNamespace == "MyFhirSdk.ModelMetadata" ||
            typeNamespace.StartsWith(
                "MyFhirSdk.ModelMetadata.",
                StringComparison.Ordinal) ||
            typeNamespace == "MyFhirSdk.Primitives" ||
            typeNamespace == "MyFhirSdk.Serialization" ||
            typeNamespace.StartsWith(
                "MyFhirSdk.Serialization.",
                StringComparison.Ordinal) ||
            typeNamespace == "MyFhirSdk.Validation" ||
            typeNamespace.StartsWith(
                "MyFhirSdk.Validation.",
                StringComparison.Ordinal);
    }

    private static string FormatType(Type type)
    {
        var baseTypeName = type.BaseType is null
            ? "-"
            : FormatTypeName(type.BaseType);
        var interfaces = type
            .GetInterfaces()
            .Where(@interface => @interface.IsVisible)
            .Select(FormatTypeName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var interfaceNames = interfaces.Length == 0
            ? "-"
            : string.Join(", ", interfaces);

        return
            $"TYPE {GetTypeKind(type)} {type.FullName} | " +
            $"Base={baseTypeName} | Interfaces={interfaceNames}";
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definitionName =
            type.GetGenericTypeDefinition().FullName ?? type.Name;
        var arityMarker = definitionName.IndexOf('`');
        if (arityMarker >= 0)
        {
            definitionName = definitionName[..arityMarker];
        }

        var argumentNames = type
            .GetGenericArguments()
            .Select(FormatTypeName);

        return $"{definitionName}<{string.Join(", ", argumentNames)}>";
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

    private static IReadOnlyList<MemberInfo> GetDeclaredPublicMembers(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        return type
            .GetMembers(flags)
            .Where(IsApprovedMemberKind)
            .OrderBy(member => member.MemberType)
            .ThenBy(member => member.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsApprovedMemberKind(MemberInfo member)
    {
        return member.MemberType switch
        {
            MemberTypes.Constructor => true,
            MemberTypes.Event => true,
            MemberTypes.Field => true,
            MemberTypes.Property => true,
            MemberTypes.Method => member is MethodInfo { IsSpecialName: false },
            _ => false
        };
    }

    private static string NormalizeNewlines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
