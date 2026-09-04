using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Diagnostics;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Compilation;

public sealed class RoslynCompilationValidatorTests
{
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

}
