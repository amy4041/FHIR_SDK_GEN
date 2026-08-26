using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Compilation;

namespace MyFhirSdk.CodeGen.Models;

public sealed class PrimitiveGenerationBatch
{
    public PrimitiveGenerationBatch(
        IEnumerable<GeneratedSource> sources,
        PrimitiveGenerationManifestModel manifest,
        string renderedManifest)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(renderedManifest);

        Sources = new ReadOnlyCollection<GeneratedSource>(
            sources.OrderBy(item => item.FileName, StringComparer.Ordinal).ToArray());
        Manifest = manifest;
        Artifacts = new ReadOnlyCollection<GeneratedArtifact>(
            Sources.Select(source => new GeneratedArtifact(
                    source.FileName,
                    source.Source))
                .Append(new GeneratedArtifact(manifest.FileName, renderedManifest))
                .OrderBy(item => item.FileName, StringComparer.Ordinal)
                .ToArray());
    }

    public IReadOnlyList<GeneratedSource> Sources { get; }
    public PrimitiveGenerationManifestModel Manifest { get; }
    public IReadOnlyList<GeneratedArtifact> Artifacts { get; }
}
