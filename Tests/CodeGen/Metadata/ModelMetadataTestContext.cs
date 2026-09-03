using MyFhirSdk.CodeGen.Graph;
using MyFhirSdk.CodeGen.Ir;
using MyFhirSdk.CodeGen.Tests.Generation;

namespace MyFhirSdk.CodeGen.Tests.Metadata;

internal static class ModelMetadataTestContext
{
    internal static async Task<ModelIrBatch> BuildFullModelIrAsync()
    {
        var graph = await ComplexDatatypeTestContext.BuildOfficialGraphAsync();
        var names = graph.Nodes
            .Where(node => node.Disposition == DefinitionDependencyNodeDisposition.GeneratedModel)
            .Select(node => node.FhirTypeName)
            .ToArray();
        return (await ComplexDatatypeTestContext.BuildOfficialIrAsync(names)).Ir;
    }
}
