using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Contracts;

public sealed partial class RuntimeContractValidator
{
    public const int SupportedSchemaVersion = 1;

    public GenerationResult<RuntimeContractView?> Validate(
        RuntimeContractDescriptorDocument document,
        string descriptorSha256,
        string sourceFile = "<runtime-contract>")
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<GeneratorDiagnostic>();
        if (document.SchemaVersion is null)
        {
            AddInvalid(diagnostics, sourceFile, "Required field 'schemaVersion' is missing.");
        }
        else if (document.SchemaVersion != SupportedSchemaVersion)
        {
            diagnostics.Add(Diagnostic(
                GeneratorDiagnosticCodes.UnsupportedRuntimeContractSchema,
                sourceFile,
                $"Runtime contract schema version '{document.SchemaVersion}' is not supported; expected '{SupportedSchemaVersion}'."));
        }

        RequireText(document.ContractVersion, "contractVersion", diagnostics, sourceFile);
        RequireText(document.TargetFramework, "targetFramework", diagnostics, sourceFile);
        RequireSha256(descriptorSha256, "descriptor SHA-256", diagnostics, sourceFile);

        ValidateAssembly(document.RuntimeAssembly, "runtimeAssembly", diagnostics, sourceFile);
        ValidateCompatibility(document.Compatibility, diagnostics, sourceFile);
        ValidateSymbols(document.Symbols, diagnostics, sourceFile);
        ValidateSlots(document.DeclaredSlots, document.Symbols, diagnostics, sourceFile);
        ValidateCompilerReference(
            document.CompilerReference,
            document.RuntimeAssembly,
            document.TargetFramework,
            diagnostics,
            sourceFile);

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
        if (orderedDiagnostics.Length > 0)
        {
            return new GenerationResult<RuntimeContractView?>(null, orderedDiagnostics);
        }

