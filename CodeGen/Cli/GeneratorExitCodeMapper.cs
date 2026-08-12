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
                GeneratorDiagnosticCodes.MissingDifferential))
        {
            return 2;
        }

        return fallback;
    }
}
