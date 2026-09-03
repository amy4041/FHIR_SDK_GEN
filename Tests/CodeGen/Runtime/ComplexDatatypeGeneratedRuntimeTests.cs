using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Tests.Generation;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Validation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

public sealed class ComplexDatatypeGeneratedRuntimeTests
{
    [Fact]
    public async Task ExistingFiveMvpTypes_PreservePublicRuntimeShape()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync(
            "Address",
            "Coding",
            "HumanName",
            "Identifier",
            "Period");
        var generationResult = new ComplexDatatypeGenerationPipeline().Generate(ir);
        Assert.True(
            generationResult.IsSuccess,
            ComplexDatatypeTestContext.Describe(generationResult.Diagnostics));
        var assembly = CompileGeneratedAssembly(
            Assert.IsType<ComplexDatatypeGenerationBatch>(generationResult.Value).Sources);
        var runtimeTypes = new[]
        {
            typeof(MyFhirSdk.Types.Address),
            typeof(MyFhirSdk.Types.Coding),
            typeof(MyFhirSdk.Types.HumanName),
            typeof(MyFhirSdk.Types.Identifier),
            typeof(MyFhirSdk.Types.Period)
        };

        foreach (var runtimeType in runtimeTypes)
        {
            var generatedType = assembly.GetType(runtimeType.FullName!, throwOnError: true)!;
            Assert.Equal(runtimeType.IsAbstract, generatedType.IsAbstract);
            Assert.Equal(runtimeType.IsSealed, generatedType.IsSealed);
            Assert.Equal(runtimeType.BaseType?.FullName, generatedType.BaseType?.FullName);
            Assert.Equal(PublicShape(runtimeType), PublicShape(generatedType));
        }
    }

    [Fact]
    public async Task GeneratedPeriod_RoundTripsAndParticipatesInValidationTraversal()
    {
        var (_, ir) = await ComplexDatatypeTestContext.BuildOfficialIrAsync("Period");
        var generationResult = new ComplexDatatypeGenerationPipeline().Generate(ir);
        Assert.True(
            generationResult.IsSuccess,
            ComplexDatatypeTestContext.Describe(generationResult.Diagnostics));
        var batch = Assert.IsType<ComplexDatatypeGenerationBatch>(generationResult.Value);
        var assembly = CompileGeneratedAssembly(batch.Sources.Append(new GeneratedSource(
            "GeneratedDatatypeContainer.cs",
            """
            using MyFhirSdk.Core;
            using MyFhirSdk.Types;

            namespace MyFhirSdk.C4RuntimeFixtures;

            public sealed class GeneratedDatatypeContainer : Resource
            {
                public override string ResourceType => nameof(GeneratedDatatypeContainer);

                public Period? Value { get; set; }
            }
            """)).ToArray());
        var containerType = assembly.GetType(
            "MyFhirSdk.C4RuntimeFixtures.GeneratedDatatypeContainer",
            throwOnError: true)!;
        var periodType = assembly.GetType("MyFhirSdk.Types.Period", throwOnError: true)!;
        var period = Activator.CreateInstance(periodType)!;
        periodType.GetProperty("Start")!.SetValue(
            period,
            new FhirDateTime("2020-01-01T00:00:00Z"));
        var container = Assert.IsAssignableFrom<Resource>(Activator.CreateInstance(containerType));
        containerType.GetProperty("Value")!.SetValue(container, period);
        var serializer = new FhirJsonSerializer();

        var firstJson = serializer.Serialize(container);
        var parseMethod = typeof(FhirJsonParser)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == nameof(FhirJsonParser.Parse) && method.IsGenericMethod)
            .MakeGenericMethod(containerType);
        var parsed = Assert.IsAssignableFrom<Resource>(
            parseMethod.Invoke(new FhirJsonParser(), [firstJson]));
        var secondJson = serializer.Serialize(parsed);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstJson), JsonNode.Parse(secondJson)));
        Assert.Equal(
            "2020-01-01T00:00:00Z",
            ((FhirDateTime)periodType.GetProperty("Start")!.GetValue(
                containerType.GetProperty("Value")!.GetValue(parsed)!)!).Value);

        periodType.GetProperty("Start")!.SetValue(
            containerType.GetProperty("Value")!.GetValue(parsed),
            new FhirDateTime("2020-99-99"));
        var validation = new FhirValidator().Validate(parsed);
        Assert.Contains(validation.Issues, issue =>
            issue.Code == ValidationIssueCode.PrimitiveFormat &&
            issue.Path == "GeneratedDatatypeContainer.value.start");
    }

    private static string[] PublicShape(Type type)
    {
        var nullability = new NullabilityInfoContext();
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => string.Join(
                '|',
                property.Name,
                FormatType(property.PropertyType),
                nullability.Create(property).ReadState,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "-",
                property.SetMethod?.IsPublic == true))
            .ToArray();
    }

    private static string FormatType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }
        var genericName = type.GetGenericTypeDefinition().FullName!;
        return $"{genericName[..genericName.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
    }

    private static Assembly CompileGeneratedAssembly(IReadOnlyList<GeneratedSource> sources)
    {
        var syntaxTrees = sources.Select(source => CSharpSyntaxTree.ParseText(
            source.Source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13),
            source.FileName));
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(DataType).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            $"MyFhirSdk.Generated.ComplexDatatypes.Tests.{Guid.NewGuid():N}",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics));
        stream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }
}
