using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Cli;

public sealed class GeneratorCommandLineParser
{
    public const string Usage =
        """
        Usage:
          # Phase B primitive batch mode
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --mode primitive \
            --input <definitions-path> \
            --policy <policy-path> \
            --output <path> \
            --fhir-version <version> \
            --package-id <package-id> \
            --package-version <package-version>

          # Phase C R5 model batch mode (omit --canonical for full scope)
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --mode model \
            --input <package.tgz> \
            --output <path> \
            --fhir-version <version> \
            --package-id <package-id> \
            --package-version <package-version> \
            [--canonical <structure-definition-canonical> ...]
        """;

    public CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 1 &&
            (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase)))
        {
            return new CommandLineParseResult(null, ShowHelp: true);
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
                "primitive" => ParsePrimitive(remaining),
                "model" => ParseModel(remaining),
                _ => Invalid(
                    $"Unknown generator mode '{mode}'. Expected " +
                    "'primitive' or 'model'.")
            };
        }

        return Invalid("Required option '--mode' was not provided.");
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

    private static CommandLineParseResult ParseModel(IReadOnlyList<string> args)
    {
        string? input = null; string? output = null; string? fhirVersion = null;
        string? packageId = null; string? packageVersion = null; string? primitivePolicy = null;
        var canonicals = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            var option = args[index];
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                return Invalid($"Option '{option}' requires a value.");
            var value = args[index + 1];
            switch (option)
            {
                case "--input" when input is null: input = value; break;
                case "--output" when output is null: output = value; break;
                case "--fhir-version" when fhirVersion is null: fhirVersion = value; break;
                case "--package-id" when packageId is null: packageId = value; break;
                case "--package-version" when packageVersion is null: packageVersion = value; break;
                case "--policy" when primitivePolicy is null: primitivePolicy = value; break;
                case "--canonical" when canonicals.Add(value): break;
                case "--input" or "--output" or "--fhir-version" or "--package-id" or "--package-version" or "--policy":
                    return Invalid($"Option '{option}' may only be specified once.");
                case "--canonical": return Invalid($"Canonical '{value}' may only be specified once.");
                default: return Invalid($"Unknown option '{option}'.");
            }
        }
        var required = new[] { ("--input", input), ("--output", output), ("--fhir-version", fhirVersion),
            ("--package-id", packageId), ("--package-version", packageVersion) };
        var missing = required.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Item2));
        if (missing != default) return Invalid($"Required option '{missing.Item1}' was not provided.");

        string Policy(string name) => Path.Combine(AppContext.BaseDirectory, "Policy", name);
        return new CommandLineParseResult(null, false, null,
            new ModelGenerationOptions(
                input!, output!, packageId!, packageVersion!, fhirVersion!,
                primitivePolicy ?? PrimitiveGenerationPolicyDefaults.GetPath(),
                Policy("r5-model-ownership-policy.json"),
                new ModelIrPolicyPaths(
                    Policy("r5-model-naming-policy.json"),
                    Policy("r5-backbone-policy.json"),
                    Policy("r5-choice-open-type-policy.json")),
                Policy("r5-validation-capability-policy.json"),
                canonicals.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                ModelGenerationPipeline.DefaultCodeGenVersion));
    }

    private static CommandLineParseResult Invalid(string message) =>
        new(message, ShowHelp: false);
}
