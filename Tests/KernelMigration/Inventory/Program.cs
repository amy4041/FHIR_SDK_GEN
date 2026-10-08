using System.Runtime.Loader;
using System.Text;
using MyFhirSdk.KernelMigration;

if (args.Length < 2) throw new ArgumentException("Usage: Inventory <implementation.dll> <output.txt> [additional-implementation.dll ...]");
// Load explicitly supplied dependencies first. The two-argument historical K0 mode is unchanged.
var paths = new[] { args[0] }.Concat(args.Skip(2)).Select(Path.GetFullPath).ToArray();
var assemblies = paths.Reverse().Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).ToArray();
var lines = new SortedSet<string>(StringComparer.Ordinal);
foreach (var assembly in assemblies)
    foreach (var line in KernelApiInventory.Create(assembly).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        lines.Add(line);
File.WriteAllText(args[1], string.Join('\n', lines) + "\n", new UTF8Encoding(false));
Console.WriteLine($"Inventoried {assemblies.Sum(a => a.GetExportedTypes().Length)} exported types across {assemblies.Length} assemblies.");
