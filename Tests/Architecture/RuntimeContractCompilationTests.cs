using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.Core;

namespace MyFhirSdk.Tests.Architecture;

public sealed class RuntimeContractCompilationTests
{
    [Fact]
    public void ExternalGeneratedModelsAndSdkConsumerCanCompile()
    {
        const string source = """
            using MyFhirSdk.Core;
            using MyFhirSdk.Serialization;
            using MyFhirSdk.Validation;

            public sealed class GeneratedDataType : DataType
            {
            }

            public sealed class GeneratedBackboneType : BackboneType
            {
            }

            public sealed class GeneratedBackboneElement : BackboneElement
            {
            }

            public sealed class GeneratedPrimitive : PrimitiveType<string>
            {
                public GeneratedPrimitive()
                {
                }

                public GeneratedPrimitive(string value)
                    : base(value)
                {
                }
            }

            public sealed class GeneratedResource : DomainResource
            {
                public override string ResourceType => "GeneratedResource";
            }

            public static class ExternalConsumer
            {
                public static ValidationResult Exercise(
                    IFhirSerializer serializer,
                    IFhirParser parser,
                    IFhirValidator validator)
                {
                    var resource = new GeneratedResource
                    {
                        Meta = new Meta(),
                        Text = new Narrative()
                    };
                    resource.Extension.Add(new Extension
                    {
                        Url = "https://example.test/extension",
                        Value = new GeneratedPrimitive("value")
                    });

                    var json = serializer.Serialize(resource);
                    var parsed = parser.Parse<GeneratedResource>(json);
                    return validator.Validate(parsed);
                }

                public static FhirSdkException CreateSdkException() =>
                    new("external consumer failure");
            }
            """;

        var errors = CompileExternalConsumer(source);

        Assert.Empty(errors);
    }

    public static TheoryData<string, string[]> InaccessibleRuntimeApis => new()
    {
        {
            """
            using MyFhirSdk.Primitives;

            public static class ExternalConsumer
            {
                public static bool Validate(FhirString value) => value.IsValid();
            }
            """,
            ["CS1061"]
        },
        {
            """
            using MyFhirSdk.Primitives;

            public static class ExternalConsumer
            {
                public static object Cast(object value) =>
                    (IFhirValidatablePrimitive)value;
            }
            """,
            ["CS0122", "CS0246"]
        },
        {
            """
            using MyFhirSdk.Primitives;

            public static class ExternalConsumer
            {
                public static object GetRegistryType() => typeof(PrimitiveRegistry);
            }
            """,
            ["CS0122", "CS0246"]
        },
        {
            """
            using MyFhirSdk.Primitives;

            public static class ExternalConsumer
            {
                public static object CreateCodec() => new DecimalPrimitiveCodec();
            }
            """,
            ["CS0122", "CS0246"]
        }
    };

    [Theory]
    [MemberData(nameof(InaccessibleRuntimeApis))]
    public void InternalRuntimeApisCannotBeCompiledByExternalConsumer(
        string source,
        string[] expectedDiagnosticIds)
    {
        var errors = CompileExternalConsumer(source);

        Assert.NotEmpty(errors);
        Assert.Contains(
            errors,
            diagnostic => expectedDiagnosticIds.Contains(
                diagnostic.Id,
                StringComparer.Ordinal));
    }

    private static Diagnostic[] CompileExternalConsumer(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            $"ExternalConsumer_{Guid.NewGuid():N}",
            [syntaxTree],
            CreateMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
    }

    private static IEnumerable<MetadataReference> CreateMetadataReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");
        var assemblyPaths = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(FhirObject).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return assemblyPaths.Select(
            assemblyPath => MetadataReference.CreateFromFile(assemblyPath));
    }
}
