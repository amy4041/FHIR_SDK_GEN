using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Policy;

namespace MyFhirSdk.CodeGen.Generation;

public sealed class PrimitiveRuntimeSymbolResolver
{
    public GenerationResult<PrimitiveRuntimeSymbols?> Resolve(
        PrimitiveCodecKey codecKey,
        PrimitiveValidatorKey validatorKey,
        string sourceFile,
        string fhirTypeName)
    {
        var codecSymbol = ResolveCodec(codecKey);
        var validatorSymbol = ResolveValidator(validatorKey);
        if (codecSymbol is not null && validatorSymbol is not null)
        {
            return new GenerationResult<PrimitiveRuntimeSymbols?>(
                new PrimitiveRuntimeSymbols(codecSymbol, validatorSymbol),
                Array.Empty<GeneratorDiagnostic>());
        }

        var unknownParts = new List<string>();
        if (codecSymbol is null)
        {
            unknownParts.Add($"codec key '{codecKey}'");
        }

        if (validatorSymbol is null)
        {
            unknownParts.Add($"validator key '{validatorKey}'");
        }

        return new GenerationResult<PrimitiveRuntimeSymbols?>(
            null,
            [new GeneratorDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveRegistryModel,
                GeneratorDiagnosticSeverity.Error,
                $"Primitive '{fhirTypeName}' has unsupported Runtime " +
                $"{string.Join(" and ", unknownParts)}.",
                sourceFile)]);
    }

    private static string? ResolveCodec(PrimitiveCodecKey key)
    {
        return key switch
        {
            PrimitiveCodecKey.String => "PrimitiveCodecs.String",
            PrimitiveCodecKey.Boolean => "PrimitiveCodecs.Boolean",
            PrimitiveCodecKey.Integer => "PrimitiveCodecs.Integer",
            PrimitiveCodecKey.DecimalLiteral => "PrimitiveCodecs.Decimal",
            PrimitiveCodecKey.Integer64Literal => "PrimitiveCodecs.Integer64",
            _ => null
        };
    }

    private static string? ResolveValidator(PrimitiveValidatorKey key)
    {
        return key switch
        {
            PrimitiveValidatorKey.Base64Binary => "PrimitiveValidators.Base64Binary",
            PrimitiveValidatorKey.Boolean => "PrimitiveValidators.Boolean",
            PrimitiveValidatorKey.Canonical => "PrimitiveValidators.Canonical",
            PrimitiveValidatorKey.Code => "PrimitiveValidators.Code",
            PrimitiveValidatorKey.Date => "PrimitiveValidators.Date",
            PrimitiveValidatorKey.DateTime => "PrimitiveValidators.DateTime",
            PrimitiveValidatorKey.Decimal => "PrimitiveValidators.Decimal",
            PrimitiveValidatorKey.Id => "PrimitiveValidators.Id",
            PrimitiveValidatorKey.Instant => "PrimitiveValidators.Instant",
            PrimitiveValidatorKey.Integer => "PrimitiveValidators.Integer",
            PrimitiveValidatorKey.Integer64 => "PrimitiveValidators.Integer64",
            PrimitiveValidatorKey.Markdown => "PrimitiveValidators.Markdown",
            PrimitiveValidatorKey.PositiveInt => "PrimitiveValidators.PositiveInt",
            PrimitiveValidatorKey.String => "PrimitiveValidators.String",
            PrimitiveValidatorKey.UnsignedInt => "PrimitiveValidators.UnsignedInt",
            PrimitiveValidatorKey.Uri => "PrimitiveValidators.Uri",
            PrimitiveValidatorKey.Url => "PrimitiveValidators.Url",
            _ => null
        };
    }
}

public sealed record PrimitiveRuntimeSymbols(
    string CodecSymbol,
    string ValidatorSymbol);
