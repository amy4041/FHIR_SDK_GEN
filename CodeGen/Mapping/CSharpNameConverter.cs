using System.Text;

namespace MyFhirSdk.CodeGen.Mapping;

public sealed class CSharpNameConverter
{
    private static readonly HashSet<string> CSharpKeywords =
        new(StringComparer.Ordinal)
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

    public CSharpNameConversionResult ConvertTypeName(string? fhirTypeName)
    {
        return ConvertIdentifier(fhirTypeName);
    }

    public CSharpNameConversionResult ConvertPropertyName(
        string? fhirNameOrPath,
        IReadOnlySet<string>? existingNames = null)
    {
        var fhirName = GetLastPathSegment(fhirNameOrPath);
        var result = ConvertIdentifier(fhirName);

        if (!result.IsSuccess ||
            existingNames is null ||
            !existingNames.Contains(result.Name!))
        {
            return result;
        }

        return new CSharpNameConversionResult(
            result.Name,
            CSharpNameConversionFailure.Conflict);
    }

    private static CSharpNameConversionResult ConvertIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return InvalidResult();
        }

        var builder = new StringBuilder(value.Length);
        var startOfWord = true;

        foreach (var character in value)
        {
            if (char.IsAsciiLetter(character))
            {
                builder.Append(
                    startOfWord
                        ? char.ToUpperInvariant(character)
                        : character);
                startOfWord = false;
                continue;
            }

            if (char.IsAsciiDigit(character))
            {
                if (builder.Length == 0)
                {
                    builder.Append('_');
                }

                builder.Append(character);
                startOfWord = false;
                continue;
            }

            startOfWord = true;
        }

        if (builder.Length == 0)
        {
            return InvalidResult();
        }

        var identifier = builder.ToString();
        if (CSharpKeywords.Contains(identifier))
        {
            identifier += "_";
        }

        return new CSharpNameConversionResult(
            identifier,
            CSharpNameConversionFailure.None);
    }

    private static string? GetLastPathSegment(string? fhirNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fhirNameOrPath))
        {
            return fhirNameOrPath;
        }

        var separatorIndex = fhirNameOrPath.LastIndexOf('.');
        return separatorIndex < 0
            ? fhirNameOrPath
            : fhirNameOrPath[(separatorIndex + 1)..];
    }

    private static CSharpNameConversionResult InvalidResult()
    {
        return new CSharpNameConversionResult(
            null,
            CSharpNameConversionFailure.InvalidIdentifier);
    }
}
