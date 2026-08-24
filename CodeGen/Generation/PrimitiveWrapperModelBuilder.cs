using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class PrimitiveWrapperModelBuilder
{
    public const string SupportedPrimitiveNamespace = "MyFhirSdk.Primitives";

    public GenerationResult<IReadOnlyList<PrimitiveWrapperModel>> Build(
        PrimitiveInventoryPolicyCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        if (!string.Equals(
                coverage.Policy.PrimitiveNamespace,
                SupportedPrimitiveNamespace,
                StringComparison.Ordinal))
        {
            return Failure(
                coverage.Policy.SourceFile,
                $"Primitive namespace '{coverage.Policy.PrimitiveNamespace}' is not " +
                $"supported for wrapper generation; expected " +
                $"'{SupportedPrimitiveNamespace}'.");
        }

        var models = coverage.Matches
            .Where(match => match.Policy.IsSupported)
            .Select(CreateModel)
            .OrderBy(model => model.FhirTypeName, StringComparer.Ordinal)
            .ToArray();

        return new GenerationResult<IReadOnlyList<PrimitiveWrapperModel>>(
            Array.AsReadOnly(models),
            Array.Empty<GeneratorDiagnostic>());
    }

    private static PrimitiveWrapperModel CreateModel(
        PrimitiveInventoryPolicyMatch match)
    {
        var policy = match.Policy;
        var literalKind = GetLiteralKind(policy);
        var constants = policy.PublicConstants
            .Select(constant => new PrimitiveWrapperConstantModel(
                constant.Name,
                constant.ClrType switch
                {
                    PrimitiveConstantClrType.Int32 =>
                        PrimitiveWrapperConstantClrType.Int32,
                    PrimitiveConstantClrType.Int64 =>
                        PrimitiveWrapperConstantClrType.Int64,
                    _ => throw new InvalidOperationException(
                        $"Unsupported primitive constant CLR type " +
                        $"'{constant.ClrType}'.")
                },
                constant.Value))
            .OrderBy(constant => constant.Name, StringComparer.Ordinal);

        return new PrimitiveWrapperModel(
            match.Definition.FhirTypeName,
            match.Definition.Canonical,
            match.Definition.FhirVersion,
            SupportedPrimitiveNamespace,
            policy.WrapperName!,
            policy.ClrValueType!,
            NormalizeDocumentation(
                match.Definition.Description,
                match.Definition.FhirTypeName),
            literalKind,
            policy.LiteralPropertyName,
            policy.ToStringBehavior switch
            {
                PrimitiveToStringBehavior.Inherited =>
                    PrimitiveWrapperToStringKind.Inherited,
                PrimitiveToStringBehavior.BooleanLowercase =>
                    PrimitiveWrapperToStringKind.BooleanLowercase,
                PrimitiveToStringBehavior.InvariantValue =>
                    PrimitiveWrapperToStringKind.InvariantValue,
                PrimitiveToStringBehavior.LiteralOrInvariantValue =>
                    PrimitiveWrapperToStringKind.LiteralOrInvariantValue,
                _ => throw new InvalidOperationException(
                    $"Unsupported primitive ToString behavior " +
                    $"'{policy.ToStringBehavior}'.")
            },
            constants);
    }

    private static PrimitiveWrapperLiteralKind GetLiteralKind(
        ValidatedPrimitivePolicyEntry policy)
    {
        if (!policy.LiteralConstructor)
        {
            return PrimitiveWrapperLiteralKind.None;
        }

        return policy.CodecKey switch
        {
            PrimitiveCodecKey.DecimalLiteral =>
                PrimitiveWrapperLiteralKind.Decimal,
            PrimitiveCodecKey.Integer64Literal =>
                PrimitiveWrapperLiteralKind.Integer64,
            _ => throw new InvalidOperationException(
                $"Primitive '{policy.FhirTypeName}' has no supported literal " +
                "constructor template.")
        };
    }

    private static string NormalizeDocumentation(
        string? description,
        string fhirTypeName)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return $"FHIR {fhirTypeName} primitive.";
        }

        return string.Join(
            ' ',
            description.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static GenerationResult<IReadOnlyList<PrimitiveWrapperModel>> Failure(
        string sourceFile,
        string message)
    {
        return new GenerationResult<IReadOnlyList<PrimitiveWrapperModel>>(
            Array.Empty<PrimitiveWrapperModel>(),
            [new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveWrapperModel,
                GeneratorDiagnosticSeverity.Error,
                message,
                sourceFile)]);
    }
}
