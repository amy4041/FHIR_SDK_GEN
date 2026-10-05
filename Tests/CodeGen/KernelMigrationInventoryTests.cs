using System.Globalization;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.KernelMigration;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests;

public sealed class KernelMigrationInventoryTests
{
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    [Theory]
    [InlineData("public string? Read() => null;", "public string Read() => string.Empty;")]
    [InlineData("public void Read(string? value) {}", "public void Read(string value) {}")]
    [InlineData("public System.Collections.Generic.List<string?>?[] Read() => [];", "public System.Collections.Generic.List<string>[] Read() => [];")]
    [InlineData("public string? Value { get; set; }", "public string Value { get; set; } = string.Empty;")]
    [InlineData("public string? Value;", "public string Value = string.Empty;")]
    [InlineData("public event System.Action? Changed;", "public event System.Action Changed = delegate {};")]
    [InlineData("public void Read(int retries = 1) {}", "public void Read(int retries = 9) {}")]
    [InlineData("public const int Limit = 1;", "public const int Limit = 9;")]
    [InlineData("public enum Mode { A = 1 }", "public enum Mode { A = 9 }")]
    public void ContractChangesCannotProduceIdenticalInventory(string before, string after)
    {
        Assert.NotEqual(Inventory("public class Probe { " + before + " }"), Inventory("public class Probe { " + after + " }"));
    }

    [Fact]
    public void InventoriesPreserveConstructorsConstraintsAndExternalGenericIdentities()
    {
        var text = Inventory("""
            public class Probe<T> where T : System.IDisposable, new() {
                protected Probe(T value) {}
                public class Nested { public System.Collections.Generic.List<System.Uri?> Values = []; }
                public event System.Action? Changed;
            }
            """);
        Assert.Contains(".ctor(", text);
        Assert.Contains("Family", text);
        Assert.Contains("Probe`1+Nested", text);
        Assert.Contains("GENERIC !0 DefaultConstructorConstraint", text);
        Assert.Contains("System.IDisposable", text);
        Assert.Contains("[System.Private.Uri]System.Uri", text);
        Assert.Contains("IDENTITY [System.Private.Uri] = System.Private.Uri, Version=", text);
        Assert.Contains(" EVENT ", text);
    }

    [Fact]
    public void DefaultsAreEscapedAndCultureIndependent()
    {
        const string source = "public class Probe { public void Read(decimal n = 1.25m, string s = \"a\\nb\") {} }";
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var french = Inventory(source);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(french, Inventory(source));
            Assert.Contains("default=1.25", french);
            Assert.Contains("default=\"a\\nb\"", french);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void AssemblyIdentityChangesAreVisible()
    {
        Assert.NotEqual(Inventory("[assembly:System.Reflection.AssemblyVersion(\"1.0.0.0\")] public class Probe {}"),
            Inventory("[assembly:System.Reflection.AssemblyVersion(\"2.0.0.0\")] public class Probe {}"));
    }

    private static string Inventory(string source)
    {
        var compilation = CSharpCompilation.Create("KernelInventoryProbe", [CSharpSyntaxTree.ParseText("#nullable enable\n" + source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        stream.Position = 0;
        var context = new AssemblyLoadContext("kernel-inventory-probe", isCollectible: true);
        try { return KernelApiInventory.Create(context.LoadFromStream(stream)); }
        finally { context.Unload(); }
    }
}
