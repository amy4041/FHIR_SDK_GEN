using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.Core;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

internal static class GeneratedModelTestCompiler
{
    internal static Assembly Compile(IReadOnlyList<GeneratedSource> sources)
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
            $"MyFhirSdk.Generated.Models.Tests.{Guid.NewGuid():N}",
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
