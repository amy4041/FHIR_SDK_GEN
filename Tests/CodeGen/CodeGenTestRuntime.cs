using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Metadata;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Writing;

namespace MyFhirSdk.CodeGen.Tests;

internal static class CodeGenTestRuntime
{
    private static readonly Lazy<RuntimeContractView> Contract = new(LoadContract);
    private static readonly Lazy<RuntimeReferenceSet> References = new(LoadReferences);

    internal static RuntimeContractView RuntimeContract => Contract.Value;

    internal static RuntimeReferenceSet RuntimeReferences => References.Value;

    internal static RoslynCompilationValidator CreateCompilationValidator() =>
        new(RuntimeReferences);

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
        new(CreateDevelopmentSafetyContext(repositoryRoot), RuntimeContract, CreateCompilationValidator());

    internal static PrimitiveGenerationPipeline CreatePrimitivePipeline(string repositoryRoot) =>
        new(
            CreateDevelopmentSafetyContext(repositoryRoot),
            RuntimeContract,
            CreateCompilationValidator());

    private static OutputSafetyContext CreateDevelopmentSafetyContext(
        string repositoryRoot) =>
        new OutputSafetyContext(AppContext.BaseDirectory)
            .WithDevelopmentRepository(repositoryRoot);

    private static RuntimeReferenceSet LoadReferences()
    {
        var paths = new ToolAssetResolver(AppContext.BaseDirectory)
            .ResolveRuntimeReferencePaths(
                RuntimeContract,
                new ToolAssetOverrides());
        var result = new RuntimeReferenceService().Resolve(
            RuntimeContract,
            paths,
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());
        if (!result.IsSuccess || result.Value is null)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic =>
                    $"[{diagnostic.Code}] {diagnostic.Message}")));
        }
        return result.Value;
    }

    private static RuntimeContractView LoadContract()
    {
        var result = new RuntimeContractLoader().LoadAsync(GetContractPath())
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

    private static string GetContractPath() => Path.Combine(
        AppContext.BaseDirectory,
        "Policy",
        "runtime-contract.json");
}
