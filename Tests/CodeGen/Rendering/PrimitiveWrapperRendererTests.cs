using System.Security.Cryptography;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Inventory;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.Core;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Rendering;

public sealed class PrimitiveWrapperRendererTests
{
    private readonly PrimitiveWrapperRenderer _renderer = new();

    [Fact]
    public async Task RenderAll_MatchesTwentyGoldenHashes()
    {
        var models = await LoadModelsAsync();
        var expected = File.ReadAllLines(GetGoldenHashPath())
            .Select(line => line.Split("  ", 2, StringSplitOptions.None))
            .ToDictionary(parts => parts[1], parts => parts[0], StringComparer.Ordinal);

        var sources = _renderer.RenderAll(models);

        Assert.Equal(20, sources.Count);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), sources.Select(
            source => source.FileName));
        Assert.All(
            sources,
            source => Assert.Equal(
                expected[source.FileName],
                Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(source.Source))).ToLowerInvariant()));
    }

    [Fact]
    public async Task RenderAll_IsDeterministicForShuffledModelsAndCompiles()
    {
        var models = await LoadModelsAsync();

        var original = _renderer.RenderAll(models);
        var reversed = _renderer.RenderAll(models.Reverse());
        var compilationResult = new RoslynCompilationValidator().Validate(original);

        Assert.Equal(original, reversed);
        Assert.True(
            compilationResult.IsSuccess,
            string.Join(Environment.NewLine, compilationResult.Diagnostics));
    }

    [Fact]
    public async Task RenderAll_DoesNotReferenceRuntimeImplementationApis()
    {
        var sources = _renderer.RenderAll(await LoadModelsAsync());
        var forbiddenTokens = new[]
        {
            "IPrimitiveCodec",
            "IPrimitiveValidator",
            "PrimitiveRegistry",
            "IsValid(",
            "MyFhirSdk.Parser",
            "MyFhirSdk.Serialization",
            "MyFhirSdk.Validation"
        };

        Assert.All(
            sources,
            source => Assert.All(
                forbiddenTokens,
                token => Assert.DoesNotContain(
                    token,
                    source.Source,
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void Render_EscapesXmlDocumentationAndNormalizesNewlines()
    {
        var model = new PrimitiveWrapperModel(
            "sample",
            "http://example.test/sample",
            "5.0.0",
            "MyFhirSdk.Primitives",
            "FhirSample",
            "string",
            "First <line> & \"value\".\r\nSecond line's detail.",
            PrimitiveWrapperLiteralKind.None,
            null,
            PrimitiveWrapperToStringKind.Inherited,
            []);

        var source = _renderer.Render(model);

        Assert.Contains(
            "/// First &lt;line&gt; &amp; &quot;value&quot;.\n" +
            "/// Second line&apos;s detail.\n",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain('\r', source);
        Assert.False(source.StartsWith('\uFEFF'));
    }

    [Fact]
    public async Task GeneratedAssembly_PreservesWrapperShapeAndLiteralBehavior()
    {
        var models = await LoadModelsAsync();
        var assembly = CompileGeneratedAssembly(_renderer.RenderAll(models));

        foreach (var model in models)
        {
            var type = assembly.GetType($"{model.Namespace}.{model.WrapperName}");
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
            Assert.True(type.IsSealed);
            var valueType = GetClrValueType(model.ClrValueType);
            Assert.Equal(
                typeof(PrimitiveType<>).MakeGenericType(valueType),
                type.BaseType);
            Assert.Null(type.GetMethod(
                "IsValid",
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly));
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
            Assert.NotNull(type.GetConstructor([valueType]));
            Assert.Equal(
                model.LiteralKind == PrimitiveWrapperLiteralKind.None ? 2 : 3,
                type.GetConstructors().Length);

            var literal = type.GetProperty(
                "Literal",
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            Assert.Equal(
                model.LiteralKind != PrimitiveWrapperLiteralKind.None,
                literal is not null);
            Assert.Null(literal?.SetMethod);
        }

        AssertLiteralBehavior(
            assembly,
            "FhirDecimal",
            "1.20e2",
            120m);
        AssertLiteralBehavior(
            assembly,
            "FhirInteger64",
            "00123",
            123L);
        AssertLiteralBehavior(
            assembly,
            "FhirDecimal",
            "not-a-decimal",
            null);
        AssertLiteralBehavior(
            assembly,
            "FhirInteger64",
            "9223372036854775808",
            null);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var booleanType = assembly.GetType(
                "MyFhirSdk.Primitives.FhirBoolean",
                throwOnError: true)!;
            var boolean = Activator.CreateInstance(
                booleanType,
                new object?[] { true })!;
            Assert.Equal("true", boolean.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static async Task<IReadOnlyList<PrimitiveWrapperModel>> LoadModelsAsync()
    {
        var coverageResult = await new PrimitiveInventoryCoveragePipeline().BuildAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "StructureDefinitions",
                "Primitives",
                "R5"),
            Path.Combine(
                AppContext.BaseDirectory,
                "Policy",
                "primitive-generation-policy.json"),
            "5.0.0");
        Assert.True(coverageResult.IsSuccess);

        var modelResult = new PrimitiveWrapperModelBuilder().Build(
            Assert.IsType<PrimitiveInventoryPolicyCoverage>(coverageResult.Value));
        Assert.True(modelResult.IsSuccess);
        return modelResult.Value;
    }

    private static Assembly CompileGeneratedAssembly(
        IReadOnlyList<GeneratedSource> sources)
    {
        var syntaxTrees = sources.Select(source => CSharpSyntaxTree.ParseText(
            source.Source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13),
            source.FileName));
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");
        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(PrimitiveType<>).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            $"MyFhirSdk.Generated.Primitives.Tests.{Guid.NewGuid():N}",
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

    private static void AssertLiteralBehavior(
        Assembly assembly,
        string wrapperName,
        string literal,
        object? expectedValue)
    {
        var type = assembly.GetType(
            $"MyFhirSdk.Primitives.{wrapperName}",
            throwOnError: true)!;
        var instance = Activator.CreateInstance(type, literal)!;

        Assert.Equal(literal, type.GetProperty("Literal")!.GetValue(instance));
        Assert.Equal(expectedValue, type.GetProperty("Value")!.GetValue(instance));
        Assert.Equal(literal, instance.ToString());
    }

    private static Type GetClrValueType(string clrValueType)
    {
        return clrValueType switch
        {
            "string" => typeof(string),
            "bool?" => typeof(bool?),
            "decimal?" => typeof(decimal?),
            "int?" => typeof(int?),
            "long?" => typeof(long?),
            _ => throw new InvalidOperationException(
                $"Unexpected CLR value type '{clrValueType}'.")
        };
    }

    private static string GetGoldenHashPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "GoldenFiles",
            "R5",
            "Primitives",
            "SHA256SUMS.txt");
    }
}
