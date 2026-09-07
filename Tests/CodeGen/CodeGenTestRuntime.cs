using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Metadata;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.Core;

namespace MyFhirSdk.CodeGen.Tests;

internal static class CodeGenTestRuntime
{
    private static readonly Lazy<RuntimeContractView> Contract = new(LoadContract);

    internal static RuntimeContractView RuntimeContract => Contract.Value;

    internal static RoslynCompilationValidator CreateCompilationValidator() =>
        new(Net9RuntimeReferenceSetFactory.Create(typeof(FhirObject).Assembly.Location));

    internal static ModelMetadataIrBuilder CreateMetadataBuilder() =>
        new(RuntimeContract);

    internal static ComplexDatatypeGenerationPipeline CreateComplexDatatypePipeline() =>
        new(new ComplexDatatypeRenderer(), CreateCompilationValidator());

    internal static ResourceBackboneGenerationPipeline CreateResourceBackbonePipeline() =>
        new(
            new ComplexDatatypeRenderer(),
            new ResourceBackboneRenderer(),
            CreateCompilationValidator());

    internal static ModelMetadataGenerationPipeline CreateModelMetadataPipeline() =>
        new(RuntimeContract, CreateCompilationValidator());

    internal static ModelGenerationPipeline CreateModelPipeline(string repositoryRoot) =>
        new(repositoryRoot, RuntimeContract, CreateCompilationValidator());

    internal static PrimitiveGenerationPipeline CreatePrimitivePipeline(string repositoryRoot) =>
        new(repositoryRoot, CreateCompilationValidator());

    private static RuntimeContractView LoadContract()
    {
        var result = new RuntimeContractLoader().LoadAsync(Path.Combine(
                AppContext.BaseDirectory,
                "Policy",
                "runtime-contract.json"))
            .GetAwaiter()
            .GetResult();
        if (!result.IsSuccess || result.Value is null)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic =>
                    $"[{diagnostic.Code}] {diagnostic.Message}")));
        }
        return result.Value;
    }
}
