using System.Collections.ObjectModel;
using MyFhirSdk.CodeGen.Compilation;

namespace MyFhirSdk.CodeGen.Models;

public sealed class ResourceBackboneGenerationBatch
{
    internal ResourceBackboneGenerationBatch(IEnumerable<GeneratedSource> sources)
    {
        Sources = new ReadOnlyCollection<GeneratedSource>(sources.ToArray());
        Artifacts = new ReadOnlyCollection<GeneratedArtifact>(
            Sources.Select(source => new GeneratedArtifact(source.FileName, source.Source)).ToArray());
    }

    public IReadOnlyList<GeneratedSource> Sources { get; }

    public IReadOnlyList<GeneratedArtifact> Artifacts { get; }
}
