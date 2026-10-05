using System.Runtime.Loader;
using System.Text;
using MyFhirSdk.KernelMigration;

if (args.Length != 2) throw new ArgumentException("Usage: Inventory <implementation.dll> <output.txt>");
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
File.WriteAllText(args[1], KernelApiInventory.Create(assembly), new UTF8Encoding(false));
Console.WriteLine($"Inventoried {assembly.GetExportedTypes().Length} exported types.");
