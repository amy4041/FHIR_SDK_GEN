using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Inventory;

public sealed class PrimitiveInventoryPolicyJoiner
{
    public GenerationResult<PrimitiveInventoryPolicyCoverage?> Join(
        PrimitiveDefinitionInventory inventory,
        ValidatedPrimitiveGenerationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(policy);

        var diagnostics = new List<GeneratorDiagnostic>();
        var matches = new List<PrimitiveInventoryPolicyMatch>();
        var inventoryByType = inventory.Items.ToDictionary(
            item => item.FhirTypeName,
            StringComparer.Ordinal);
        var policyByType = policy.Primitives.ToDictionary(
            entry => entry.FhirTypeName,
            StringComparer.Ordinal);

        var topLevelVersionMatches = string.Equals(
            inventory.FhirVersion,
            policy.FhirVersion,
            StringComparison.Ordinal);
        if (!topLevelVersionMatches)
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                GeneratorDiagnosticCodes.PrimitivePolicyIdentityMismatch,
                policy,
                null,
                $"Primitive inventory FHIR version '{inventory.FhirVersion}' does not " +
                $"match policy FHIR version '{policy.FhirVersion}'."));
        }

        foreach (var definition in inventory.Items.OrderBy(
                     item => item.FhirTypeName,
                     StringComparer.Ordinal))
        {
            if (!policyByType.TryGetValue(
                    definition.FhirTypeName,
                    out var policyEntry))
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.MissingPrimitivePolicyEntry,
                    GeneratorDiagnosticSeverity.Error,
                    $"Primitive inventory type '{definition.FhirTypeName}' has no " +
                    "policy entry.",
                    definition.SourceFile,
                    definition.Canonical,
                    definition.FhirVersion));
                continue;
            }

            var identityMatches = true;
            if (!string.Equals(
                    definition.Canonical,
                    policyEntry.Canonical,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(CreatePolicyDiagnostic(
                    GeneratorDiagnosticCodes.PrimitivePolicyIdentityMismatch,
                    policy,
                    policyEntry,
                    $"Primitive '{definition.FhirTypeName}' inventory canonical " +
                    $"'{definition.Canonical}' does not match policy canonical " +
                    $"'{policyEntry.Canonical}'."));
                identityMatches = false;
            }

            if (topLevelVersionMatches &&
                !string.Equals(
                    definition.FhirVersion,
                    policyEntry.FhirVersion,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(CreatePolicyDiagnostic(
                    GeneratorDiagnosticCodes.PrimitivePolicyIdentityMismatch,
                    policy,
                    policyEntry,
                    $"Primitive '{definition.FhirTypeName}' inventory FHIR version " +
                    $"'{definition.FhirVersion}' does not match policy entry version " +
                    $"'{policyEntry.FhirVersion}'."));
                identityMatches = false;
            }

            if (topLevelVersionMatches && identityMatches)
            {
                matches.Add(new PrimitiveInventoryPolicyMatch(
                    definition,
                    policyEntry));
            }
        }

        foreach (var policyEntry in policy.Primitives
                     .Where(entry => !inventoryByType.ContainsKey(entry.FhirTypeName))
                     .OrderBy(entry => entry.FhirTypeName, StringComparer.Ordinal))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                GeneratorDiagnosticCodes.ExtraPrimitivePolicyEntry,
                policy,
                policyEntry,
                $"Primitive policy type '{policyEntry.FhirTypeName}' has no inventory " +
                "definition."));
        }

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
            .ThenBy(
                diagnostic => diagnostic.DefinitionCanonical,
                StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
        if (orderedDiagnostics.Length > 0)
        {
            return new GenerationResult<PrimitiveInventoryPolicyCoverage?>(
                null,
                orderedDiagnostics);
        }

        var coverage = new PrimitiveInventoryPolicyCoverage(
            inventory,
            policy,
            matches.OrderBy(
                match => match.Definition.FhirTypeName,
                StringComparer.Ordinal));
        return new GenerationResult<PrimitiveInventoryPolicyCoverage?>(
            coverage,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static GeneratorDiagnostic CreatePolicyDiagnostic(
        string code,
        ValidatedPrimitiveGenerationPolicy policy,
        ValidatedPrimitivePolicyEntry? entry,
        string message)
    {
        return new GeneratorDiagnostic(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            policy.SourceFile,
            entry?.Canonical,
            entry?.FhirVersion ?? policy.FhirVersion);
    }
}
