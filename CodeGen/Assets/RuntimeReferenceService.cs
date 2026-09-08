using System.Security;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Assets;

public sealed class RuntimeReferenceService
{
    public GenerationResult<RuntimeReferenceSet?> ResolvePackageOwned(
        RuntimeContractView contract,
        string toolRoot)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolRoot);

        var reference = contract.CompilerReference;
        var path = Path.Combine(
            toolRoot,
            "Assets",
            "RuntimeReferences",
            reference.TargetFramework,
            reference.Assembly.Name + ".dll");
        return Resolve(contract, [path], GetTrustedPlatformAssemblyPaths());
    }

    public GenerationResult<RuntimeReferenceSet?> Resolve(
        RuntimeContractView contract,
        IEnumerable<string> runtimeReferencePaths,
        IEnumerable<string> trustedPlatformAssemblyPaths)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(runtimeReferencePaths);
        ArgumentNullException.ThrowIfNull(trustedPlatformAssemblyPaths);

        var diagnostics = new List<GeneratorDiagnostic>();
        var runtimeReferences = ReadRuntimeReferences(
            contract,
            runtimeReferencePaths,
            diagnostics);
        var trustedPlatformReferences = ReadTrustedPlatformReferences(
            contract,
            trustedPlatformAssemblyPaths,
            diagnostics);
        AddDuplicateIdentityDiagnostics(runtimeReferences, "Runtime contract", diagnostics);
        AddDuplicateIdentityDiagnostics(trustedPlatformReferences, "trusted platform", diagnostics);

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
        if (orderedDiagnostics.Length > 0)
        {
            return new GenerationResult<RuntimeReferenceSet?>(null, orderedDiagnostics);
        }

        var runtimeReference = runtimeReferences.Single();
        return new GenerationResult<RuntimeReferenceSet?>(
            new RuntimeReferenceSet(
                contract.TargetFramework,
                new RuntimeReferenceLogicalIdentity(
                    contract.TargetFramework,
                    runtimeReference.Assembly),
                contract.DescriptorSha256,
                runtimeReference.Sha256!,
                trustedPlatformReferences,
                runtimeReferences),
            Array.Empty<GeneratorDiagnostic>());
    }

    public static IReadOnlyList<string> GetTrustedPlatformAssemblyPaths()
    {
        var value = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<ResolvedRuntimeReference> ReadRuntimeReferences(
        RuntimeContractView contract,
        IEnumerable<string> paths,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var reference = contract.CompilerReference;
        var source = RuntimeSource(reference.LogicalName);
        var inputPaths = paths.ToArray();
        if (inputPaths.Length == 0)
        {
            diagnostics.Add(Diagnostic(
                GeneratorDiagnosticCodes.RuntimeReferenceMissing,
                source,
                $"Runtime compiler reference '{reference.LogicalName}' is missing."));
            return Array.Empty<ResolvedRuntimeReference>();
        }

        var results = new List<ResolvedRuntimeReference>();
        foreach (var path in inputPaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceMissing,
                    source,
                    $"Runtime compiler reference '{reference.LogicalName}' is missing."));
                continue;
            }

            ResolvedRuntimeReference actual;
            try
            {
                actual = RuntimeReferenceMetadataReader.Read(
                    path,
                    RuntimeReferenceKind.RuntimeContract,
                    includeSha256: true);
            }
            catch (Exception exception) when (IsReferenceReadFailure(exception))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceReadFailure,
                    source,
                    $"Runtime compiler reference '{reference.LogicalName}' is unreadable or corrupt."));
                continue;
            }

            results.Add(actual);
            if (!AssemblyIdentityEquals(actual.Assembly, reference.Assembly))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceIdentityMismatch,
                    source,
                    $"Runtime compiler reference '{reference.LogicalName}' has assembly identity " +
                    $"'{actual.AssemblyIdentity}'; expected '{FormatIdentity(reference.Assembly)}'."));
            }
            if (!string.Equals(
                    actual.TargetFramework,
                    reference.TargetFramework,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceTargetFrameworkMismatch,
                    source,
                    $"Runtime compiler reference '{reference.LogicalName}' targets " +
                    $"'{actual.TargetFramework ?? "<missing>"}'; expected '{reference.TargetFramework}'."));
            }
            if (!string.Equals(actual.Sha256, reference.Sha256, StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceHashMismatch,
                    source,
                    $"Runtime compiler reference '{reference.LogicalName}' has SHA-256 " +
                    $"'{actual.Sha256}'; expected '{reference.Sha256}'."));
            }
        }
        return results;
    }

    private static IReadOnlyList<ResolvedRuntimeReference> ReadTrustedPlatformReferences(
        RuntimeContractView contract,
        IEnumerable<string> paths,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var inputs = new HashSet<string>(pathComparer);
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                inputs.Add(Path.GetFullPath(path));
            }
            catch (Exception exception) when (exception is ArgumentException or
                NotSupportedException or PathTooLongException or SecurityException)
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceReadFailure,
                    "<trusted-platform-assemblies>",
                    "A trusted platform assembly path is invalid."));
            }
        }
        if (inputs.Count == 0)
        {
            diagnostics.Add(Diagnostic(
                GeneratorDiagnosticCodes.RuntimeReferenceMissing,
                "<trusted-platform-assemblies>",
                "Trusted platform assemblies are unavailable."));
            return Array.Empty<ResolvedRuntimeReference>();
        }

        var results = new List<ResolvedRuntimeReference>();
        foreach (var path in inputs)
        {
            if (!File.Exists(path))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceMissing,
                    "<trusted-platform-assemblies>",
                    "A trusted platform assembly is missing."));
                continue;
            }
            try
            {
                var resolved = RuntimeReferenceMetadataReader.Read(
                    path,
                    RuntimeReferenceKind.TrustedPlatform,
                    includeSha256: false);
                // The explicit contract reference always wins over an assembly already present
                // in the host TPA list, so a loaded SDK can never become an implicit fallback.
                if (!string.Equals(
                        resolved.Assembly.Name,
                        contract.CompilerReference.Assembly.Name,
                        StringComparison.Ordinal))
                {
                    results.Add(resolved);
                }
            }
            catch (Exception exception) when (IsReferenceReadFailure(exception))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.RuntimeReferenceReadFailure,
                    "<trusted-platform-assemblies>",
                    "A trusted platform assembly is unreadable or corrupt."));
            }
        }
        return results;
    }

    private static void AddDuplicateIdentityDiagnostics(
        IEnumerable<ResolvedRuntimeReference> references,
        string referenceKind,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        foreach (var duplicate in references
                     .GroupBy(
                         reference => reference.LogicalIdentity.ToString(),
                         StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            diagnostics.Add(Diagnostic(
                GeneratorDiagnosticCodes.DuplicateRuntimeReferenceIdentity,
                "<runtime-references>",
                $"Duplicate {referenceKind} assembly identity '{duplicate.Key}'."));
        }
    }

    private static bool AssemblyIdentityEquals(
        RuntimeAssemblyIdentity actual,
        RuntimeAssemblyIdentity expected) =>
        string.Equals(actual.Name, expected.Name, StringComparison.Ordinal) &&
        string.Equals(actual.Version, expected.Version, StringComparison.Ordinal) &&
        string.Equals(actual.PublicKeyToken, expected.PublicKeyToken, StringComparison.Ordinal);

    private static string FormatIdentity(RuntimeAssemblyIdentity identity) =>
        $"{identity.Name}, Version={identity.Version}, PublicKeyToken={identity.PublicKeyToken}";

    private static bool IsReferenceReadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or
            BadImageFormatException or ArgumentException or NotSupportedException;

    private static string RuntimeSource(string logicalName) =>
        $"<runtime-reference:{logicalName}>";

    private static GeneratorDiagnostic Diagnostic(
        string code,
        string sourceFile,
        string message) =>
        new(code, GeneratorDiagnosticSeverity.Error, message, sourceFile);
}
