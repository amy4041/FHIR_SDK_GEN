using MyFhirSdk.CodeGen.Assets;
using MyFhirSdk.CodeGen.Compilation;
using MyFhirSdk.CodeGen.Contracts;
using MyFhirSdk.CodeGen.Metadata;

namespace MyFhirSdk.Tests.Architecture;

public sealed class PhaseDCodeGenDependencyArchitectureTests
{
    [Fact]
    public void CodeGenAssemblyDoesNotReferenceSdkImplementationAssembly()
    {
        var references = typeof(ModelMetadataIrBuilder).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.DoesNotContain("MyFhirSdk", references, StringComparer.Ordinal);
    }

    [Fact]
    public void MetadataBuilderRequiresValidatedRuntimeContractView()
    {
        var constructor = Assert.Single(typeof(ModelMetadataIrBuilder).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(RuntimeContractView), parameter.ParameterType);
    }

    [Fact]
    public void CompilationValidatorRequiresExplicitRuntimeReferenceSet()
    {
        var constructor = Assert.Single(typeof(RoslynCompilationValidator).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(RuntimeReferenceSet), parameter.ParameterType);
    }
}
