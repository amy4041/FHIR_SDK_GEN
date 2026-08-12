using MyFhirSdk.CodeGen.Cli;
using MyFhirSdk.CodeGen.Generation;

namespace MyFhirSdk.CodeGen;

public static class Program
{
    public static int Main(string[] args)
    {
        var repositoryRoot = RepositoryRootLocator.Find(
            Directory.GetCurrentDirectory());
        var generator = new FhirSdkGenerator(repositoryRoot);
        var cli = new GeneratorCli(generator, Console.Out, Console.Error);

        return cli.RunAsync(args).GetAwaiter().GetResult();
    }
}
