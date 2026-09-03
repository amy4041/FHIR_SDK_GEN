using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Metadata;

namespace MyFhirSdk.CodeGen.Models;

public sealed class ModelMetadataGenerationBatch
{
    internal ModelMetadataGenerationBatch(
        ModelMetadataIrBatch metadata,
        IEnumerable<GeneratedSource> sources,
        IEnumerable<GeneratedSource> compilationSources)
    {
        Metadata = metadata;
        Sources = new ReadOnlyCollection<GeneratedSource>(sources.ToArray());
        CompilationSources = new ReadOnlyCollection<GeneratedSource>(compilationSources.ToArray());
        Artifacts = new ReadOnlyCollection<GeneratedArtifact>(Sources
            .Select(source => new GeneratedArtifact(source.FileName, source.Source))
            .ToArray());
    }

    public ModelMetadataIrBatch Metadata { get; }

    public IReadOnlyList<GeneratedSource> Sources { get; }

    public IReadOnlyList<GeneratedSource> CompilationSources { get; }

    public IReadOnlyList<GeneratedArtifact> Artifacts { get; }
}
