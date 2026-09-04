using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen;

public static class Program
{
    public static int Main(string[] args)
    {
        var repositoryRoot = RepositoryRootLocator.Find(
            Directory.GetCurrentDirectory());
        var primitivePipeline = new PrimitiveGenerationPipeline(repositoryRoot);
        var modelPipeline = new ModelGenerationPipeline(repositoryRoot);
        var cli = new GeneratorCli(
            Console.Out,
            Console.Error,
            primitivePipeline: primitivePipeline,
            modelPipeline: modelPipeline);

        return cli.RunAsync(args).GetAwaiter().GetResult();
    }
}
