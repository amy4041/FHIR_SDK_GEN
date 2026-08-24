using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Models;

public sealed class PrimitiveRegistryCompositionModel
{
    public PrimitiveRegistryCompositionModel(
        string @namespace,
        IEnumerable<PrimitiveRegistryEntryModel> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentNullException.ThrowIfNull(entries);

        var materialized = entries.ToArray();
        foreach (var entry in materialized)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.FhirTypeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.WrapperName);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.ClrValueType);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.CodecSymbol);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.ValidatorSymbol);
        }

        ThrowIfDuplicate(
            materialized,
            entry => entry.FhirTypeName,
            "FHIR type name");
        ThrowIfDuplicate(
            materialized,
            entry => entry.WrapperName,
            "wrapper name");

        Namespace = @namespace;
        Entries = new ReadOnlyCollection<PrimitiveRegistryEntryModel>(
            materialized);
    }

    public string Namespace { get; }

    public IReadOnlyList<PrimitiveRegistryEntryModel> Entries { get; }

    public string FileName => "PrimitiveRegistry.Composition.g.cs";

    private static void ThrowIfDuplicate(
        IEnumerable<PrimitiveRegistryEntryModel> entries,
        Func<PrimitiveRegistryEntryModel, string> selector,
        string label)
    {
        var duplicate = entries
            .GroupBy(selector, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Primitive registry {label} '{duplicate.Key}' is duplicated.",
                nameof(entries));
        }
    }
}

public sealed record PrimitiveRegistryEntryModel(
    string FhirTypeName,
    string WrapperName,
    string ClrValueType,
    string CodecSymbol,
    string ValidatorSymbol);
