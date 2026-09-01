using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Reconnaissance;

public sealed class R5PackageReconnaissanceTests
{
    [Fact]
    public void OfficialPackage_MatchesApprovedDeterministicSnapshot()
    {
        var input = R5PackageReconnaissance.Read(GetArchivePath());
        var actual = R5PackageReconnaissance.Render(
            R5PackageReconnaissance.Build(input));
        var expected = NormalizeNewlines(
            File.ReadAllText(GetApprovedSnapshotPath()));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Build_WithReorderedDefinitions_ProducesIdenticalReport()
    {
        var input = R5PackageReconnaissance.Read(GetArchivePath());
        var original = R5PackageReconnaissance.Render(
            R5PackageReconnaissance.Build(input));
        var reordered = R5PackageReconnaissance.Render(
            R5PackageReconnaissance.Build(
                input with
                {
                    Definitions = input.Definitions.Reverse().ToArray()
                }));

        Assert.Equal(original, reordered);
    }

    [Fact]
    public void Build_TwoRuns_ProducesByteIdenticalReport()
    {
        var first = R5PackageReconnaissance.Render(
            R5PackageReconnaissance.Build(
                R5PackageReconnaissance.Read(GetArchivePath())));
        var second = R5PackageReconnaissance.Render(
            R5PackageReconnaissance.Build(
                R5PackageReconnaissance.Read(GetArchivePath())));

        Assert.Equal(first, second);
    }

    private static string GetArchivePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz");
    }

    private static string GetApprovedSnapshotPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "structuredefinition-reconnaissance.approved.json");
    }

    private static string NormalizeNewlines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
