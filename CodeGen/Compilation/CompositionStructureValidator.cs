using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Rendering;

namespace MyFhirSdk.CodeGen.Compilation;

/// <summary>Checks rendered composition syntax and entry points, not SDK semantic compatibility.</summary>
public sealed class CompositionStructureValidator
{
    public GenerationResult<IReadOnlyList<GeneratedSource>> Validate(IReadOnlyList<GeneratedSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ModelMetadataRenderer.ArtifactPath] = "GeneratedR5ModelMetadata",
            [ValidationCompositionRenderer.ArtifactPath] = "GeneratedR5ValidationRules"
        };
        var diagnostics = new List<GeneratorDiagnostic>();
        foreach (var (path, name) in expected)
        {
            var matches = sources.Where(source => source.FileName == path).ToArray();
            if (matches.Length != 1)
            {
                Add(path, "Composition artifact must occur exactly once.");
                continue;
            }
            var tree = CSharpSyntaxTree.ParseText(matches[0].Source,
                new CSharpParseOptions(LanguageVersion.CSharp13), path);
            foreach (var error in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                Add(path, error.ToString());
            var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            if (classes.Length != 1 || classes[0].Identifier.ValueText != name
                || !classes[0].Modifiers.Any(SyntaxKind.InternalKeyword)
                || !classes[0].Modifiers.Any(SyntaxKind.StaticKeyword)
                || classes[0].Members.OfType<MethodDeclarationSyntax>().Count(method =>
                    method.Identifier.ValueText == "Create" && method.ParameterList.Parameters.Count == 0
                    && method.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && method.Modifiers.Any(SyntaxKind.InternalKeyword)) != 1)
                Add(path, $"Expected internal static {name}.Create() composition entry point.");
            var ns = tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToArray();
            if (ns.Length != 1 || ns[0].Name.ToString() != "MyFhirSdk.ModelMetadata.R5")
                Add(path, "Unexpected composition namespace.");
        }
        if (sources.Count != expected.Count) Add("<composition-batch>", "Unexpected composition artifact inventory.");
        return new(sources, diagnostics);

        void Add(string path, string message) => diagnostics.Add(new(
            GeneratorDiagnosticCodes.InvalidModelIr, GeneratorDiagnosticSeverity.Error, message, path));
    }
}
