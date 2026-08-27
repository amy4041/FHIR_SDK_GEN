using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Cli;

public sealed class GeneratorCommandLineParser
{
    public const string Usage =
        """
        Usage:
          # Existing datatype preview mode (default when --mode is omitted)
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --mode datatype-preview \
            --input <path> \
            --output <path> \
            --namespace <namespace> \
            --fhir-version <version> \
            [--policy <primitive-policy-path>] \
            --type <fhir-type> [--type <fhir-type> ...]

          # Phase B primitive batch mode
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --mode primitive \
            --input <definitions-path> \
            --policy <policy-path> \
            --output <path> \
            --fhir-version <version> \
            --package-id <package-id> \
            --package-version <package-version>
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

        var modeIndexes = Enumerable.Range(0, args.Count)
            .Where(index => string.Equals(
                args[index],
                "--mode",
                StringComparison.Ordinal))
            .ToArray();
        if (modeIndexes.Length > 1)
        {
            return Invalid("Option '--mode' may only be specified once.");
        }

        if (modeIndexes.Length == 1)
        {
            var modeIndex = modeIndexes[0];
            if (modeIndex + 1 >= args.Count ||
                args[modeIndex + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return Invalid("Option '--mode' requires a value.");
            }

            var mode = args[modeIndex + 1];
            var remaining = args
                .Where((_, index) => index != modeIndex && index != modeIndex + 1)
                .ToArray();
            return mode switch
            {
                "datatype-preview" => ParseDatatypePreview(remaining),
                "primitive" => ParsePrimitive(remaining),
                _ => Invalid(
                    $"Unknown generator mode '{mode}'. Expected " +
                    "'datatype-preview' or 'primitive'.")
            };
        }

        return ParseDatatypePreview(args);
    }

    private static CommandLineParseResult ParseDatatypePreview(
        IReadOnlyList<string> args)
    {
        string? inputPath = null;
        string? outputPath = null;
        string? targetNamespace = null;
        string? fhirVersion = null;
        string? policyPath = null;
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
                "--policy" when policyPath is not null => option,
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
                case "--policy":
                    policyPath = value;
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
                    StringComparer.Ordinal).ToArray(),
                policyPath ?? PrimitiveGenerationPolicyDefaults.GetPath()),
            null,
            ShowHelp: false);
    }

    private static CommandLineParseResult ParsePrimitive(
        IReadOnlyList<string> args)
    {
        string? inputPath = null;
        string? policyPath = null;
        string? outputPath = null;
        string? fhirVersion = null;
        string? packageId = null;
        string? packageVersion = null;

        for (var index = 0; index < args.Count; index += 2)
        {
            var option = args[index];
            if (index + 1 >= args.Count ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return Invalid($"Option '{option}' requires a value.");
            }

            var value = args[index + 1];
            var duplicate = option switch
            {
                "--input" when inputPath is not null => option,
                "--policy" when policyPath is not null => option,
                "--output" when outputPath is not null => option,
                "--fhir-version" when fhirVersion is not null => option,
                "--package-id" when packageId is not null => option,
                "--package-version" when packageVersion is not null => option,
                _ => null
            };
            if (duplicate is not null)
            {
                return Invalid($"Option '{duplicate}' may only be specified once.");
            }

            switch (option)
            {
                case "--input": inputPath = value; break;
                case "--policy": policyPath = value; break;
                case "--output": outputPath = value; break;
                case "--fhir-version": fhirVersion = value; break;
                case "--package-id": packageId = value; break;
                case "--package-version": packageVersion = value; break;
                default: return Invalid($"Unknown option '{option}'.");
            }
        }

        var required = new[]
        {
            ("--input", inputPath),
            ("--policy", policyPath),
            ("--output", outputPath),
            ("--fhir-version", fhirVersion),
            ("--package-id", packageId),
            ("--package-version", packageVersion)
        };
        var missing = required.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Item2));
        if (missing != default)
        {
            return Invalid($"Required option '{missing.Item1}' was not provided.");
        }

        return new CommandLineParseResult(
            null,
            null,
            ShowHelp: false,
            new PrimitiveGenerationOptions(
                inputPath!,
                policyPath!,
                outputPath!,
                fhirVersion!,
                packageId!,
                packageVersion!,
                PrimitiveGenerationPipeline.DefaultCodeGenVersion));
    }

    private static bool IsValidNamespace(string value)
    {
        return value.Split('.').All(segment =>
            segment.Length > 0 && SyntaxFacts.IsValidIdentifier(segment));
    }

    private static CommandLineParseResult Invalid(string message) =>
        new(null, message, ShowHelp: false);
}
