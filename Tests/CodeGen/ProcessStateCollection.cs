using Xunit;

namespace MyFhirSdk.CodeGen.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessStateCollection
{
    public const string Name = "Process state";
}
