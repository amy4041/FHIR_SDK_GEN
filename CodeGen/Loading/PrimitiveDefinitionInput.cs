using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Loading;

public enum PrimitiveDefinitionInputKind
{
    Directory,
    PackageArchive
}

public sealed record PrimitiveDefinitionInput(string Path, PrimitiveDefinitionInputKind Kind)
{
    public static GenerationResult<PrimitiveDefinitionInput?> Resolve(
        string path,
        PrimitiveDefinitionInputKind? expectedKind = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                PrimitiveDefinitionInputKind? kind = System.IO.Directory.Exists(fullPath)
                    ? PrimitiveDefinitionInputKind.Directory
                    : File.Exists(fullPath) && string.Equals(
                        System.IO.Path.GetExtension(fullPath), ".tgz", StringComparison.OrdinalIgnoreCase)
                        ? PrimitiveDefinitionInputKind.PackageArchive : null;
                if (kind is not null && (expectedKind is null || kind == expectedKind))
                {
                    return new(new PrimitiveDefinitionInput(fullPath, kind.Value), []);
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or
            NotSupportedException or UnauthorizedAccessException)
        {
            // Classification is based on the supplied path, never on exception text or archive contents.
        }
        return new(null, [new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.InvalidInput,
            GeneratorDiagnosticSeverity.Error,
            "Primitive input must be an existing directory or .tgz file matching the declared input kind.",
            path ?? "<primitive-input>")]);
    }
}
