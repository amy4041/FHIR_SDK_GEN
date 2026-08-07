using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Rendering;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Compilation;

public sealed class RoslynCompilationValidatorTests
{
    private const string FhirVersion = "5.0.0";
    private const string TargetNamespace =
        "MyFhirSdk.GeneratorFixtures.Types";

    private static readonly string[] MvpTypeNames =
        ["Period", "Coding", "HumanName", "Address", "Identifier"];

    private static readonly IReadOnlySet<string> MvpPreviewTypes =
        new HashSet<string>(MvpTypeNames, StringComparer.Ordinal);

    [Fact]
    public async Task Validate_AllMvpGeneratedSources_CompilesTogether()
    {
        var generatedSources = await GenerateMvpSourcesAsync();

        var result = new RoslynCompilationValidator().Validate(generatedSources);

        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(generatedSources, result.Value);
    }

    [Fact]
    public void Validate_UnresolvableType_ReturnsFsg0012WithRoslynDetails()
    {
        var generatedSources = new[]
        {
            new GeneratedSource(
                "BrokenType.g.cs",
                """
                namespace MyFhirSdk.GeneratorFixtures.Types;

                public sealed class BrokenType : MissingBaseType
                {
                }
                """)
        };

        var result = new RoslynCompilationValidator().Validate(generatedSources);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.CompilationFailure, diagnostic.Code);
        Assert.Equal(GeneratorDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("BrokenType.g.cs", diagnostic.SourceFile);
        Assert.Contains("CS0246", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(
            "MissingBaseType",
            diagnostic.Message,
            StringComparison.Ordinal);
        Assert.Contains("line 3", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DuplicateProperty_ReturnsFsg0012ForGeneratedFile()
    {
        var generatedSources = new[]
        {
            new GeneratedSource(
                "DuplicateProperty.g.cs",
                """
                namespace MyFhirSdk.GeneratorFixtures.Types;

                public sealed class DuplicateProperty
                {
                    public string? Value { get; set; }
                    public string? Value { get; set; }
                }
                """)
        };

        var result = new RoslynCompilationValidator().Validate(generatedSources);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticCodes.CompilationFailure, diagnostic.Code);
        Assert.Equal("DuplicateProperty.g.cs", diagnostic.SourceFile);
        Assert.Contains("CS0102", diagnostic.Message, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<GeneratedSource>>
        GenerateMvpSourcesAsync()
    {
        var renderer = new CSharpClassRenderer();
        var generatedSources = new List<GeneratedSource>();

        foreach (var typeName in MvpTypeNames)
        {
            var fixturePath = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "StructureDefinitions",
                "Valid",
                $"StructureDefinition-{typeName}.json");
            var loadResult = await new StructureDefinitionLoader().LoadAsync(
                fixturePath,
                FhirVersion);
            Assert.True(
                loadResult.IsSuccess,
                FormatDiagnostics(loadResult.Diagnostics));

            var loadedDefinition = Assert.Single(loadResult.Value);
            var parseResult = new StructureDefinitionParser().Parse(
                loadedDefinition,
                TargetNamespace,
                MvpPreviewTypes);
            Assert.True(
                parseResult.IsSuccess,
                FormatDiagnostics(parseResult.Diagnostics));

            var model = Assert.IsType<FhirTypeModel>(parseResult.Value);
            generatedSources.Add(new GeneratedSource(
                $"{typeName}.g.cs",
                renderer.Render(model)));
        }

        return generatedSources;
    }

    private static string FormatDiagnostics(
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"[{diagnostic.Code}] {diagnostic.SourceFile}: " +
                diagnostic.Message));
    }
}
