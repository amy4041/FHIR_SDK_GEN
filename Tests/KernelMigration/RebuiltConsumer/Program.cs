using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using KernelMigration.RebuiltLibrary;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;

var runtime = typeof(Base).Assembly;
var sdk = typeof(FhirBoolean).Assembly;
var library = typeof(MigrationProbe).Assembly;
var application = Assembly.GetExecutingAssembly();
var kernelNames = new[] {
    "BackboneElement", "BackboneType", "Base", "DataType", "DomainResource", "Element",
    "Extension", "FhirObject", "IFhirExtensionValue", "IPrimitiveValueAccessor", "Meta",
    "Narrative", "PrimitiveType`1", "Resource"
};
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
Require(runtime.GetName().Name == "MyFhirSdk.Runtime", "Incorrect Runtime defining assembly.");
Require(sdk.GetName().Name == "MyFhirSdk", "Incorrect SDK identity.");
Require(runtime.GetExportedTypes().Select(t => t.Name).Order(StringComparer.Ordinal)
    .SequenceEqual(kernelNames.Order(StringComparer.Ordinal)), "Unexpected kernel ownership.");
Require(!sdk.GetExportedTypes().Any(t => t.Namespace == "MyFhirSdk.Core" && kernelNames.Contains(t.Name)),
    "SDK duplicates kernel declarations.");
Require(!sdk.GetForwardedTypes().Any(t => t.Namespace == "MyFhirSdk.Core" && kernelNames.Contains(t.Name)),
    "Kernel type forwarders are forbidden by the rebuild policy.");
foreach (var type in runtime.GetExportedTypes())
{
    Require(type.AssemblyQualifiedName!.Contains(", MyFhirSdk.Runtime,"), "Unexpected assembly-qualified identity.");
    Require(Type.GetType(type.AssemblyQualifiedName, throwOnError: true) == type, "Reflection identity resolution failed.");
}
foreach (var assembly in new[] { runtime, sdk, library, application })
    Require(Path.GetFullPath(Path.GetDirectoryName(assembly.Location)!) == Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)),
        "An assembly was loaded outside the isolated deployment: " + assembly.Location);
foreach (var assembly in new[] { library, application })
{
    using var stream = File.OpenRead(assembly.Location);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var kernelReferences = metadata.TypeReferences.Select(h => metadata.GetTypeReference(h))
        .Where(t => metadata.GetString(t.Namespace) == "MyFhirSdk.Core" && kernelNames.Contains(metadata.GetString(t.Name))).ToArray();
    Require(kernelReferences.Length > 0, "Consumer did not exercise moved types.");
    foreach (var reference in kernelReferences)
        Require(reference.ResolutionScope.Kind == HandleKind.AssemblyReference
            && metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name) == "MyFhirSdk.Runtime",
            "Consumer DLL still references the old SDK kernel identity.");
}
Base model = MigrationProbe.RoundTrip();
Require(model is Resource, "Application model/base assignment failed.");
PrimitiveType<bool?> primitive = MigrationProbe.AccessPrimitive();
Require(((IPrimitiveValueAccessor)primitive).UntypedValue is null, "Application SPI access failed.");
Console.WriteLine(JsonSerializer.Serialize(new {
    status = "passed", checks = new[] { "ownership", "noForwarders", "libraryAndApplicationPe", "reflection", "loadPaths", "jsonRoundTrip", "accessorCasts" },
    assemblies = new[] { runtime, sdk, library, application }.Select(a => new { identity = a.FullName, path = a.Location })
}));
