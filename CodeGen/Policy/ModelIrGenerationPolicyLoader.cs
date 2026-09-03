using System.Security;
using System.Text.Json;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Policy;

public sealed record ModelIrPolicyPaths(
    string NamingPolicyPath,
    string BackbonePolicyPath,
    string ChoicePolicyPath);

public sealed class ModelIrGenerationPolicyLoader
{
    public async Task<GenerationResult<ModelIrGenerationPolicy?>> LoadAsync(
        ModelIrPolicyPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            using var naming = await ReadAsync(paths.NamingPolicyPath, cancellationToken);
            using var backbone = await ReadAsync(paths.BackbonePolicyPath, cancellationToken);
            using var choice = await ReadAsync(paths.ChoicePolicyPath, cancellationToken);

            var namingRoot = naming.RootElement;
            var backboneRoot = backbone.RootElement;
            var choiceRoot = choice.RootElement;
            var fhirVersion = RequireString(namingRoot, "fhirVersion");
            if (!string.Equals(fhirVersion, RequireString(backboneRoot, "fhirVersion"), StringComparison.Ordinal) ||
                !string.Equals(fhirVersion, RequireString(choiceRoot, "fhirVersion"), StringComparison.Ordinal))
            {
                throw new InvalidDataException("C0 model IR policies do not have the same FHIR version.");
            }

            var namespaces = namingRoot.GetProperty("namespaceRules")
                .EnumerateArray()
                .ToDictionary(
                    item => RequireString(item, "category"),
                    item => item,
                    StringComparer.Ordinal);
            var memberRenames = namingRoot.GetProperty("explicitMemberRenames")
                .EnumerateArray()
                .Select(item => new ModelIrMemberRename(
                    RequireString(item, "elementId"),
                    RequireString(item, "clrName"),
                    RequireString(item, "jsonName")))
                .ToArray();
            var profileTypeOverrides = namingRoot.GetProperty("profileTypeOverrides")
                .EnumerateArray()
                .Select(item => new ModelIrProfileTypeOverride(
                    RequireString(item, "profileCanonical"),
                    RequireString(item, "clrType")))
                .ToArray();
            var backboneRenames = backboneRoot.GetProperty("explicitTypeRenames")
                .EnumerateArray()
                .Select(item => new ModelIrBackboneRename(
                    RequireString(item, "elementId"),
                    RequireString(item, "clrName")))
                .ToArray();
            var openTypeIds = choiceRoot.GetProperty("classification")
                .GetProperty("openTypeElementIds")
                .EnumerateArray()
                .Select(item => item.GetString() ?? throw new InvalidDataException(
                    "An open type element id cannot be null."))
                .ToArray();
            var syntheticMembers = namingRoot.GetProperty("syntheticMembers")
                .EnumerateArray()
                .Where(item => string.Equals(
                    RequireString(item, "category"),
                    "concrete-resource",
                    StringComparison.Ordinal))
                .Select(item => RequireString(item, "clrName"))
                .ToArray();

            ValidateDecisions(choiceRoot, backboneRoot);
            var policy = new ModelIrGenerationPolicy(
                fhirVersion,
                RequireString(namespaces["complex-datatype"], "namespace"),
                RequireString(namespaces["resource"], "namespace"),
                RequireString(namespaces["backbone"], "namespace"),
                RequireString(backboneRoot.GetProperty("publicShape"), "baseClrType"),
                RequireString(choiceRoot.GetProperty("openTypeRepresentation"), "generatedClrType"),
                memberRenames,
                profileTypeOverrides,
                backboneRenames,
                openTypeIds,
                syntheticMembers);
            return new GenerationResult<ModelIrGenerationPolicy?>(
                policy,
                Array.Empty<GeneratorDiagnostic>());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or
            InvalidOperationException or KeyNotFoundException or SecurityException or
            UnauthorizedAccessException or NotSupportedException)
        {
            return new GenerationResult<ModelIrGenerationPolicy?>(
                null,
                [new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.ModelIrPolicyReadFailure,
                    GeneratorDiagnosticSeverity.Error,
                    $"Could not load C0 model IR policies: {exception.Message}",
                    string.Join(";", new[]
                    {
                        paths.NamingPolicyPath,
                        paths.BackbonePolicyPath,
                        paths.ChoicePolicyPath
                    }))]);
        }
    }

    private static async Task<JsonDocument> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = File.OpenRead(Path.GetFullPath(path));
        return await JsonDocument.ParseAsync(
            stream,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            },
            cancellationToken);
    }

    private static void ValidateDecisions(JsonElement choice, JsonElement backbone)
    {
        if (!string.Equals(
                RequireString(choice.GetProperty("ordinaryChoiceRepresentation"), "publicShape"),
                "one-nullable-property-per-alternative",
                StringComparison.Ordinal) ||
            !string.Equals(
                RequireString(choice.GetProperty("openTypeRepresentation"), "publicShape"),
                "one-nullable-polymorphic-property",
                StringComparison.Ordinal) ||
            !string.Equals(
                RequireString(backbone.GetProperty("publicShape"), "placement"),
                "public-top-level",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("C0 model IR representation decisions are unsupported.");
        }
    }

    private static string RequireString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString() ??
        throw new InvalidDataException($"Policy property '{propertyName}' is required.");
}