        var compatibility = document.Compatibility!;
        var view = new RuntimeContractView(
            document.SchemaVersion!.Value,
            document.ContractVersion!,
            document.TargetFramework!,
            CreateAssembly(document.RuntimeAssembly!),
            new RuntimeCompatibility(
                compatibility.ToolVersion!,
                compatibility.CodeGenVersion!,
                new RuntimeFhirPackageIdentity(
                    compatibility.FhirPackage!.Id!,
                    compatibility.FhirPackage.Version!,
                    compatibility.FhirPackage.FhirVersion!),
                new RuntimePolicyIdentity(
                    compatibility.PrimitivePolicy!.Version!,
                    compatibility.PrimitivePolicy.Sha256!),
                new ReadOnlyCollection<RuntimeNamedAssetIdentity>(compatibility.ModelPolicies!
                    .Select(policy => new RuntimeNamedAssetIdentity(policy.Name!, policy.Sha256!))
                    .ToArray())),
            document.Symbols!.Select(symbol => new RuntimeSymbol(
                symbol.ClrType!,
                symbol.Role!,
                symbol.Kind!,
                symbol.BaseClrType,
                symbol.IsAbstract!.Value,
                symbol.IsSealed!.Value,
                symbol.GenericArity!.Value,
                new ReadOnlyCollection<string>(symbol.Interfaces!.ToArray()))),
            document.DeclaredSlots!.Select(slot => new RuntimeDeclaredSlot(
                slot.DeclaringClrType!,
                slot.ClrPropertyName!,
                slot.PropertyClrType!,
                slot.ElementClrType!,
                slot.IsCollection!.Value,
                slot.IsNullable!.Value,
                slot.Role!)),
            new RuntimeCompilerReference(
                document.CompilerReference!.LogicalName!,
                document.CompilerReference.TargetFramework!,
                CreateAssembly(document.CompilerReference.Assembly!),
                document.CompilerReference.Sha256!),
            descriptorSha256);
        return new GenerationResult<RuntimeContractView?>(
            view,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static void ValidateCompatibility(
        RuntimeCompatibilityDocument? compatibility,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (compatibility is null)
        {
            AddInvalid(diagnostics, sourceFile, "Required object 'compatibility' is missing.");
            return;
        }

        RequireVersion(compatibility.ToolVersion, "compatibility.toolVersion", diagnostics, sourceFile);
        RequireVersion(compatibility.CodeGenVersion, "compatibility.codeGenVersion", diagnostics, sourceFile);
        if (compatibility.FhirPackage is null)
        {
            AddInvalid(diagnostics, sourceFile, "Required object 'compatibility.fhirPackage' is missing.");
        }
        else
        {
            RequireText(compatibility.FhirPackage.Id, "compatibility.fhirPackage.id", diagnostics, sourceFile);
            RequireVersion(compatibility.FhirPackage.Version, "compatibility.fhirPackage.version", diagnostics, sourceFile);
            RequireVersion(compatibility.FhirPackage.FhirVersion, "compatibility.fhirPackage.fhirVersion", diagnostics, sourceFile);
        }

        if (compatibility.PrimitivePolicy is null)
        {
            AddInvalid(diagnostics, sourceFile, "Required object 'compatibility.primitivePolicy' is missing.");
        }
        else
        {
            RequireVersion(compatibility.PrimitivePolicy.Version, "compatibility.primitivePolicy.version", diagnostics, sourceFile);
            RequireSha256(compatibility.PrimitivePolicy.Sha256, "compatibility.primitivePolicy.sha256", diagnostics, sourceFile);
        }

        if (compatibility.ModelPolicies is null || compatibility.ModelPolicies.Count == 0)
        {
            AddInvalid(diagnostics, sourceFile, "Required array 'compatibility.modelPolicies' must not be empty.");
            return;
        }

        ValidateOrdinalOrder(
            compatibility.ModelPolicies.Select(policy => policy.Name),
            "compatibility.modelPolicies",
            diagnostics,
            sourceFile);
        AddDuplicateDiagnostics(
            compatibility.ModelPolicies.Select(policy => policy.Name),
            "model policy identity",
            diagnostics,
            sourceFile);
        foreach (var policy in compatibility.ModelPolicies)
        {
            RequireText(policy.Name, "compatibility.modelPolicies[].name", diagnostics, sourceFile);
            RequireSha256(policy.Sha256, $"model policy '{policy.Name}' SHA-256", diagnostics, sourceFile);
        }
    }

    private static void ValidateSymbols(
        IReadOnlyList<RuntimeSymbolDocument>? symbols,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (symbols is null || symbols.Count == 0)
        {
            AddInvalid(diagnostics, sourceFile, "Required array 'symbols' must not be empty.");
            return;
        }

        ValidateOrdinalOrder(symbols.Select(symbol => symbol.ClrType), "symbols", diagnostics, sourceFile);
        AddDuplicateDiagnostics(
            symbols.Select(symbol => symbol.ClrType),
            "Runtime symbol CLR type",
            diagnostics,
            sourceFile);

        var symbolNames = symbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol.ClrType))
            .Select(symbol => symbol.ClrType!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            RequireText(symbol.ClrType, "symbols[].clrType", diagnostics, sourceFile);
            RequireText(symbol.Role, $"symbol '{symbol.ClrType}' role", diagnostics, sourceFile);
            if (!string.IsNullOrWhiteSpace(symbol.Role) &&
                !RuntimeContractRoles.SymbolRoles.Contains(symbol.Role))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.UnknownRuntimeContractRole,
                    sourceFile,
                    $"Runtime symbol '{symbol.ClrType}' has unknown role '{symbol.Role}'."));
            }
            else if (!string.IsNullOrWhiteSpace(symbol.Role))
            {
                ValidateRoleShape(symbol, symbols, diagnostics, sourceFile);
            }
            if (symbol.Kind is not ("class" or "interface"))
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime symbol '{symbol.ClrType}' has invalid kind '{symbol.Kind}'.");
            }
            if (symbol.IsAbstract is null || symbol.IsSealed is null || symbol.GenericArity is null)
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime symbol '{symbol.ClrType}' must specify abstract, sealed, and genericArity.");
            }
            else
            {
                if (symbol.IsAbstract.Value && symbol.IsSealed.Value && symbol.Kind == "class")
                {
                    AddInvalid(diagnostics, sourceFile,
                        $"Runtime class '{symbol.ClrType}' cannot be both abstract and sealed.");
                }
                if (symbol.GenericArity.Value < 0 ||
                    GetDeclaredArity(symbol.ClrType) != symbol.GenericArity.Value)
                {
                    AddInvalid(diagnostics, sourceFile,
                        $"Runtime symbol '{symbol.ClrType}' generic arity does not match its CLR name.");
                }
            }
            if (symbol.BaseClrType is not null &&
                symbol.BaseClrType != "System.Object" &&
                !symbolNames.Contains(symbol.BaseClrType))
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime symbol '{symbol.ClrType}' has unknown base CLR type '{symbol.BaseClrType}'.");
            }
            if (symbol.Interfaces is null)
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime symbol '{symbol.ClrType}' must specify an interfaces array.");
            }
            else
            {
                ValidateOrdinalOrder(symbol.Interfaces, $"interfaces for '{symbol.ClrType}'", diagnostics, sourceFile);
                AddDuplicateDiagnostics(symbol.Interfaces, $"interface on '{symbol.ClrType}'", diagnostics, sourceFile);
                foreach (var interfaceName in symbol.Interfaces.Where(name => !symbolNames.Contains(name)))
                {
                    AddInvalid(diagnostics, sourceFile,
                        $"Runtime symbol '{symbol.ClrType}' has unknown interface '{interfaceName}'.");
                }
            }
        }

        foreach (var role in RuntimeContractRoles.SymbolRoles.OrderBy(role => role, StringComparer.Ordinal))
        {
            var count = symbols.Count(symbol => string.Equals(symbol.Role, role, StringComparison.Ordinal));
            if (count != 1)
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime contract role '{role}' must occur exactly once; found {count}.");
            }
        }

        DetectInheritanceCycles(symbols, diagnostics, sourceFile);
    }

    private static void ValidateRoleShape(
        RuntimeSymbolDocument symbol,
        IReadOnlyList<RuntimeSymbolDocument> symbols,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        var role = symbol.Role!;
        var expectedKind = role == RuntimeContractRoles.ExtensionValueMarker
            ? "interface"
            : "class";
        if (!string.Equals(symbol.Kind, expectedKind, StringComparison.Ordinal))
        {
            AddInvalid(diagnostics, sourceFile,
                $"Runtime role '{role}' must have kind '{expectedKind}'.");
        }

        var concreteBootstrap = role is RuntimeContractRoles.ExtensionBootstrap or
            RuntimeContractRoles.MetaBootstrap or RuntimeContractRoles.NarrativeBootstrap;
        if (symbol.IsAbstract is not null && symbol.IsAbstract.Value == concreteBootstrap)
        {
            AddInvalid(diagnostics, sourceFile,
                $"Runtime role '{role}' has an invalid abstract modifier.");
        }
        if (symbol.IsSealed is not null && symbol.IsSealed.Value != concreteBootstrap)
        {
            AddInvalid(diagnostics, sourceFile,
                $"Runtime role '{role}' has an invalid sealed modifier.");
        }

        var expectedArity = role == RuntimeContractRoles.PrimitiveWrapperBase ? 1 : 0;
        if (symbol.GenericArity is not null && symbol.GenericArity.Value != expectedArity)
        {
            AddInvalid(diagnostics, sourceFile,
                $"Runtime role '{role}' must have generic arity '{expectedArity}'.");
        }

        var expectedBaseRole = role switch
        {
            RuntimeContractRoles.ModelRoot => "System.Object",
            RuntimeContractRoles.ExtensionValueMarker => null,
            RuntimeContractRoles.FoundationBase => RuntimeContractRoles.ModelRoot,
            RuntimeContractRoles.ElementFoundation => RuntimeContractRoles.FoundationBase,
            RuntimeContractRoles.BackboneElementFoundation => RuntimeContractRoles.ElementFoundation,
            RuntimeContractRoles.BackboneTypeFoundation => RuntimeContractRoles.DatatypeFoundation,
            RuntimeContractRoles.DatatypeFoundation => RuntimeContractRoles.ElementFoundation,
            RuntimeContractRoles.PrimitiveWrapperBase => RuntimeContractRoles.DatatypeFoundation,
            RuntimeContractRoles.ResourceFoundation => RuntimeContractRoles.FoundationBase,
            RuntimeContractRoles.DomainResourceFoundation => RuntimeContractRoles.ResourceFoundation,
            RuntimeContractRoles.ExtensionBootstrap => RuntimeContractRoles.ElementFoundation,
            RuntimeContractRoles.MetaBootstrap => RuntimeContractRoles.DatatypeFoundation,
            RuntimeContractRoles.NarrativeBootstrap => RuntimeContractRoles.DatatypeFoundation,
            _ => null
        };
        var expectedBaseType = expectedBaseRole == "System.Object"
            ? expectedBaseRole
            : symbols.FirstOrDefault(candidate =>
                string.Equals(candidate.Role, expectedBaseRole, StringComparison.Ordinal))?.ClrType;
        if (!string.Equals(symbol.BaseClrType, expectedBaseType, StringComparison.Ordinal))
        {
            AddInvalid(diagnostics, sourceFile,
                $"Runtime role '{role}' has base CLR type '{symbol.BaseClrType}'; expected '{expectedBaseType}'.");
        }

        if (role == RuntimeContractRoles.DatatypeFoundation)
        {
            var markerType = symbols.FirstOrDefault(candidate =>
                candidate.Role == RuntimeContractRoles.ExtensionValueMarker)?.ClrType;
            if (markerType is not null &&
                (symbol.Interfaces is null || !symbol.Interfaces.Contains(markerType, StringComparer.Ordinal)))
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime role '{role}' must implement extension-value marker '{markerType}'.");
            }
        }
    }

    private static void ValidateSlots(
        IReadOnlyList<RuntimeDeclaredSlotDocument>? slots,
        IReadOnlyList<RuntimeSymbolDocument>? symbols,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (slots is null || slots.Count == 0)
        {
            AddInvalid(diagnostics, sourceFile, "Required array 'declaredSlots' must not be empty.");
            return;
        }

        string Identity(RuntimeDeclaredSlotDocument slot) =>
            $"{slot.DeclaringClrType}|{slot.ClrPropertyName}";
        ValidateOrdinalOrder(slots.Select(Identity), "declaredSlots", diagnostics, sourceFile);
        AddDuplicateDiagnostics(slots.Select(Identity), "declared slot", diagnostics, sourceFile);
        var symbolNames = symbols?
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol.ClrType))
            .Select(symbol => symbol.ClrType!)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in slots)
        {
            RequireText(slot.DeclaringClrType, "declaredSlots[].declaringClrType", diagnostics, sourceFile);
            RequireText(slot.ClrPropertyName, "declaredSlots[].clrPropertyName", diagnostics, sourceFile);
            RequireText(slot.PropertyClrType, "declaredSlots[].propertyClrType", diagnostics, sourceFile);
            RequireText(slot.ElementClrType, "declaredSlots[].elementClrType", diagnostics, sourceFile);
            RequireText(slot.Role, "declaredSlots[].role", diagnostics, sourceFile);
            if (slot.IsCollection is null || slot.IsNullable is null)
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Declared slot '{Identity(slot)}' must specify collection and nullable.");
            }
            if (!string.IsNullOrWhiteSpace(slot.Role) &&
                !RuntimeContractRoles.SlotRoles.Contains(slot.Role))
            {
                diagnostics.Add(Diagnostic(
                    GeneratorDiagnosticCodes.UnknownRuntimeContractRole,
                    sourceFile,
                    $"Declared slot '{Identity(slot)}' has unknown role '{slot.Role}'."));
            }
            if (!string.IsNullOrWhiteSpace(slot.DeclaringClrType) &&
                !symbolNames.Contains(slot.DeclaringClrType))
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Declared slot '{Identity(slot)}' has an unknown declaring Runtime symbol.");
            }
            if (!string.IsNullOrWhiteSpace(slot.ElementClrType) &&
                !symbolNames.Contains(slot.ElementClrType))
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Declared slot '{Identity(slot)}' has an unknown element Runtime symbol.");
            }
        }

        foreach (var role in RuntimeContractRoles.SlotRoles.OrderBy(role => role, StringComparer.Ordinal))
        {
            if (!slots.Any(slot => string.Equals(slot.Role, role, StringComparison.Ordinal)))
            {
                AddInvalid(diagnostics, sourceFile,
                    $"Runtime declared slot role '{role}' must occur at least once.");
            }
        }
    }

    private static void ValidateCompilerReference(
        RuntimeCompilerReferenceDocument? reference,
        RuntimeAssemblyIdentityDocument? runtimeAssembly,
        string? targetFramework,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (reference is null)
        {
            AddInvalid(diagnostics, sourceFile, "Required object 'compilerReference' is missing.");
            return;
        }
        RequireText(reference.LogicalName, "compilerReference.logicalName", diagnostics, sourceFile);
        RequireText(reference.TargetFramework, "compilerReference.targetFramework", diagnostics, sourceFile);
        RequireSha256(reference.Sha256, "compilerReference.sha256", diagnostics, sourceFile);
        ValidateAssembly(reference.Assembly, "compilerReference.assembly", diagnostics, sourceFile);
        if (!string.Equals(reference.TargetFramework, targetFramework, StringComparison.Ordinal))
        {
            AddInvalid(diagnostics, sourceFile,
                $"Compiler reference target framework '{reference.TargetFramework}' does not match Runtime contract target framework '{targetFramework}'.");
        }
        if (reference.Assembly is not null && runtimeAssembly is not null &&
            (!string.Equals(reference.Assembly.Name, runtimeAssembly.Name, StringComparison.Ordinal) ||
             !string.Equals(reference.Assembly.Version, runtimeAssembly.Version, StringComparison.Ordinal) ||
             !string.Equals(reference.Assembly.PublicKeyToken, runtimeAssembly.PublicKeyToken, StringComparison.Ordinal)))
        {
            AddInvalid(diagnostics, sourceFile,
                "Compiler reference assembly identity does not match runtimeAssembly.");
        }
    }

    private static void ValidateAssembly(
        RuntimeAssemblyIdentityDocument? assembly,
        string field,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (assembly is null)
        {
            AddInvalid(diagnostics, sourceFile, $"Required object '{field}' is missing.");
            return;
        }
        RequireText(assembly.Name, $"{field}.name", diagnostics, sourceFile);
        RequireVersion(assembly.Version, $"{field}.version", diagnostics, sourceFile, components: 4);
        if (assembly.PublicKeyToken is null ||
            (assembly.PublicKeyToken != "null" && !PublicKeyTokenPattern().IsMatch(assembly.PublicKeyToken)))
        {
            AddInvalid(diagnostics, sourceFile,
                $"'{field}.publicKeyToken' must be 'null' or 16 lowercase hexadecimal characters.");
        }
    }

    private static RuntimeAssemblyIdentity CreateAssembly(RuntimeAssemblyIdentityDocument document) =>
        new(document.Name!, document.Version!, document.PublicKeyToken!);

    private static void DetectInheritanceCycles(
        IReadOnlyList<RuntimeSymbolDocument> symbols,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        var bases = symbols
            .Where(symbol => symbol.ClrType is not null)
            .GroupBy(symbol => symbol.ClrType!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().BaseClrType,
                StringComparer.Ordinal);
        foreach (var start in bases.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = start;
            while (bases.TryGetValue(current, out var next) && next is not null)
            {
                if (!visited.Add(current))
                {
                    AddInvalid(diagnostics, sourceFile,
                        $"Runtime symbol inheritance contains a cycle at '{current}'.");
                    break;
                }
                current = next;
            }
        }
    }

    private static int GetDeclaredArity(string? clrType)
    {
        if (string.IsNullOrWhiteSpace(clrType))
        {
            return 0;
        }
        var separator = clrType.LastIndexOf('`');
        return separator >= 0 && int.TryParse(clrType[(separator + 1)..], out var arity)
            ? arity
            : 0;
    }

    private static void ValidateOrdinalOrder(
        IEnumerable<string?> values,
        string field,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        var actual = values.Select(value => value ?? string.Empty).ToArray();
        var expected = actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            AddInvalid(diagnostics, sourceFile, $"'{field}' must use ordinal ordering.");
        }
    }

    private static void AddDuplicateDiagnostics(
        IEnumerable<string?> identities,
        string kind,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        foreach (var duplicate in identities
                     .Where(identity => !string.IsNullOrWhiteSpace(identity))
                     .GroupBy(identity => identity!, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            diagnostics.Add(Diagnostic(
                GeneratorDiagnosticCodes.DuplicateRuntimeContractEntry,
                sourceFile,
                $"Duplicate {kind} '{duplicate.Key}'."));
        }
    }

    private static void RequireText(
        string? value,
        string field,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddInvalid(diagnostics, sourceFile, $"Required field '{field}' is missing or blank.");
        }
    }

    private static void RequireVersion(
        string? value,
        string field,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile,
        int? components = null)
    {
        RequireText(value, field, diagnostics, sourceFile);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        var valid = components == 4
            ? AssemblyVersionPattern().IsMatch(value)
            : SemanticVersionPattern().IsMatch(value);
        if (!valid)
        {
            AddInvalid(diagnostics, sourceFile, $"Field '{field}' has invalid version '{value}'.");
        }
    }

    private static void RequireSha256(
        string? value,
        string field,
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile)
    {
        if (value is null || !Sha256Pattern().IsMatch(value))
        {
            AddInvalid(diagnostics, sourceFile,
                $"Field '{field}' must contain 64 lowercase hexadecimal characters.");
        }
    }

    private static void AddInvalid(
        ICollection<GeneratorDiagnostic> diagnostics,
        string sourceFile,
        string message) =>
        diagnostics.Add(Diagnostic(
            GeneratorDiagnosticCodes.InvalidRuntimeContract,
            sourceFile,
            message));

    private static GeneratorDiagnostic Diagnostic(
        string code,
        string sourceFile,
        string message) =>
        new(code, GeneratorDiagnosticSeverity.Error, message, sourceFile);

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AssemblyVersionPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex("^[0-9a-f]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex PublicKeyTokenPattern();
}
