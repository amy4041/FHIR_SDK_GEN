using MyFhirSdk.Core;
using GeneratedHumanName = MyFhirSdk.GeneratorFixtures.Types.HumanName;

namespace MyFhirSdk.CodeGen.Tests.Runtime;

public sealed class GeneratedDatatypeContainer : Resource
{
    public override string ResourceType => nameof(GeneratedDatatypeContainer);

    public GeneratedHumanName? Name { get; set; }
}
