using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Cli;

public static class GeneratorExitCodeMapper
{
    public static int GetExitCode(
        IEnumerable<GeneratorDiagnostic> diagnostics,
        int fallback)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var codes = diagnostics
            .Select(diagnostic => diagnostic.Code)
            .ToHashSet(StringComparer.Ordinal);

        if (codes.Contains(GeneratorDiagnosticCodes.UnsafeOutputPath))
        {
            return 5;
        }

        if (codes.Contains(GeneratorDiagnosticCodes.CompilationFailure))
        {
            return 4;
        }

        if (codes.Any(code => code is
                GeneratorDiagnosticCodes.UnsupportedDefinition or
                GeneratorDiagnosticCodes.UnsupportedSlicing or
                GeneratorDiagnosticCodes.UnsupportedChoiceType or
                GeneratorDiagnosticCodes.UnsupportedContentReference or
                GeneratorDiagnosticCodes.MissingTypeMapping or
                GeneratorDiagnosticCodes.CSharpNameConflict))
        {
            return 3;
        }

        if (codes.Any(code => code is
                GeneratorDiagnosticCodes.InvalidInput or
                GeneratorDiagnosticCodes.FhirVersionMismatch or
                GeneratorDiagnosticCodes.MissingSnapshot or
                GeneratorDiagnosticCodes.MissingDifferential or
                GeneratorDiagnosticCodes.PrimitivePolicyReadFailure or
                GeneratorDiagnosticCodes.UnsupportedPrimitivePolicySchema or
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy or
                GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry or
                GeneratorDiagnosticCodes.UnknownPrimitivePolicyKey or
                GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy or
                GeneratorDiagnosticCodes.InvalidPrimitiveInventory or
                GeneratorDiagnosticCodes.DuplicatePrimitiveInventoryEntry or
                GeneratorDiagnosticCodes.MissingPrimitivePolicyEntry or
                GeneratorDiagnosticCodes.ExtraPrimitivePolicyEntry or
                GeneratorDiagnosticCodes.PrimitivePolicyIdentityMismatch or
                GeneratorDiagnosticCodes.InvalidPrimitiveWrapperModel or
                GeneratorDiagnosticCodes.InvalidPrimitiveRegistryModel))
        {
            return 2;
        }

        return fallback;
    }
}
