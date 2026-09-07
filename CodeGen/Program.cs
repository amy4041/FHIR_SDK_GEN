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
        var runtimeAssemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            contractResult.Value.CompilerReference.Assembly.Name + ".dll");
        var compilationValidator = new RoslynCompilationValidator(
            Net9RuntimeReferenceSetFactory.Create(runtimeAssemblyPath));
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
            primitivePipeline: primitivePipeline,
            modelPipeline: modelPipeline);

        return cli.RunAsync(args).GetAwaiter().GetResult();
    }
}
