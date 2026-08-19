using System.Reflection;
using System.Reflection.Emit;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;

namespace MyFhirSdk.Tests.Architecture;

public sealed class RuntimeModelDependencyTests
{
    private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

    private static readonly string[] RuntimeEngineNamespacePrefixes =
    [
        "MyFhirSdk.Serialization",
        "MyFhirSdk.Validation"
    ];

    private static readonly string[] ForbiddenConcreteModelNamespacePrefixes =
    [
        "MyFhirSdk.Resources",
        "MyFhirSdk.Types"
    ];

    [Fact]
    public void RuntimeEnginesDoNotReferenceConcreteR5Models()
    {
        var runtimeAssembly = typeof(FhirObject).Assembly;
        var violations = runtimeAssembly
            .GetTypes()
            .Where(IsRuntimeEngineType)
            .SelectMany(GetReferencedTypes)
            .Where(reference => IsForbiddenConcreteModel(reference.ReferencedType))
            .Select(reference =>
                $"{reference.Origin} -> {reference.ReferencedType.FullName}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Runtime engine must not reference concrete R5 Resources/Types. " +
            "Move model-specific bindings to ModelMetadata/R5." +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DependencyScannerDetectsDeliberateConcreteModelCoupling()
    {
        var references = GetReferencedTypes(
                typeof(DeliberatelyCoupledEngineFixture))
            .ToArray();

        Assert.Contains(
            references,
            reference => reference.ReferencedType == typeof(Patient));
    }

    [Fact]
    public void RuntimeEnginesDoNotReferenceConcretePrimitiveWrappers()
    {
        var runtimeAssembly = typeof(FhirObject).Assembly;
        var primitiveWrappers = runtimeAssembly
            .GetExportedTypes()
            .Where(IsConcretePrimitiveWrapper)
            .ToHashSet();
        var violations = runtimeAssembly
            .GetTypes()
            .Where(IsRuntimeEngineType)
            .SelectMany(GetReferencedTypes)
            .Where(reference => primitiveWrappers.Contains(reference.ReferencedType))
            .Select(reference =>
                $"{reference.Origin} -> {reference.ReferencedType.FullName}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Runtime engine must dispatch primitive behavior through the " +
            "definition/codec/validator contracts, not concrete wrappers." +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static bool IsRuntimeEngineType(Type type)
    {
        var typeNamespace = type.Namespace ?? string.Empty;
        return RuntimeEngineNamespacePrefixes.Any(prefix =>
            typeNamespace.Equals(prefix, StringComparison.Ordinal) ||
            typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    private static bool IsForbiddenConcreteModel(Type type)
    {
        var typeNamespace = type.Namespace ?? string.Empty;
        return ForbiddenConcreteModelNamespacePrefixes.Any(prefix =>
            typeNamespace.Equals(prefix, StringComparison.Ordinal) ||
            typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    private static bool IsConcretePrimitiveWrapper(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType &&
                current.GetGenericTypeDefinition() == typeof(PrimitiveType<>))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<TypeReference> GetReferencedTypes(Type sourceType)
    {
        const BindingFlags memberFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        foreach (var referencedType in ExpandType(sourceType.BaseType))
        {
            yield return new TypeReference(referencedType, $"{sourceType.FullName} base type");
        }

        foreach (var @interface in sourceType.GetInterfaces())
        {
            foreach (var referencedType in ExpandType(@interface))
            {
                yield return new TypeReference(
                    referencedType,
                    $"{sourceType.FullName} interface");
            }
        }

        foreach (var field in sourceType.GetFields(memberFlags))
        {
            foreach (var referencedType in ExpandType(field.FieldType))
            {
                yield return new TypeReference(
                    referencedType,
                    $"{sourceType.FullName}.{field.Name} field");
            }
        }

        foreach (var property in sourceType.GetProperties(memberFlags))
        {
            foreach (var referencedType in ExpandType(property.PropertyType))
            {
                yield return new TypeReference(
                    referencedType,
                    $"{sourceType.FullName}.{property.Name} property");
            }
        }

        foreach (var method in sourceType.GetMethods(memberFlags).Cast<MethodBase>()
                     .Concat(sourceType.GetConstructors(memberFlags)))
        {
            foreach (var reference in GetMethodReferences(method))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<TypeReference> GetMethodReferences(MethodBase method)
    {
        var origin = $"{method.DeclaringType?.FullName}.{method.Name}";

        if (method is MethodInfo methodInfo)
        {
            foreach (var referencedType in ExpandType(methodInfo.ReturnType))
            {
                yield return new TypeReference(referencedType, $"{origin} return type");
            }
        }

        foreach (var parameter in method.GetParameters())
        {
            foreach (var referencedType in ExpandType(parameter.ParameterType))
            {
                yield return new TypeReference(
                    referencedType,
                    $"{origin} parameter '{parameter.Name}'");
            }
        }

        foreach (var genericArgument in method is MethodInfo genericMethod
                     ? genericMethod.GetGenericArguments()
                     : [])
        {
            foreach (var constraint in genericArgument.GetGenericParameterConstraints())
            {
                foreach (var referencedType in ExpandType(constraint))
                {
                    yield return new TypeReference(
                        referencedType,
                        $"{origin} generic constraint");
                }
            }
        }

        var body = method.GetMethodBody();
        if (body is null)
        {
            yield break;
        }

        foreach (var clause in body.ExceptionHandlingClauses)
        {
            if (clause.Flags != ExceptionHandlingClauseOptions.Clause)
            {
                continue;
            }

            foreach (var referencedType in ExpandType(clause.CatchType))
            {
                yield return new TypeReference(referencedType, $"{origin} catch type");
            }
        }

        foreach (var referencedType in ReadIlTypeReferences(method, body))
        {
            yield return new TypeReference(referencedType, $"{origin} method body");
        }
    }

    private static IEnumerable<Type> ReadIlTypeReferences(
        MethodBase method,
        MethodBody body)
    {
        var il = body.GetILAsByteArray() ?? [];
        var offset = 0;

        while (offset < il.Length)
        {
            var firstByte = il[offset++];
            var opCodeValue = firstByte == 0xFE
                ? unchecked((short)(0xFE00 | il[offset++]))
                : (short)firstByte;
            var opCode = OpCodesByValue.TryGetValue(opCodeValue, out var resolved)
                ? resolved
                : throw new InvalidOperationException(
                    $"Unknown IL opcode 0x{opCodeValue:X4} in '{method}'.");

            if (IsMetadataTokenOperand(opCode.OperandType))
            {
                var token = BitConverter.ToInt32(il, offset);
                offset += sizeof(int);

                foreach (var referencedType in ResolveTokenTypes(method, token))
                {
                    yield return referencedType;
                }

                continue;
            }

            offset += GetOperandSize(opCode.OperandType, il, offset, method);
        }
    }

    private static bool IsMetadataTokenOperand(OperandType operandType) =>
        operandType is OperandType.InlineField or
            OperandType.InlineMethod or
            OperandType.InlineTok or
            OperandType.InlineType;

    private static int GetOperandSize(
        OperandType operandType,
        byte[] il,
        int operandOffset,
        MethodBase method)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or
            OperandType.ShortInlineI or
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or
            OperandType.InlineI or
            OperandType.InlineSig or
            OperandType.InlineString or
            OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                sizeof(int) +
                (BitConverter.ToInt32(il, operandOffset) * sizeof(int)),
            _ => throw new InvalidOperationException(
                $"Unsupported IL operand '{operandType}' in '{method}'.")
        };
    }

    private static IEnumerable<Type> ResolveTokenTypes(
        MethodBase method,
        int metadataToken)
    {
        var declaringTypeArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : null;
        var methodArguments = method is MethodInfo { IsGenericMethod: true } methodInfo
            ? methodInfo.GetGenericArguments()
            : null;
        var member = method.Module.ResolveMember(
            metadataToken,
            declaringTypeArguments,
            methodArguments);

        switch (member)
        {
            case Type type:
                return ExpandType(type);
            case FieldInfo field:
                return ExpandType(field.DeclaringType)
                    .Concat(ExpandType(field.FieldType));
            case MethodInfo calledMethod:
                return ExpandType(calledMethod.DeclaringType)
                    .Concat(ExpandType(calledMethod.ReturnType))
                    .Concat(calledMethod.GetParameters()
                        .SelectMany(parameter => ExpandType(parameter.ParameterType)));
            case ConstructorInfo constructor:
                return ExpandType(constructor.DeclaringType)
                    .Concat(constructor.GetParameters()
                        .SelectMany(parameter => ExpandType(parameter.ParameterType)));
            default:
                return [];
        }
    }

    private static IEnumerable<Type> ExpandType(Type? type)
    {
        if (type is null)
        {
            yield break;
        }

        yield return type;

        if (type.HasElementType)
        {
            foreach (var elementType in ExpandType(type.GetElementType()))
            {
                yield return elementType;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var argumentType in ExpandType(argument))
                {
                    yield return argumentType;
                }
            }
        }
    }

    private readonly record struct TypeReference(Type ReferencedType, string Origin);

    private sealed class DeliberatelyCoupledEngineFixture
    {
        public object CreateConcreteR5Resource() => new Patient();
    }
}
