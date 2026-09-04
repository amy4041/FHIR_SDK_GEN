using System.Collections;
using System.Reflection;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.Core;

namespace MyFhirSdk.Tests.Architecture;

public sealed class RuntimeContractDescriptorArchitectureTests
{
    [Fact]
    public async Task DescriptorMatchesHandwrittenRuntimeShape()
    {
        var result = await new RuntimeContractLoader().LoadAsync(Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "runtime-contract.json"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => $"[{diagnostic.Code}] {diagnostic.Message}")));
        var contract = Assert.IsType<RuntimeContractView>(result.Value);
        var runtimeAssembly = typeof(FhirObject).Assembly;
        AssertAssemblyIdentity(contract.RuntimeAssembly, runtimeAssembly.GetName());
        Assert.Equal(contract.TargetFramework, contract.CompilerReference.TargetFramework);
        AssertAssemblyIdentity(contract.CompilerReference.Assembly, runtimeAssembly.GetName());

        foreach (var symbol in contract.Symbols)
        {
            var runtimeType = runtimeAssembly.GetType(symbol.ClrType, throwOnError: false);
            Assert.True(runtimeType is not null, $"Missing Runtime symbol '{symbol.ClrType}'.");
            Assert.Equal(symbol.Kind == "interface", runtimeType.IsInterface);
            Assert.Equal(symbol.IsAbstract, runtimeType.IsAbstract);
            Assert.Equal(symbol.IsSealed, runtimeType.IsSealed);
            Assert.Equal(symbol.GenericArity, runtimeType.IsGenericTypeDefinition
                ? runtimeType.GetGenericArguments().Length
                : 0);
            Assert.Equal(symbol.BaseClrType, runtimeType.BaseType?.FullName);
            foreach (var interfaceName in symbol.Interfaces)
            {
                Assert.Contains(runtimeType.GetInterfaces(), implemented =>
                    implemented.FullName == interfaceName);
            }
        }

        var nullability = new NullabilityInfoContext();
        foreach (var slot in contract.DeclaredSlots)
        {
            var declaringType = runtimeAssembly.GetType(slot.DeclaringClrType);
            Assert.True(declaringType is not null,
                $"Missing declaring Runtime symbol '{slot.DeclaringClrType}'.");
            var property = declaringType.GetProperty(
                slot.ClrPropertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.True(property is not null,
                $"Missing declared Runtime slot '{slot.DeclaringClrType}.{slot.ClrPropertyName}'.");
            Assert.Equal(slot.PropertyClrType, FormatClrType(property.PropertyType));
            Assert.Equal(slot.IsCollection, IsCollection(property.PropertyType));
            Assert.Equal(slot.ElementClrType, FormatClrType(GetElementType(property.PropertyType)));
            Assert.Equal(
                slot.IsNullable,
                nullability.Create(property).ReadState == NullabilityState.Nullable);
        }
    }

    private static void AssertAssemblyIdentity(
        RuntimeAssemblyIdentity expected,
        AssemblyName actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Version, actual.Version?.ToString());
        var token = actual.GetPublicKeyToken();
        Assert.Equal(
            expected.PublicKeyToken,
            token is null || token.Length == 0
                ? "null"
                : Convert.ToHexString(token).ToLowerInvariant());
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static Type GetElementType(Type type)
    {
        if (!IsCollection(type))
        {
            return type;
        }
        return type.GetInterfaces()
            .Append(type)
            .First(candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .GetGenericArguments()[0];
    }

    private static string FormatClrType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName!;
        }
        var definitionName = type.GetGenericTypeDefinition().FullName!;
        var tick = definitionName.LastIndexOf('`');
        return $"{definitionName[..tick]}<{string.Join(',', type.GetGenericArguments().Select(FormatClrType))}>";
    }
}
