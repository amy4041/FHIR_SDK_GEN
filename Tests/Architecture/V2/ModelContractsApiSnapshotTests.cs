using MyFhirSdk.ModelContracts;

namespace MyFhirSdk.Tests.Architecture.V2;

public sealed class ModelContractsApiSnapshotTests
{
    [Fact]
    public void NeutralSpiAndItsRuntimeSignatureDependenciesMatchTheApprovedSnapshot()
    {
        var actual = PublicApiSnapshotTests.CreateSnapshot(typeof(ModelIdentity).Assembly, includeAll: true);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ActualModelContractsApi.txt"), actual + "\n");
        var approved = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ApprovedModelContractsApi.txt"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        Assert.Equal(approved, actual);
    }
}
