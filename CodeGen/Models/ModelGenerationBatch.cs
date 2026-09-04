using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Compilation;

namespace MyFhirSdk.CodeGen.Models;

public sealed class ModelGenerationBatch
{
    internal ModelGenerationBatch(
        IEnumerable<GeneratedSource> sources,
        ModelGenerationManifestModel manifest,
        string renderedManifest)
    {
        Sources = new ReadOnlyCollection<GeneratedSource>(sources.OrderBy(x => x.FileName, StringComparer.Ordinal).ToArray());
        Manifest = manifest;
        Artifacts = new ReadOnlyCollection<GeneratedArtifact>(Sources
            .Select(x => new GeneratedArtifact(x.FileName, x.Source))
            .Append(new GeneratedArtifact(ModelGenerationManifestModel.FileName, renderedManifest))
            .OrderBy(x => x.FileName, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<GeneratedSource> Sources { get; }
    public ModelGenerationManifestModel Manifest { get; }
    public IReadOnlyList<GeneratedArtifact> Artifacts { get; }
}

