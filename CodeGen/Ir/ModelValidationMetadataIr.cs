using System.Collections.ObjectModel;

namespace MyFhirSdk.CodeGen.Ir;

public sealed class ModelValidationMetadataIr
{
    internal ModelValidationMetadataIr(
        IEnumerable<ModelConstraintIr> constraints,
        ModelBindingIr? binding,
        bool? mustSupport,
        bool? isModifier,
        string? isModifierReason,
        bool? isSummary,
        IEnumerable<string> conditions,
        string? slicingJson,
        IEnumerable<ModelRawValueIr> fixedValues,
        IEnumerable<ModelRawValueIr> patternValues,
        string? label,
        IEnumerable<string> aliases,
        IEnumerable<string> representations,
        string? comment,
        string? requirements,
        string? meaningWhenMissing,
        string? orderMeaning)
    {
        Constraints = new ReadOnlyCollection<ModelConstraintIr>(constraints.ToArray());
        Binding = binding;
        MustSupport = mustSupport;
        IsModifier = isModifier;
        IsModifierReason = isModifierReason;
        IsSummary = isSummary;
        Conditions = new ReadOnlyCollection<string>(conditions.ToArray());
        SlicingJson = slicingJson;
        FixedValues = new ReadOnlyCollection<ModelRawValueIr>(fixedValues.ToArray());
        PatternValues = new ReadOnlyCollection<ModelRawValueIr>(patternValues.ToArray());
        Label = label;
        Aliases = new ReadOnlyCollection<string>(aliases.ToArray());
        Representations = new ReadOnlyCollection<string>(representations.ToArray());
        Comment = comment;
        Requirements = requirements;
        MeaningWhenMissing = meaningWhenMissing;
        OrderMeaning = orderMeaning;
    }

    public IReadOnlyList<ModelConstraintIr> Constraints { get; }

    public ModelBindingIr? Binding { get; }

    public bool? MustSupport { get; }

    public bool? IsModifier { get; }

    public string? IsModifierReason { get; }

    public bool? IsSummary { get; }

    public IReadOnlyList<string> Conditions { get; }

    public string? SlicingJson { get; }

    public IReadOnlyList<ModelRawValueIr> FixedValues { get; }

    public IReadOnlyList<ModelRawValueIr> PatternValues { get; }

    public string? Label { get; }

    public IReadOnlyList<string> Aliases { get; }

    public IReadOnlyList<string> Representations { get; }

    public string? Comment { get; }

    public string? Requirements { get; }

    public string? MeaningWhenMissing { get; }

    public string? OrderMeaning { get; }
}

public sealed record ModelConstraintIr(
    string Key,
    string Severity,
    string Human,
    string? Expression,
    string? Source);

public sealed record ModelBindingIr(
    string Strength,
    string? Description,
    string? ValueSet);

public sealed record ModelRawValueIr(
    string JsonPropertyName,
    string JsonValue);
