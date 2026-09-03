using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Types;
using MyFhirSdk.Validation;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class R5ValidationCapabilityPolicyTests
{
    private static readonly Lazy<IReadOnlySet<string>> OpenTypeElementIds =
        new(ReadOpenTypeElementIds);
    private static readonly Lazy<IReadOnlyList<OfficialDefinition>> Definitions =
        new(ReadOfficialDefinitions);

    [Fact]
    public void CapabilityMatrixIsCompleteOrderedAndHasExplicitDispositions()
    {
        using var policy = ReadPolicy("r5-validation-capability-policy.json");
        var root = policy.RootElement;
        var scope = root.GetProperty("scope");
        var composition = root.GetProperty("compositionRules");
        var capabilities = root.GetProperty("capabilities")
            .EnumerateArray()
            .ToArray();
        var ids = capabilities
            .Select(capability => capability.GetProperty("id").GetString()!)
            .ToArray();

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("5.0.0", root.GetProperty("fhirVersion").GetString());
        Assert.Equal(
            "excluded-from-phase-c-model-validation-generation",
            scope.GetProperty("constraintProfiles").GetString());
        Assert.Equal(
            "model-agnostic-runtime",
            composition.GetProperty("ruleAlgorithmsOwner").GetString());
        Assert.Equal(
            "generated-model-metadata",
            composition.GetProperty("r5RuleEntriesOwner").GetString());
        Assert.False(
            composition.GetProperty("runtimeConcreteR5TypeBranchesAllowed").GetBoolean());
        Assert.False(
            composition.GetProperty("generatedClassValidationMethodsAllowed").GetBoolean());
        Assert.Equal(
            "c6-effective-rule-set-per-concrete-type",
            composition.GetProperty("inheritedRuleComposition").GetString());
        Assert.Equal(
            [
                "primitive-format",
                "collection-structure",
                "maximum-cardinality",
                "required-scalar",
                "required-collection",
                "ordinary-choice",
                "open-type-presence",
                "fhirpath-invariant",
                "terminology-binding",
                "fixed-value",
                "pattern-value",
                "slicing",
                "reference-target-profile"
            ],
            ids);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(capabilities, capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(
                capability.GetProperty("officialMetadata").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(
                capability.GetProperty("runtimeEvidence").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(
                capability.GetProperty("status").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(
                capability.GetProperty("c6Disposition").GetString()));
        });

        Assert.Equal(
            "existing-runtime-global",
            FindCapability(capabilities, "primitive-format").GetProperty("status").GetString());
        Assert.Equal(
            "generate-existing-runtime-rule",
            FindCapability(capabilities, "ordinary-choice").GetProperty("status").GetString());
        Assert.Equal(
            "preserve-only-runtime-extension-required",
            FindCapability(capabilities, "fhirpath-invariant").GetProperty("status").GetString());
        Assert.Equal(
            "preserve-only-runtime-extension-required",
            FindCapability(capabilities, "terminology-binding").GetProperty("status").GetString());
        Assert.Equal(
            "zero-in-specialization-scope-policy-update-required",
            FindCapability(capabilities, "fixed-value").GetProperty("status").GetString());
    }

    [Fact]
    public void OfficialGeneratedValidationInventoryMatchesApprovedR5Shape()
    {
        using var policy = ReadPolicy("r5-validation-capability-policy.json");
        using var ownership = ReadPolicy("r5-model-ownership-policy.json");
        using var choices = ReadPolicy("r5-choice-open-type-policy.json");
        var approved = policy.RootElement.GetProperty("approvedR5Inventory");
        var externalTypes = ownership.RootElement
            .GetProperty("externalDefinitionNodes")
            .EnumerateArray()
            .Select(node => node.GetProperty("fhirType").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var modelDefinitions = Definitions.Value
            .Where(definition =>
                definition.Derivation == "specialization" &&
                definition.Kind is "complex-type" or "resource")
            .ToArray();
        var generated = modelDefinitions
            .Where(definition => !externalTypes.Contains(definition.FhirType))
            .ToArray();
        var external = modelDefinitions
            .Where(definition => externalTypes.Contains(definition.FhirType))
            .ToArray();
        var generatedMetrics = CreateMetrics(generated);
        var externalMetrics = CreateMetrics(external);
        var choiceInventory = choices.RootElement.GetProperty("approvedR5Inventory");

        Assert.Equal(
            approved.GetProperty("modelSpecializationDefinitionCount").GetInt32(),
            modelDefinitions.Length);
        Assert.Equal(
            approved.GetProperty("generatedDefinitionCount").GetInt32(),
            generated.Length);
        Assert.Equal(
            approved.GetProperty("externalSpecializationDefinitionCount").GetInt32(),
            external.Length);
        Assert.Contains("Base", externalTypes);
        Assert.Equal(
            "Base",
            approved.GetProperty("externalMissingDerivationRoot").GetString());
        Assert.DoesNotContain(modelDefinitions, definition => definition.FhirType == "Base");
        AssertMetrics(approved, "generated", generatedMetrics);
        Assert.Equal(
            approved.GetProperty("externalDirectElementCount").GetInt32(),
            externalMetrics.DirectElements.Count);
        Assert.Equal(
            approved.GetProperty("externalRequiredScalarCount").GetInt32(),
            externalMetrics.DirectElements.Count(element => element.Min == 1 && element.Max == "1"));
        Assert.Equal(
            approved.GetProperty("externalDirectChoiceElementCount").GetInt32(),
            externalMetrics.DirectElements.Count(IsChoice));
        Assert.Equal(
            approved.GetProperty("externalDirectConstraintCount").GetInt32(),
            externalMetrics.DirectElements.Sum(element => element.ConstraintCount));
        Assert.Equal(
            approved.GetProperty("externalDirectBindingCount").GetInt32(),
            externalMetrics.DirectElements.Count(element => element.BindingStrength is not null));
        Assert.Equal(
            approved.GetProperty("externalDirectSlicingCount").GetInt32(),
            externalMetrics.DirectElements.Count(element => element.HasSlicing));
        Assert.Equal(
            choiceInventory.GetProperty("generatedScopeChoiceElementCount").GetInt32(),
            generatedMetrics.DirectElements.Count(IsChoice));
        Assert.Equal(
            choiceInventory.GetProperty("requiredChoiceElementCount").GetInt32(),
            generatedMetrics.DirectElements.Count(element => IsChoice(element) && element.Min == 1));
        Assert.All(
            generatedMetrics.DirectElements,
            element => Assert.True(element.Min is 0 or 1));
        Assert.All(
            generatedMetrics.DirectElements,
            element => Assert.True(element.Max is "1" or "*"));
    }

    [Fact]
    public void ConstraintBindingAndProfileOnlyMetadataCannotBeSilentlyClaimedAsSupported()
    {
        using var policy = ReadPolicy("r5-validation-capability-policy.json");
        using var reconnaissance = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "structuredefinition-reconnaissance.approved.json")));
        var rules = policy.RootElement.GetProperty("unsupportedCapabilityRules");
        var approved = policy.RootElement.GetProperty("approvedR5Inventory");
        var fullShape = reconnaissance.RootElement.GetProperty("shapeUsage");
        var specializationShape = reconnaissance.RootElement
            .GetProperty("specializationShapeUsage");

        Assert.False(rules.GetProperty("dropOfficialMetadataAllowed").GetBoolean());
        Assert.False(rules.GetProperty("claimFullR5ValidationAllowed").GetBoolean());
        Assert.False(rules.GetProperty("generateAdHocModelSpecificAlgorithmAllowed").GetBoolean());
        Assert.True(rules.GetProperty("runtimeExtensionMustBeModelAgnostic").GetBoolean());
        Assert.Equal(
            "diagnostic-before-render",
            rules.GetProperty("unapprovedOccurrenceDisposition").GetString());
        Assert.Equal(87, fullShape.GetProperty("fixedElementCount").GetInt32());
        Assert.Equal(33, fullShape.GetProperty("patternElementCount").GetInt32());
        Assert.Equal(0, specializationShape.GetProperty("fixedElementCount").GetInt32());
        Assert.Equal(0, specializationShape.GetProperty("patternElementCount").GetInt32());
        Assert.Equal(
            fullShape.GetProperty("fixedElementCount").GetInt32(),
            approved.GetProperty("excludedConstraintProfileFixedValueCount").GetInt32());
        Assert.Equal(
            fullShape.GetProperty("patternElementCount").GetInt32(),
            approved.GetProperty("excludedConstraintProfilePatternValueCount").GetInt32());
    }

    [Fact]
    public void RuntimeEvidenceMatchesExistingModelAgnosticRuleContracts()
    {
        var assembly = typeof(FhirValidator).Assembly;
        var requiredRule = GetInternalType(
            assembly,
            "MyFhirSdk.Validation.Rules.RequiredFieldRule`1");
        var choiceRule = GetInternalType(
            assembly,
            "MyFhirSdk.Validation.Rules.ChoiceElementRule`1");

        Assert.NotNull(GetInternalType(
            assembly,
            "MyFhirSdk.Validation.Rules.PrimitiveFormatRule"));
        Assert.NotNull(GetInternalType(
            assembly,
            "MyFhirSdk.Validation.Rules.CardinalityRule"));
        Assert.NotNull(GetInternalType(
            assembly,
            "MyFhirSdk.Validation.Traversal.FhirObjectGraphWalker"));
        Assert.Contains(requiredRule.GetMethods(), method => method.Name == "For");
        Assert.Contains(requiredRule.GetMethods(), method => method.Name == "ForList");
        Assert.Contains(choiceRule.GetMethods(), method => method.Name == "AtMostOne");
        Assert.Contains(choiceRule.GetMethods(), method => method.Name == "ExactlyOne");
        Assert.Equal(
            ["Required", "Cardinality", "PrimitiveFormat", "ChoiceElement", "Profile"],
            Enum.GetNames<ValidationIssueCode>());
        Assert.Null(assembly.GetType("MyFhirSdk.Validation.Rules.FhirPathConstraintRule"));
        Assert.Null(assembly.GetType("MyFhirSdk.Validation.Rules.TerminologyBindingRule"));
        Assert.Null(assembly.GetType("MyFhirSdk.Validation.Rules.FixedValueRule"));
        Assert.Null(assembly.GetType("MyFhirSdk.Validation.Rules.PatternValueRule"));
    }

    [Fact]
    public void ExistingValidatorBehaviorCoversTheApprovedExecutableBaseline()
    {
        var validator = new FhirValidator();
        var bundleResult = validator.Validate(new Bundle());
        var patient = new Patient
        {
            Name = null!,
            BirthDate = new FhirDate("2026-99-99"),
            DeceasedBoolean = new FhirBoolean(true),
            DeceasedDateTime = new FhirDateTime("2026-01-01")
        };
        var patientResult = validator.Validate(patient);

        Assert.Contains(bundleResult.Issues, issue =>
            issue.Path == "Bundle.type" && issue.Code == ValidationIssueCode.Required);
        Assert.Contains(patientResult.Issues, issue =>
            issue.Path == "Patient.name" && issue.Code == ValidationIssueCode.Cardinality);
        Assert.Contains(patientResult.Issues, issue =>
            issue.Path == "Patient.birthDate" && issue.Code == ValidationIssueCode.PrimitiveFormat);
        Assert.Contains(patientResult.Issues, issue =>
            issue.Path == "Patient.deceased[x]" && issue.Code == ValidationIssueCode.ChoiceElement);
    }

    private static void AssertMetrics(
        JsonElement approved,
        string prefix,
        ValidationMetrics metrics)
    {
        var direct = metrics.DirectElements;
        var strengths = direct
            .Where(element => element.BindingStrength is not null)
            .GroupBy(element => element.BindingStrength!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(approved.GetProperty(prefix + "DirectElementCount").GetInt32(), direct.Count);
        Assert.Equal(
            approved.GetProperty(prefix + "EffectiveElementCountExcludingRoots").GetInt32(),
            metrics.EffectiveElementCountExcludingRoots);
        Assert.Equal(
            approved.GetProperty(prefix + "OptionalDirectElementCount").GetInt32(),
            direct.Count(element => element.Min == 0));
        Assert.Equal(
            approved.GetProperty(prefix + "RequiredDirectElementCount").GetInt32(),
            direct.Count(element => element.Min == 1));
        Assert.Equal(
            approved.GetProperty(prefix + "MaxOneDirectElementCount").GetInt32(),
            direct.Count(element => element.Max == "1"));
        Assert.Equal(
            approved.GetProperty(prefix + "MaxUnboundedDirectElementCount").GetInt32(),
            direct.Count(element => element.Max == "*"));
        Assert.Equal(
            approved.GetProperty(prefix + "FiniteCollectionMaxCount").GetInt32(),
            direct.Count(element => element.Max is not "1" and not "*"));
        Assert.Equal(
            approved.GetProperty(prefix + "RequiredScalarIncludingChoiceCount").GetInt32(),
            direct.Count(element => element.Min == 1 && element.Max == "1"));
        Assert.Equal(
            approved.GetProperty(prefix + "RequiredNonChoiceScalarCount").GetInt32(),
            direct.Count(element => element.Min == 1 && element.Max == "1" && !IsChoice(element)));
        Assert.Equal(
            approved.GetProperty(prefix + "RequiredCollectionCount").GetInt32(),
            direct.Count(element => element.Min == 1 && element.Max == "*"));
        Assert.Equal(
            approved.GetProperty(prefix + "RequiredOrdinaryChoiceCount").GetInt32(),
            direct.Count(element =>
                IsChoice(element) &&
                element.Min == 1 &&
                !IsOpenType(element.Id)));
        Assert.Equal(
            approved.GetProperty(prefix + "OptionalOrdinaryChoiceCount").GetInt32(),
            direct.Count(element =>
                IsChoice(element) &&
                element.Min == 0 &&
                !IsOpenType(element.Id)));
        Assert.Equal(
            approved.GetProperty(prefix + "RequiredOpenTypeCount").GetInt32(),
            direct.Count(element => element.Min == 1 && IsOpenType(element.Id)));
        Assert.Equal(
            approved.GetProperty(prefix + "OptionalOpenTypeCount").GetInt32(),
            direct.Count(element => element.Min == 0 && IsOpenType(element.Id)));
        Assert.Equal(
            approved.GetProperty(prefix + "DirectConstraintCount").GetInt32(),
            direct.Sum(element => element.ConstraintCount));
        Assert.Equal(
            approved.GetProperty(prefix + "DirectElementsWithConstraints").GetInt32(),
            direct.Count(element => element.ConstraintCount > 0));
        Assert.Equal(
            approved.GetProperty(prefix + "DirectBindingCount").GetInt32(),
            strengths.Values.Sum());
        foreach (var strength in approved.GetProperty(prefix + "BindingStrengthCounts").EnumerateObject())
        {
            Assert.Equal(strength.Value.GetInt32(), strengths[strength.Name]);
        }
        Assert.Equal(
            approved.GetProperty(prefix + "DirectFixedValueCount").GetInt32(),
            direct.Count(element => element.HasFixed));
        Assert.Equal(
            approved.GetProperty(prefix + "DirectPatternValueCount").GetInt32(),
            direct.Count(element => element.HasPattern));
        Assert.Equal(
            approved.GetProperty(prefix + "DirectSlicingCount").GetInt32(),
            direct.Count(element => element.HasSlicing));
    }

    private static bool IsOpenType(string elementId) =>
        OpenTypeElementIds.Value.Contains(elementId);

    private static IReadOnlySet<string> ReadOpenTypeElementIds()
    {
        using var policy = ReadPolicy("r5-choice-open-type-policy.json");
        return policy.RootElement
            .GetProperty("classification")
            .GetProperty("openTypeElementIds")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static ValidationMetrics CreateMetrics(
        IReadOnlyList<OfficialDefinition> definitions)
    {
        var direct = definitions
            .SelectMany(definition => definition.Elements.Where(element =>
                element.Id != definition.FhirType &&
                (element.BasePath == definition.FhirType ||
                 element.BasePath.StartsWith(
                     definition.FhirType + ".",
                     StringComparison.Ordinal))))
            .OrderBy(element => element.Id, StringComparer.Ordinal)
            .ToArray();
        var effectiveCount = definitions.Sum(definition =>
            definition.Elements.Count(element => element.Id != definition.FhirType));

        return new ValidationMetrics(direct, effectiveCount);
    }

    private static bool IsChoice(OfficialElement element) =>
        element.Id.EndsWith("[x]", StringComparison.Ordinal);

    private static JsonElement FindCapability(
        IReadOnlyList<JsonElement> capabilities,
        string id) =>
        capabilities.Single(capability =>
            capability.GetProperty("id").GetString() == id);

    private static Type GetInternalType(Assembly assembly, string name) =>
        assembly.GetType(name, throwOnError: true)!;

    private static JsonDocument ReadPolicy(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            fileName)));

    private static IReadOnlyList<OfficialDefinition> ReadOfficialDefinitions()
    {
        var result = new List<OfficialDefinition>();
        using var archive = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "FhirPackages",
            "R5",
            "hl7.fhir.r5.core-5.0.0.tgz"));
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (entry.DataStream is null ||
                !entry.Name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(entry.DataStream);
            var root = document.RootElement;
            if (!root.TryGetProperty("resourceType", out var resourceType) ||
                resourceType.GetString() != "StructureDefinition" ||
                !root.TryGetProperty("type", out var type) ||
                !root.TryGetProperty("kind", out var kind) ||
                !root.TryGetProperty("snapshot", out var snapshot) ||
                !snapshot.TryGetProperty("element", out var elements))
            {
                continue;
            }

            result.Add(new OfficialDefinition(
                type.GetString()!,
                kind.GetString()!,
                root.TryGetProperty("derivation", out var derivation)
                    ? derivation.GetString()
                    : null,
                elements.EnumerateArray().Select(ReadElement).ToArray()));
        }

        return result;
    }

    private static OfficialElement ReadElement(JsonElement element)
    {
        var propertyNames = element
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        return new OfficialElement(
            element.GetProperty("id").GetString()!,
            element.TryGetProperty("base", out var baseInfo)
                ? baseInfo.GetProperty("path").GetString()!
                : element.GetProperty("path").GetString()!,
            element.TryGetProperty("min", out var min) ? min.GetInt32() : 0,
            element.TryGetProperty("max", out var max) ? max.GetString()! : "",
            element.TryGetProperty("constraint", out var constraints)
                ? constraints.GetArrayLength()
                : 0,
            element.TryGetProperty("binding", out var binding) &&
                binding.TryGetProperty("strength", out var strength)
                    ? strength.GetString()
                    : null,
            propertyNames.Any(name => name.StartsWith("fixed", StringComparison.Ordinal)),
            propertyNames.Any(name => name.StartsWith("pattern", StringComparison.Ordinal)),
            propertyNames.Contains("slicing"));
    }

    private sealed record OfficialDefinition(
        string FhirType,
        string Kind,
        string? Derivation,
        IReadOnlyList<OfficialElement> Elements);

    private sealed record OfficialElement(
        string Id,
        string BasePath,
        int Min,
        string Max,
        int ConstraintCount,
        string? BindingStrength,
        bool HasFixed,
        bool HasPattern,
        bool HasSlicing);

    private sealed record ValidationMetrics(
        IReadOnlyList<OfficialElement> DirectElements,
        int EffectiveElementCountExcludingRoots);
}
