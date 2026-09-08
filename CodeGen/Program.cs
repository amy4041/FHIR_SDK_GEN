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
        var commandLineParser = new GeneratorCommandLineParser();
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

        var contractResult = new RuntimeContractLoader().LoadAsync(Path.Combine(
                AppContext.BaseDirectory,
                "Policy",
                "runtime-contract.json"))
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

        var repositoryRoot = RepositoryRootLocator.Find(
            Directory.GetCurrentDirectory());
        var referenceResult = new RuntimeReferenceService().ResolvePackageOwned(
            contractResult.Value,
            AppContext.BaseDirectory);
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

        var compilationValidator = new RoslynCompilationValidator(referenceResult.Value);
        var primitivePipeline = new PrimitiveGenerationPipeline(
            repositoryRoot,
            compilationValidator);
        var modelPipeline = new ModelGenerationPipeline(
            repositoryRoot,
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
