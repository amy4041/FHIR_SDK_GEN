using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Generation;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Rendering;
using MyFhirSdk.CodeGen.Tests.Generation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Rendering;

public sealed class PrimitiveRegistryCompositionRendererTests
{
    [Fact]
    public async Task Render_WithOfficialModel_MatchesGoldenSource()
    {
        var model = await LoadModelAsync();
        var source = new PrimitiveRegistryCompositionRenderer().Render(model);
        var expected = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "GoldenFiles",
            "R5",
            "Primitives",
            "PrimitiveRegistry.Composition.golden.cs.txt"));

        Assert.Equal(expected.Replace("\r\n", "\n", StringComparison.Ordinal), source);
        Assert.DoesNotContain('\r', source);
        Assert.DoesNotContain("System.Reflection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_WithReversedEntries_IsDeterministic()
    {
        var model = await LoadModelAsync();
        var reversed = new PrimitiveRegistryCompositionModel(
            model.Namespace,
            model.Entries.Reverse());
        var renderer = new PrimitiveRegistryCompositionRenderer();

        Assert.Equal(renderer.Render(model), renderer.Render(reversed));
    }

    [Fact]
    public async Task RenderedComposition_CompilesWithSameAssemblySeam()
    {
        var model = await LoadModelAsync();
        var generated = new PrimitiveRegistryCompositionRenderer().Render(model);
        var wrapperDeclarations = string.Join(
            Environment.NewLine,
            model.Entries.Select(entry =>
                $"internal sealed class {entry.WrapperName} {{ }}"));
        var stub = $$"""
            using System.Collections.Generic;

            namespace MyFhirSdk.Primitives;

            internal interface IPrimitiveDefinition { }

            internal sealed partial class PrimitiveRegistry
            {
                static partial void AddGeneratedDefinitions(
                    List<IPrimitiveDefinition> definitions);

                private static IPrimitiveDefinition Define<TPrimitive, TValue>(
                    string fhirTypeName,
                    object codec,
                    object validator) => new Definition();

                private sealed class Definition : IPrimitiveDefinition { }
            }

            internal static class PrimitiveCodecs
            {
                internal static object String { get; } = new();
                internal static object Boolean { get; } = new();
                internal static object Integer { get; } = new();
                internal static object Decimal { get; } = new();
                internal static object Integer64 { get; } = new();
            }

            internal static class PrimitiveValidators
            {
                {{string.Join(Environment.NewLine, model.Entries
                    .Select(entry => entry.ValidatorSymbol)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Select(symbol =>
                        $"internal static object {symbol.Split('.')[1]} {{ get; }} = new();"))}}
            }

            {{wrapperDeclarations}}
            """;
        var syntaxTrees = new[] { generated, stub }
            .Select(source => CSharpSyntaxTree.ParseText(
                source,
                CSharpParseOptions.Default.WithLanguageVersion(
                    LanguageVersion.CSharp13)));
        var references = ((string?)AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "PrimitiveRegistryCompositionTests",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Empty(compilation.GetDiagnostics().Where(
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    private static async Task<PrimitiveRegistryCompositionModel> LoadModelAsync()
    {
        var coverage = await PrimitiveRegistryModelBuilderTests.LoadCoverageAsync();
        return Assert.IsType<PrimitiveRegistryCompositionModel>(
            new PrimitiveRegistryModelBuilder().Build(coverage).Value);
    }
}
