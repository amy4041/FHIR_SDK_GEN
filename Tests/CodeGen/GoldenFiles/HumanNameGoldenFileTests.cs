using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Rendering;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.GoldenFiles;

public sealed class HumanNameGoldenFileTests
{
    private const string FhirVersion = "5.0.0";
    private const string TargetNamespace =
        "MyFhirSdk.GeneratorFixtures.Types";

    [Fact]
    public async Task HumanName_FromFixture_MatchesReviewedGoldenFile()
    {
        var fixturePath = GetOutputPath(
            "Fixtures",
            "StructureDefinitions",
            "Valid",
            "StructureDefinition-HumanName.json");
        var goldenFilePath = GetOutputPath(
            "GoldenFiles",
            "R5",
            "Types",
            "HumanName.golden.cs.txt");

        var loadResult = await new StructureDefinitionLoader().LoadAsync(
            fixturePath,
            FhirVersion);

        Assert.True(loadResult.IsSuccess);
        Assert.Empty(loadResult.Diagnostics);
        var loadedDefinition = Assert.Single(loadResult.Value);

        var parseResult = PrimitivePolicyTestContext.CreateParser().Parse(
            loadedDefinition,
            TargetNamespace,
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Period",
                "Coding",
                "HumanName",
                "Address",
                "Identifier"
            });

        Assert.True(parseResult.IsSuccess);
        Assert.Empty(parseResult.Diagnostics);
        var model = Assert.IsType<FhirTypeModel>(parseResult.Value);

        var renderer = new CSharpClassRenderer();
        var actualSource = renderer.Render(model);
        var repeatedSource = renderer.Render(model);
        var expectedSource = await File.ReadAllTextAsync(goldenFilePath);

        Assert.Equal(actualSource, repeatedSource);
        AssertMatchesGolden(
            NormalizeNewlines(expectedSource),
            NormalizeNewlines(actualSource));
    }

    private static string GetOutputPath(params string[] segments)
    {
        return segments.Aggregate(AppContext.BaseDirectory, Path.Combine);
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static void AssertMatchesGolden(
        string expectedSource,
        string actualSource)
    {
        if (string.Equals(expectedSource, actualSource, StringComparison.Ordinal))
        {
            return;
        }

        var expectedLines = expectedSource.Split('\n');
        var actualLines = actualSource.Split('\n');
        var sharedLineCount = Math.Min(expectedLines.Length, actualLines.Length);
        var differingLineIndex = Enumerable.Range(0, sharedLineCount)
            .FirstOrDefault(
                index => !string.Equals(
                    expectedLines[index],
                    actualLines[index],
                    StringComparison.Ordinal),
                -1);

        if (differingLineIndex < 0)
        {
            differingLineIndex = sharedLineCount;
        }

        var expectedLine = differingLineIndex < expectedLines.Length
            ? expectedLines[differingLineIndex]
            : "<end of file>";
        var actualLine = differingLineIndex < actualLines.Length
            ? actualLines[differingLineIndex]
            : "<end of file>";

        Assert.Fail(
            $"Generated source differs from the reviewed Golden File at line " +
            $"{differingLineIndex + 1}.{Environment.NewLine}" +
            $"Expected: {expectedLine}{Environment.NewLine}" +
            $"Actual:   {actualLine}");
    }
}
