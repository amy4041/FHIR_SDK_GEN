using MyFhirSdk.ModelSupport;

namespace MyFhirSdk.Tests.Architecture.V3;

public sealed class ModelSupportApiSnapshotTests
{
    [Fact]
    public void SharedSupportPublicSurfaceMatchesApprovedSnapshot()
    {
        var actual = PublicApiSnapshotTests.CreateSnapshot(typeof(ModelRules).Assembly, includeAll: true);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ActualModelSupportApi.txt"), actual + "\n");
        var approved = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ApprovedModelSupportApi.txt"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        Assert.Equal(approved, actual);
    }
}
