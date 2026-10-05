using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace MyFhirSdk.KernelMigration;

internal static class KernelApiInventory
{
    public static string Create(Assembly assembly)
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal) { "ASSEMBLY " + assembly.FullName };
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        var nullability = new NullabilityInfoContext();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
        string TypeId(Type type)
        {
            if (type.IsGenericParameter) return (type.DeclaringMethod is null ? "!" : "!!") + type.GenericParameterPosition;
            if (type.HasElementType)
                return TypeId(type.GetElementType()!) + (type.IsByRef ? "&" : type.IsPointer ? "*" :
                    type.IsSZArray ? "[]" : "[" + (type.GetArrayRank() == 1 ? "*" : new string(',', type.GetArrayRank() - 1)) + "]");
            var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            var name = definition.Assembly.GetName().Name!;
            var fullName = definition.Assembly.FullName!;
            if (identities.TryGetValue(name, out var previous) && previous != fullName)
                throw new InvalidOperationException("Ambiguous assembly alias: " + name);
            identities[name] = fullName;
            return "[" + name + "]" + definition.FullName +
                (type.IsGenericType ? "<" + string.Join(",", type.GetGenericArguments().Select(TypeId)) + ">" : "");
        }
        string Parameter(ParameterInfo p) => $"{p.Attributes}:{TypeId(p.ParameterType)} {p.Name}" +
            " nullability=" + Nullability(nullability.Create(p)) +
            (p.HasDefaultValue ? " default=" + Constant(p.DefaultValue) : "") +
            " req(" + string.Join(",", p.GetRequiredCustomModifiers().Select(TypeId)) + ") opt(" +
            string.Join(",", p.GetOptionalCustomModifiers().Select(TypeId)) + ")";
        void Generics(string owner, IEnumerable<Type> arguments)
        {
            foreach (var p in arguments.Where(t => t.IsGenericParameter))
                lines.Add(owner + " GENERIC " + TypeId(p) + " " + p.GenericParameterAttributes + " : " +
                    string.Join(";", p.GetGenericParameterConstraints().Select(TypeId).Order(StringComparer.Ordinal)));
        }
        foreach (var type in assembly.GetExportedTypes())
        {
            var id = TypeId(type);
            lines.Add("TYPE " + id + " " + type.Attributes + " BASE " + (type.BaseType is null ? "-" : TypeId(type.BaseType)));
            Generics(id, type.GetGenericArguments());
            foreach (var contract in type.GetInterfaces()) lines.Add(id + " INTERFACE " + TypeId(contract));
            foreach (var field in type.GetFields(flags).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
                lines.Add(id + " FIELD " + field.Attributes + " " + TypeId(field.FieldType) + " " + field.Name +
                    " nullability=" + Nullability(nullability.Create(field)) +
                    (field.IsLiteral ? " value=" + Constant(field.GetRawConstantValue()) : ""));
            foreach (var property in type.GetProperties(flags).Where(p => p.GetAccessors(true).Any(Visible)))
                lines.Add(id + " PROPERTY " + TypeId(property.PropertyType) + " " + property.Name + "(" +
                    string.Join(",", property.GetIndexParameters().Select(Parameter)) + ") nullability=" + Nullability(nullability.Create(property)));
            foreach (var e in type.GetEvents(flags).Where(e => e.AddMethod is not null && Visible(e.AddMethod)))
                lines.Add(id + " EVENT " + TypeId(e.EventHandlerType!) + " " + e.Name + " nullability=" + Nullability(nullability.Create(e)));
            foreach (var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)).Where(Visible))
            {
                var signature = id + " METHOD " + method.Attributes + " " + method.Name +
                    (method.IsGenericMethod ? "``" + method.GetGenericArguments().Length : "") + "(" + string.Join(",", method.GetParameters().Select(Parameter)) + ")" +
                    (method is MethodInfo info ? " -> " + Parameter(info.ReturnParameter) : "");
                lines.Add(signature);
                if (method.IsGenericMethod) Generics(signature, method.GetGenericArguments());
            }
        }
        foreach (var pair in identities) lines.Add("IDENTITY [" + pair.Key + "] = " + pair.Value);
        return string.Join('\n', lines) + "\n";
    }

    private static string Nullability(NullabilityInfo info) =>
        info.ReadState + "/" + info.WriteState +
        (info.ElementType is null ? "" : "[" + Nullability(info.ElementType) + "]") +
        (info.GenericTypeArguments.Length == 0 ? "" : "<" + string.Join(",", info.GenericTypeArguments.Select(Nullability)) + ">");

    private static string Constant(object? value) => value switch
    {
        null => "null",
        Missing => "<missing>",
        DBNull => "<dbnull>",
        string text => JsonSerializer.Serialize(text),
        char character => JsonSerializer.Serialize(character.ToString()),
        bool boolean => boolean ? "true" : "false",
        Enum e => Convert.ToString(Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)!,
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("Unsupported constant type: " + value.GetType().FullName)
    };
}
