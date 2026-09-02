namespace MyFhirSdk.CodeGen.Graph;

public enum DefinitionDependencyEdgeKind
{
    Inheritance,
    ElementType,
    Profile,
    TargetProfile,
    ContentReference,
    BackboneOwner
}
