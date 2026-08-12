using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen.Cli;

public sealed class GeneratorCommandLineParser
{
    public const string Usage =
        """
        Usage:
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --input <path> \
            --output <path> \
            --namespace <namespace> \
            --fhir-version <version> \
            --type <fhir-type> [--type <fhir-type> ...]
        """;

    public CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 1 &&
            (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
        {
            return new CommandLineParseResult(null, null, ShowHelp: true);
        }

        string? inputPath = null;
        string? outputPath = null;
        string? targetNamespace = null;
        string? fhirVersion = null;
        var typeNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < args.Count; index += 2)
        {
            var option = args[index];
            if (index + 1 >= args.Count ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return Invalid($"Option '{option}' requires a value.");
            }

            var value = args[index + 1];
            var duplicateOption = option switch
            {
                "--input" when inputPath is not null => option,
                "--output" when outputPath is not null => option,
                "--namespace" when targetNamespace is not null => option,
                "--fhir-version" when fhirVersion is not null => option,
                _ => null
            };
            if (duplicateOption is not null)
            {
                return Invalid(
                    $"Option '{duplicateOption}' may only be specified once.");
            }

            switch (option)
            {
                case "--input":
                    inputPath = value;
                    break;
                case "--output":
                    outputPath = value;
                    break;
                case "--namespace":
                    targetNamespace = value;
                    break;
                case "--fhir-version":
                    fhirVersion = value;
                    break;
                case "--type":
                    if (!typeNames.Add(value))
                    {
                        return Invalid(
                            $"FHIR type '{value}' may only be specified once.");
                    }

                    break;
                default:
                    return Invalid($"Unknown option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return Invalid("Required option '--input' was not provided.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Invalid("Required option '--output' was not provided.");
        }

        if (string.IsNullOrWhiteSpace(targetNamespace))
        {
            return Invalid("Required option '--namespace' was not provided.");
        }

        if (!IsValidNamespace(targetNamespace))
        {
            return Invalid(
                $"Namespace '{targetNamespace}' is not a valid C# namespace.");
        }

        if (string.IsNullOrWhiteSpace(fhirVersion))
        {
            return Invalid("Required option '--fhir-version' was not provided.");
        }

        if (typeNames.Count == 0)
        {
            return Invalid("At least one '--type' option must be provided.");
        }

        return new CommandLineParseResult(
            new GeneratorOptions(
                inputPath,
                outputPath,
                targetNamespace,
                fhirVersion,
                typeNames.OrderBy(
                    typeName => typeName,
                    StringComparer.Ordinal).ToArray()),
            null,
            ShowHelp: false);
    }

    private static bool IsValidNamespace(string value)
    {
        return value.Split('.').All(segment =>
            segment.Length > 0 && SyntaxFacts.IsValidIdentifier(segment));
    }

    private static CommandLineParseResult Invalid(string message) =>
        new(null, message, ShowHelp: false);
}
