using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Loading;
using MyFhirSdk.CodeGen.Models;
using MyFhirSdk.CodeGen.Parsing;
using MyFhirSdk.CodeGen.Rendering;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.GoldenFiles;

public sealed class TypeGoldenFileTests
{
    private const string TargetNamespace =
        "MyFhirSdk.GeneratorFixtures.Types";
    private const string MvpPreviewTypeNames =
        "Period,Coding,HumanName,Address,Identifier";

    public static TheoryData<string, string, string, string> TypeCases =>
        new()
        {
            // typeName, FHIR version, Golden File version directory, preview types
            { "Period", "5.0.0", "R5", MvpPreviewTypeNames },
            { "Coding", "5.0.0", "R5", MvpPreviewTypeNames },
            { "HumanName", "5.0.0", "R5", MvpPreviewTypeNames },
            { "Address", "5.0.0", "R5", MvpPreviewTypeNames },
            { "Identifier", "5.0.0", "R5", MvpPreviewTypeNames }
        };

    [Theory]
    [MemberData(nameof(TypeCases))]
    public async Task Type_FromFixture_MatchesReviewedGoldenFile(
        string typeName,
        string fhirVersion,
        string versionDirectory,
        string previewTypeNames)
    {
        var fixturePath = GetOutputPath(
            "Fixtures",
            "StructureDefinitions",
            "Valid",
            $"StructureDefinition-{typeName}.json");
        var goldenFilePath = GetOutputPath(
            "GoldenFiles",
            versionDirectory,
            "Types",
            $"{typeName}.golden.cs.txt");
        var previewTypes = previewTypeNames
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        var loadResult = await new StructureDefinitionLoader().LoadAsync(
            fixturePath,
            fhirVersion);

        Assert.True(
            loadResult.IsSuccess,
            FormatDiagnostics(typeName, "loading", loadResult.Diagnostics));
        Assert.Empty(loadResult.Diagnostics);
        var loadedDefinition = Assert.Single(loadResult.Value);

        var parseResult = new StructureDefinitionParser().Parse(
            loadedDefinition,
            TargetNamespace,
            previewTypes);

        Assert.True(
            parseResult.IsSuccess,
            FormatDiagnostics(typeName, "parsing", parseResult.Diagnostics));
        Assert.Empty(parseResult.Diagnostics);
        var model = Assert.IsType<FhirTypeModel>(parseResult.Value);

        var renderer = new CSharpClassRenderer();
        var actualSource = renderer.Render(model);
        var repeatedSource = renderer.Render(model);
        var expectedSource = await File.ReadAllTextAsync(goldenFilePath);

        Assert.Equal(actualSource, repeatedSource);
        AssertMatchesGolden(
            typeName,
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

    private static string FormatDiagnostics(
        string typeName,
        string stage,
        IEnumerable<GeneratorDiagnostic> diagnostics)
    {
        var details = string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"[{diagnostic.Code}] {diagnostic.Message}"));

        return string.IsNullOrEmpty(details)
            ? $"{typeName} failed during {stage}."
            : $"{typeName} failed during {stage}:{Environment.NewLine}{details}";
    }

    private static void AssertMatchesGolden(
        string typeName,
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
            $"{typeName} generated source differs from its reviewed Golden " +
            $"File at line {differingLineIndex + 1}.{Environment.NewLine}" +
            $"Expected: {expectedLine}{Environment.NewLine}" +
            $"Actual:   {actualLine}");
    }
}
