using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class PrimitiveRegistryModelBuilder
{
    private readonly PrimitiveRuntimeSymbolResolver _symbolResolver;

    public PrimitiveRegistryModelBuilder()
        : this(new PrimitiveRuntimeSymbolResolver())
    {
    }

    public PrimitiveRegistryModelBuilder(
        PrimitiveRuntimeSymbolResolver symbolResolver)
    {
        ArgumentNullException.ThrowIfNull(symbolResolver);
        _symbolResolver = symbolResolver;
    }

    public GenerationResult<PrimitiveRegistryCompositionModel?> Build(
        PrimitiveInventoryPolicyCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        if (!string.Equals(
                coverage.Policy.PrimitiveNamespace,
                PrimitiveWrapperModelBuilder.SupportedPrimitiveNamespace,
                StringComparison.Ordinal))
        {
            return Failure(
                coverage.Policy.SourceFile,
                $"Primitive namespace '{coverage.Policy.PrimitiveNamespace}' is not " +
                "supported for registry composition.");
        }

        var diagnostics = new List<GeneratorDiagnostic>();
        var entries = new List<PrimitiveRegistryEntryModel>();
        foreach (var match in coverage.Matches.Where(match => match.Policy.IsSupported))
        {
            var policy = match.Policy;
            if (policy.CodecKey is null || policy.ValidatorKey is null)
            {
                diagnostics.Add(new GeneratorDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitiveRegistryModel,
                    GeneratorDiagnosticSeverity.Error,
                    $"Supported primitive '{policy.FhirTypeName}' has no codec or " +
                    "validator key.",
                    coverage.Policy.SourceFile,
                    policy.Canonical,
                    policy.FhirVersion));
                continue;
            }

            var symbolsResult = _symbolResolver.Resolve(
                policy.CodecKey.Value,
                policy.ValidatorKey.Value,
                coverage.Policy.SourceFile,
                policy.FhirTypeName);
            diagnostics.AddRange(symbolsResult.Diagnostics);
            if (symbolsResult.Value is null)
            {
                continue;
            }

            entries.Add(new PrimitiveRegistryEntryModel(
                policy.FhirTypeName,
                policy.WrapperName!,
                policy.ClrValueType!,
                symbolsResult.Value.CodecSymbol,
                symbolsResult.Value.ValidatorSymbol));
        }

        if (diagnostics.Count > 0)
        {
            return new GenerationResult<PrimitiveRegistryCompositionModel?>(
                null,
                diagnostics
                    .OrderBy(item => item.Code, StringComparer.Ordinal)
                    .ThenBy(item => item.SourceFile, StringComparer.Ordinal)
                    .ThenBy(item => item.Message, StringComparer.Ordinal)
                    .ToArray());
        }

        return new GenerationResult<PrimitiveRegistryCompositionModel?>(
            new PrimitiveRegistryCompositionModel(
                PrimitiveWrapperModelBuilder.SupportedPrimitiveNamespace,
                entries.OrderBy(
                    entry => entry.FhirTypeName,
                    StringComparer.Ordinal)),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static GenerationResult<PrimitiveRegistryCompositionModel?> Failure(
        string sourceFile,
        string message)
    {
        return new GenerationResult<PrimitiveRegistryCompositionModel?>(
            null,
            [new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveRegistryModel,
                GeneratorDiagnosticSeverity.Error,
                message,
                sourceFile)]);
    }
}
