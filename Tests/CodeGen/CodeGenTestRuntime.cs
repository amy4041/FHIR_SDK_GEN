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
    private static readonly Lazy<IReadOnlyList<GeneratedSource>> ModelAuxiliary = new(() =>
    {
        var result = CreatePrimitivePipeline().BuildAsync(new(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FhirPackages", "R5", "hl7.fhir.r5.core-5.0.0.tgz"),
            Path.Combine(AppContext.BaseDirectory, "Policy", "primitive-generation-policy.json"),
            Path.GetTempPath(), "5.0.0", "hl7.fhir.r5.core", "5.0.0",
            PrimitiveGenerationPipeline.DefaultCodeGenVersion)).GetAwaiter().GetResult();
        if (!result.IsSuccess || result.Value is null)
            throw new InvalidOperationException(string.Join("\n", result.Diagnostics));
        return result.Value.Sources.Where(source =>
                !source.FileName.EndsWith("PrimitiveRegistry.Composition.g.cs", StringComparison.Ordinal))
            .ToArray();
    });

    internal static RuntimeContractView RuntimeContract => Contract.Value;

    internal static RuntimeReferenceSet RuntimeReferences => References.Value;

    internal static RoslynCompilationValidator CreateCompilationValidator() =>
        new(RuntimeReferences);

    internal static RoslynCompilationValidator CreateModelCompilationValidator(
        MyFhirSdk.CodeGen.Ir.ModelIrBatch? ir = null)
    {
        var auxiliary = ModelAuxiliary.Value.ToList();
        if (ir is not null && ir.Declarations.Any(declaration => declaration.Members.Any(member =>
                member.TypeAlternatives.Any(type => type.ClrType == "MyFhirSdk.Types.SimpleQuantity"))))
            auxiliary.Add(ModelCompilerSourceAsset.Read(contract: RuntimeContract));
        return CreateCompilationValidator().WithAdditionalSources(auxiliary);
    }

    internal static ModelMetadataIrBuilder CreateMetadataBuilder() =>
        new(RuntimeContract);

    internal static ComplexDatatypeGenerationPipeline CreateComplexDatatypePipeline(MyFhirSdk.CodeGen.Ir.ModelIrBatch ir) =>
        new(new ComplexDatatypeRenderer(), CreateModelCompilationValidator(ir));

    internal static ResourceBackboneGenerationPipeline CreateResourceBackbonePipeline(MyFhirSdk.CodeGen.Ir.ModelIrBatch ir) =>
        new(
            new ComplexDatatypeRenderer(),
            new ResourceBackboneRenderer(),
            CreateModelCompilationValidator(ir));

    internal static ModelMetadataGenerationPipeline CreateModelMetadataPipeline(MyFhirSdk.CodeGen.Ir.ModelIrBatch ir) =>
        new(RuntimeContract, CreateModelCompilationValidator(ir));

    internal static ModelGenerationPipeline CreateModelPipeline() =>
        new(CreateSafetyContext(), RuntimeContract, CreateCompilationValidator());

    internal static PrimitiveGenerationPipeline CreatePrimitivePipeline() =>
        new(
            CreateSafetyContext(),
            RuntimeContract,
            CreateCompilationValidator());

    private static OutputSafetyContext CreateSafetyContext() =>
        new(AppContext.BaseDirectory);

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
