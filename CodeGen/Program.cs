using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen;

public static class Program
{
    public static int Main(string[] args)
    {
        var assetResolver = new ToolAssetResolver(AppContext.BaseDirectory);
        var commandLineParser = new GeneratorCommandLineParser(assetResolver);
        var parseResult = commandLineParser.Parse(args);
        if (parseResult.ShowHelp || !parseResult.IsSuccess)
        {
            return new GeneratorCli(
                    Console.Out,
                    Console.Error,
                    commandLineParser)
                .RunAsync(args)
                .GetAwaiter()
                .GetResult();
        }

        var assetOverrides = parseResult.AssetOverrides ?? new ToolAssetOverrides();
        var runtimeContractPath = assetResolver.ResolveRuntimeContractPath(
            assetOverrides);
        var contractResult = new RuntimeContractLoader().LoadAsync(runtimeContractPath)
            .GetAwaiter()
            .GetResult();
        if (!contractResult.IsSuccess || contractResult.Value is null)
        {
            foreach (var diagnostic in contractResult.Diagnostics)
            {
                Console.Error.WriteLine(
                    $"[{diagnostic.Code}] {diagnostic.Severity}: " +
                    $"{diagnostic.SourceFile}: {diagnostic.Message}");
            }
            return GeneratorExitCodeMapper.GetExitCode(
                contractResult.Diagnostics,
                fallback: 2);
        }

        var runtimeReferencePaths = assetResolver.ResolveRuntimeReferencePaths(
            contractResult.Value,
            assetOverrides);
        var referenceResult = new RuntimeReferenceService().Resolve(
            contractResult.Value,
            runtimeReferencePaths,
            RuntimeReferenceService.GetTrustedPlatformAssemblyPaths());
        if (!referenceResult.IsSuccess || referenceResult.Value is null)
        {
            foreach (var diagnostic in referenceResult.Diagnostics)
            {
                Console.Error.WriteLine(
                    $"[{diagnostic.Code}] {diagnostic.Severity}: " +
                    $"{diagnostic.SourceFile}: {diagnostic.Message}");
            }
            return GeneratorExitCodeMapper.GetExitCode(
                referenceResult.Diagnostics,
                fallback: 2);
        }

        var inputPath = parseResult.ModelOptions?.PackagePath ??
            parseResult.PrimitiveOptions!.DefinitionsPath;
        var policyPaths = parseResult.ModelOptions is { } modelOptions
            ? new[]
            {
                modelOptions.PrimitivePolicyPath,
                modelOptions.OwnershipPolicyPath,
                modelOptions.ModelIrPolicyPaths.NamingPolicyPath,
                modelOptions.ModelIrPolicyPaths.BackbonePolicyPath,
                modelOptions.ModelIrPolicyPaths.ChoicePolicyPath,
                modelOptions.ValidationPolicyPath
            }
            : [parseResult.PrimitiveOptions!.PolicyPath];
        var outputSafetyContext = assetResolver.CreateOutputSafetyContext(
            inputPath,
            runtimeContractPath,
            runtimeReferencePaths,
            policyPaths);
        var compilationValidator = new RoslynCompilationValidator(referenceResult.Value);
        var primitivePipeline = new PrimitiveGenerationPipeline(
            outputSafetyContext,
            compilationValidator);
        var modelPipeline = new ModelGenerationPipeline(
            outputSafetyContext,
            contractResult.Value,
            compilationValidator);
        var cli = new GeneratorCli(
            Console.Out,
            Console.Error,
            commandLineParser,
            primitivePipeline: primitivePipeline,
            modelPipeline: modelPipeline);

        return cli.RunAsync(args).GetAwaiter().GetResult();
    }
}
