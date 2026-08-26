using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Models;

namespace MyFhirSdk.CodeGen.Compilation;

public sealed class PrimitiveRegistryCompositionCompilationValidator
{
    public GenerationResult<GeneratedSource?> Validate(
        GeneratedSource composition,
        PrimitiveRegistryCompositionModel model)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(model);

        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp13);
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(
                composition.Source,
                parseOptions,
                composition.FileName),
            CSharpSyntaxTree.ParseText(
                CreateSameAssemblyContract(model),
                parseOptions,
                "PrimitiveRegistry.ValidationContract.cs")
        };
        var references = ((string?)AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "MyFhirSdk.Generated.PrimitiveRegistry.Validation",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));
        var diagnostics = compilation.GetDiagnostics()
            .Where(item => item.Severity == DiagnosticSeverity.Error)
            .Select(ToGeneratorDiagnostic)
            .ToArray();

        return new GenerationResult<GeneratedSource?>(
            diagnostics.Length == 0 ? composition : null,
            diagnostics);
    }

    private static string CreateSameAssemblyContract(
        PrimitiveRegistryCompositionModel model)
    {
        var validatorProperties = model.Entries
            .Select(entry => entry.ValidatorSymbol.Split('.')[1])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => $"    internal static object {value} {{ get; }} = new();");
        var wrapperDeclarations = model.Entries
            .Select(entry => entry.WrapperName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => $"internal sealed class {value} {{ }}");

        return $$"""
            using System.Collections.Generic;

            namespace {{model.Namespace}};

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
            {{string.Join("\n", validatorProperties)}}
            }

            {{string.Join("\n", wrapperDeclarations)}}
            """;
    }

    private static GeneratorDiagnostic ToGeneratorDiagnostic(Diagnostic diagnostic)
    {
        var sourceFile = diagnostic.Location.SourceTree?.FilePath ??
            "<primitive-registry-composition>";
        var lineSuffix = "";
        if (diagnostic.Location.IsInSource)
        {
            var position = diagnostic.Location.GetLineSpan().StartLinePosition;
            lineSuffix = $" (line {position.Line + 1}, column {position.Character + 1})";
        }

        return new GeneratorDiagnostic(
            GeneratorDiagnosticCodes.CompilationFailure,
            GeneratorDiagnosticSeverity.Error,
            $"{diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}" +
            lineSuffix,
            sourceFile);
    }
}
