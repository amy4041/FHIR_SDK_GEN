namespace MyFhirSdk.CodeGen;

public static class Program
{
    private const string Usage =
        """
        Usage:
          dotnet run --project CodeGen/MyFhirSdk.CodeGen.csproj -- \
            --input <path> \
            --output <path> \
            --namespace <namespace> \
            --fhir-version <version> \
            --type <fhir-type>
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 1 &&
            string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase))
        {
            Console.Out.WriteLine(Usage);
            return 0;
        }

        Console.Error.WriteLine(Usage);
        Console.Error.WriteLine();
        Console.Error.WriteLine("FHIR SDK Generator arguments are not implemented yet.");
        return 1;
    }
}
