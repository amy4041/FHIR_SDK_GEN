using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using MyFhirSdk.CodeGen.Diagnostics;

namespace MyFhirSdk.CodeGen.Policy;

public sealed class PrimitiveGenerationPolicyValidator
{
    public const int SupportedSchemaVersion = 1;

    private static readonly Regex SemanticVersionPattern = new(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)" +
        "(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?" +
        "(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly IReadOnlyDictionary<string, PrimitiveJsonToken>
        JsonTokens = new Dictionary<string, PrimitiveJsonToken>(StringComparer.Ordinal)
        {
            ["string"] = PrimitiveJsonToken.String,
            ["boolean"] = PrimitiveJsonToken.Boolean,
            ["number"] = PrimitiveJsonToken.Number
        };

    private static readonly IReadOnlyDictionary<string, PrimitiveCodecKey>
        CodecKeys = new Dictionary<string, PrimitiveCodecKey>(StringComparer.Ordinal)
        {
            ["string"] = PrimitiveCodecKey.String,
            ["boolean"] = PrimitiveCodecKey.Boolean,
            ["integer"] = PrimitiveCodecKey.Integer,
            ["decimal-literal"] = PrimitiveCodecKey.DecimalLiteral,
            ["integer64-literal"] = PrimitiveCodecKey.Integer64Literal
        };

    private static readonly IReadOnlyDictionary<string, PrimitiveValidatorKey>
        ValidatorKeys = new Dictionary<string, PrimitiveValidatorKey>(StringComparer.Ordinal)
        {
            ["base64Binary"] = PrimitiveValidatorKey.Base64Binary,
            ["boolean"] = PrimitiveValidatorKey.Boolean,
            ["canonical"] = PrimitiveValidatorKey.Canonical,
            ["code"] = PrimitiveValidatorKey.Code,
            ["date"] = PrimitiveValidatorKey.Date,
            ["dateTime"] = PrimitiveValidatorKey.DateTime,
            ["decimal"] = PrimitiveValidatorKey.Decimal,
            ["id"] = PrimitiveValidatorKey.Id,
            ["instant"] = PrimitiveValidatorKey.Instant,
            ["integer"] = PrimitiveValidatorKey.Integer,
            ["integer64"] = PrimitiveValidatorKey.Integer64,
            ["markdown"] = PrimitiveValidatorKey.Markdown,
            ["positiveInt"] = PrimitiveValidatorKey.PositiveInt,
            ["string"] = PrimitiveValidatorKey.String,
            ["unsignedInt"] = PrimitiveValidatorKey.UnsignedInt,
            ["uri"] = PrimitiveValidatorKey.Uri,
            ["url"] = PrimitiveValidatorKey.Url
        };

    private static readonly IReadOnlyDictionary<string, PrimitiveToStringBehavior>
        ToStringBehaviors =
            new Dictionary<string, PrimitiveToStringBehavior>(StringComparer.Ordinal)
            {
                ["inherited"] = PrimitiveToStringBehavior.Inherited,
                ["boolean-lowercase"] = PrimitiveToStringBehavior.BooleanLowercase,
                ["invariant-value"] = PrimitiveToStringBehavior.InvariantValue,
                ["literal-or-invariant-value"] =
                    PrimitiveToStringBehavior.LiteralOrInvariantValue
            };

    private static readonly IReadOnlySet<string> ClrValueTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "string",
            "bool?",
            "decimal?",
            "int?",
            "long?"
        };

    private static readonly IReadOnlyDictionary<PrimitiveValidatorKey, string>
        ValidatorClrValueTypes =
            new Dictionary<PrimitiveValidatorKey, string>
            {
                [PrimitiveValidatorKey.Base64Binary] = "string",
                [PrimitiveValidatorKey.Boolean] = "bool?",
                [PrimitiveValidatorKey.Canonical] = "string",
                [PrimitiveValidatorKey.Code] = "string",
                [PrimitiveValidatorKey.Date] = "string",
                [PrimitiveValidatorKey.DateTime] = "string",
                [PrimitiveValidatorKey.Decimal] = "decimal?",
                [PrimitiveValidatorKey.Id] = "string",
                [PrimitiveValidatorKey.Instant] = "string",
                [PrimitiveValidatorKey.Integer] = "int?",
                [PrimitiveValidatorKey.Integer64] = "long?",
                [PrimitiveValidatorKey.Markdown] = "string",
                [PrimitiveValidatorKey.PositiveInt] = "int?",
                [PrimitiveValidatorKey.String] = "string",
                [PrimitiveValidatorKey.UnsignedInt] = "int?",
                [PrimitiveValidatorKey.Uri] = "string",
                [PrimitiveValidatorKey.Url] = "string"
            };

    public GenerationResult<ValidatedPrimitiveGenerationPolicy?> Validate(
        PrimitiveGenerationPolicyDocument document,
        string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(document);

        var source = string.IsNullOrWhiteSpace(sourceFile)
            ? "<primitive-generation-policy>"
            : sourceFile;
        var diagnostics = new List<GeneratorDiagnostic>();

        ValidateTopLevel(document, source, diagnostics);
        var entries = document.Primitives ?? [];
        ValidateUniqueness(entries, source, diagnostics);

        var validatedEntries = new List<ValidatedPrimitivePolicyEntry>();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive policy entry at index {index} is null."));
                continue;
            }

            var diagnosticCount = diagnostics.Count;
            var validated = ValidateEntry(
                entry,
                document.FhirVersion,
                source,
                diagnostics);
            if (validated is not null && diagnostics.Count == diagnosticCount)
            {
                validatedEntries.Add(validated);
            }
        }

        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
            .ThenBy(
                diagnostic => diagnostic.DefinitionCanonical,
                StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();

        if (orderedDiagnostics.Length > 0)
        {
            return new GenerationResult<ValidatedPrimitiveGenerationPolicy?>(
                null,
                orderedDiagnostics);
        }

        var validatedPolicy = new ValidatedPrimitiveGenerationPolicy(
            source,
            document.SchemaVersion!.Value,
            document.PolicyVersion!,
            document.FhirVersion!,
            document.RuntimeContractVersion!,
            document.PrimitiveNamespace!,
            validatedEntries.OrderBy(
                entry => entry.FhirTypeName,
                StringComparer.Ordinal));

        return new GenerationResult<ValidatedPrimitiveGenerationPolicy?>(
            validatedPolicy,
            Array.Empty<GeneratorDiagnostic>());
    }

    private static void ValidateTopLevel(
        PrimitiveGenerationPolicyDocument document,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (document.SchemaVersion is null)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                "Required policy field 'schemaVersion' is missing."));
        }
        else if (document.SchemaVersion != SupportedSchemaVersion)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.UnsupportedPrimitivePolicySchema,
                source,
                $"Primitive policy schema version '{document.SchemaVersion}' is not " +
                $"supported; expected '{SupportedSchemaVersion}'."));
        }

        RequireSemanticVersion(
            document.PolicyVersion,
            "policyVersion",
            source,
            diagnostics);
        RequireSemanticVersion(
            document.FhirVersion,
            "fhirVersion",
            source,
            diagnostics);
        RequireText(
            document.RuntimeContractVersion,
            "runtimeContractVersion",
            source,
            diagnostics);

        if (string.IsNullOrWhiteSpace(document.PrimitiveNamespace))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                "Required policy field 'primitiveNamespace' is missing."));
        }
        else if (!IsValidNamespace(document.PrimitiveNamespace))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"Primitive namespace '{document.PrimitiveNamespace}' is not a valid " +
                "C# namespace."));
        }

        if (document.Primitives is null)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                "Required policy field 'primitives' is missing."));
        }
        else if (document.Primitives.Count == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                "Primitive policy must contain at least one entry."));
        }
    }

    private static ValidatedPrimitivePolicyEntry? ValidateEntry(
        PrimitiveGenerationPolicyEntryDocument entry,
        string? policyFhirVersion,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var canonical = entry.Canonical;
        var version = entry.FhirVersion;
        var diagnosticCount = diagnostics.Count;

        RequireText(entry.FhirTypeName, "fhirTypeName", source, diagnostics, entry);
        RequireText(canonical, "canonical", source, diagnostics, entry);
        RequireSemanticVersion(version, "fhirVersion", source, diagnostics, entry);

        if (!string.IsNullOrWhiteSpace(entry.FhirTypeName) &&
            !string.IsNullOrWhiteSpace(canonical))
        {
            var expectedCanonical =
                $"http://hl7.org/fhir/StructureDefinition/{entry.FhirTypeName}";
            if (!string.Equals(canonical, expectedCanonical, StringComparison.Ordinal))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' canonical '{canonical}' does not " +
                    $"match '{expectedCanonical}'.",
                    entry));
            }
        }

        if (!string.IsNullOrWhiteSpace(version) &&
            !string.IsNullOrWhiteSpace(policyFhirVersion) &&
            !string.Equals(version, policyFhirVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"Primitive '{entry.FhirTypeName}' FHIR version '{version}' does not " +
                $"match policy version '{policyFhirVersion}'.",
                entry));
        }

        var supportStatus = ParseSupportStatus(entry, source, diagnostics);
        if (supportStatus == PrimitiveSupportStatus.Supported)
        {
            return ValidateSupportedEntry(
                entry,
                source,
                diagnostics,
                diagnosticCount);
        }

        if (supportStatus == PrimitiveSupportStatus.Unsupported)
        {
            return ValidateUnsupportedEntry(
                entry,
                source,
                diagnostics,
                diagnosticCount);
        }

        return null;
    }

    private static ValidatedPrimitivePolicyEntry? ValidateSupportedEntry(
        PrimitiveGenerationPolicyEntryDocument entry,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics,
        int diagnosticCount)
    {
        RequireText(entry.WrapperName, "wrapperName", source, diagnostics, entry);
        RequireText(entry.ClrValueType, "clrValueType", source, diagnostics, entry);
        RequireText(entry.JsonToken, "jsonToken", source, diagnostics, entry);
        RequireText(entry.CodecKey, "codecKey", source, diagnostics, entry);
        RequireText(entry.ValidatorKey, "validatorKey", source, diagnostics, entry);
        RequireText(
            entry.ToStringBehavior,
            "toStringBehavior",
            source,
            diagnostics,
            entry);

        if (!string.IsNullOrWhiteSpace(entry.UnsupportedReason))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"Supported primitive '{entry.FhirTypeName}' must not define " +
                "'unsupportedReason'.",
                entry));
        }

        if (!string.IsNullOrWhiteSpace(entry.WrapperName) &&
            !SyntaxFacts.IsValidIdentifier(entry.WrapperName))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"Wrapper name '{entry.WrapperName}' is not a valid C# identifier.",
                entry));
        }

        if (!string.IsNullOrWhiteSpace(entry.ClrValueType) &&
            !ClrValueTypes.Contains(entry.ClrValueType))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"CLR value type '{entry.ClrValueType}' is not supported by the " +
                "primitive policy schema.",
                entry));
        }

        var jsonToken = ParseClosedKey(
            entry.JsonToken,
            "jsonToken",
            JsonTokens,
            source,
            diagnostics,
            entry,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy);
        var codecKey = ParseClosedKey(
            entry.CodecKey,
            "codecKey",
            CodecKeys,
            source,
            diagnostics,
            entry,
            GeneratorDiagnosticCodes.UnknownPrimitivePolicyKey);
        var validatorKey = ParseClosedKey(
            entry.ValidatorKey,
            "validatorKey",
            ValidatorKeys,
            source,
            diagnostics,
            entry,
            GeneratorDiagnosticCodes.UnknownPrimitivePolicyKey);
        var toStringBehavior = ParseClosedKey(
            entry.ToStringBehavior,
            "toStringBehavior",
            ToStringBehaviors,
            source,
            diagnostics,
            entry,
            GeneratorDiagnosticCodes.UnknownPrimitivePolicyKey);

        if (entry.PreserveLiteral is null)
        {
            AddMissingField("preserveLiteral", source, diagnostics, entry);
        }

        if (entry.LiteralConstructor is null)
        {
            AddMissingField("literalConstructor", source, diagnostics, entry);
        }

        if (entry.PublicConstants is null)
        {
            AddMissingField("publicConstants", source, diagnostics, entry);
        }

        var constants = ValidateConstants(entry, source, diagnostics);
        ValidateLiteralContract(
            entry,
            jsonToken,
            codecKey,
            toStringBehavior,
            source,
            diagnostics);
        ValidateStandardCodecContract(
            entry,
            jsonToken,
            codecKey,
            source,
            diagnostics);
        ValidateValidatorContract(
            entry,
            validatorKey,
            source,
            diagnostics);
        ValidateToStringContract(
            entry,
            toStringBehavior,
            source,
            diagnostics);

        return diagnostics.Count == diagnosticCount
            ? new ValidatedPrimitivePolicyEntry(
                entry.FhirTypeName!,
                entry.Canonical!,
                entry.FhirVersion!,
                PrimitiveSupportStatus.Supported,
                null,
                entry.WrapperName!,
                entry.ClrValueType!,
                jsonToken!.Value,
                codecKey!.Value,
                validatorKey!.Value,
                entry.PreserveLiteral!.Value,
                entry.LiteralConstructor!.Value,
                NullIfWhiteSpace(entry.LiteralPropertyName),
                toStringBehavior!.Value,
                constants)
            : null;
    }

    private static ValidatedPrimitivePolicyEntry? ValidateUnsupportedEntry(
        PrimitiveGenerationPolicyEntryDocument entry,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics,
        int diagnosticCount)
    {
        RequireText(
            entry.UnsupportedReason,
            "unsupportedReason",
            source,
            diagnostics,
            entry);

        var generationFields = new List<string>();
        AddIfPresent(generationFields, "wrapperName", entry.WrapperName);
        AddIfPresent(generationFields, "clrValueType", entry.ClrValueType);
        AddIfPresent(generationFields, "jsonToken", entry.JsonToken);
        AddIfPresent(generationFields, "codecKey", entry.CodecKey);
        AddIfPresent(generationFields, "validatorKey", entry.ValidatorKey);
        AddIfPresent(generationFields, "literalPropertyName", entry.LiteralPropertyName);
        AddIfPresent(generationFields, "toStringBehavior", entry.ToStringBehavior);
        if (entry.PreserveLiteral is not null)
        {
            generationFields.Add("preserveLiteral");
        }

        if (entry.LiteralConstructor is not null)
        {
            generationFields.Add("literalConstructor");
        }

        if (entry.PublicConstants is { Count: > 0 })
        {
            generationFields.Add("publicConstants");
        }

        if (generationFields.Count > 0)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"Unsupported primitive '{entry.FhirTypeName}' must not define " +
                $"generation fields: {string.Join(", ", generationFields.Order(StringComparer.Ordinal))}.",
                entry));
        }

        return diagnostics.Count == diagnosticCount
            ? new ValidatedPrimitivePolicyEntry(
                entry.FhirTypeName!,
                entry.Canonical!,
                entry.FhirVersion!,
                PrimitiveSupportStatus.Unsupported,
                entry.UnsupportedReason!,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                null,
                null,
                Array.Empty<PrimitivePublicConstant>())
            : null;
    }

    private static IReadOnlyList<PrimitivePublicConstant> ValidateConstants(
        PrimitiveGenerationPolicyEntryDocument entry,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var constants = entry.PublicConstants ?? [];
        var result = new List<PrimitivePublicConstant>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < constants.Count; index++)
        {
            var constant = constants[index];
            if (constant is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' public constant at index " +
                    $"{index} is null.",
                    entry));
                continue;
            }

            if (string.IsNullOrWhiteSpace(constant.Name) ||
                !SyntaxFacts.IsValidIdentifier(constant.Name))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' public constant name " +
                    $"'{constant.Name}' is not a valid C# identifier.",
                    entry));
                continue;
            }

            if (!names.Add(constant.Name))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry,
                    source,
                    $"Primitive '{entry.FhirTypeName}' has duplicate public constant " +
                    $"'{constant.Name}'.",
                    entry));
                continue;
            }

            var clrType = constant.ClrType switch
            {
                "int" => PrimitiveConstantClrType.Int32,
                "long" => PrimitiveConstantClrType.Int64,
                _ => (PrimitiveConstantClrType?)null
            };
            if (clrType is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' public constant " +
                    $"'{constant.Name}' has unsupported CLR type '{constant.ClrType}'.",
                    entry));
                continue;
            }

            if (constant.Value is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' public constant " +
                    $"'{constant.Name}' has no value.",
                    entry));
                continue;
            }

            if (clrType == PrimitiveConstantClrType.Int32 &&
                (constant.Value < int.MinValue || constant.Value > int.MaxValue))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' public constant " +
                    $"'{constant.Name}' value is outside Int32 range.",
                    entry));
                continue;
            }

            result.Add(new PrimitivePublicConstant(
                constant.Name,
                clrType.Value,
                constant.Value.Value));
        }

        return result
            .OrderBy(constant => constant.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateLiteralContract(
        PrimitiveGenerationPolicyEntryDocument entry,
        PrimitiveJsonToken? jsonToken,
        PrimitiveCodecKey? codecKey,
        PrimitiveToStringBehavior? toStringBehavior,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (entry.PreserveLiteral is null ||
            entry.LiteralConstructor is null ||
            codecKey is null)
        {
            return;
        }

        var isLiteralCodec = codecKey is
            PrimitiveCodecKey.DecimalLiteral or
            PrimitiveCodecKey.Integer64Literal;

        if (!isLiteralCodec)
        {
            if (entry.PreserveLiteral.Value ||
                entry.LiteralConstructor.Value ||
                !string.IsNullOrWhiteSpace(entry.LiteralPropertyName))
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
                    source,
                    $"Non-literal codec '{entry.CodecKey}' for primitive " +
                    $"'{entry.FhirTypeName}' cannot require literal preservation.",
                    entry));
            }

            if (toStringBehavior == PrimitiveToStringBehavior.LiteralOrInvariantValue)
            {
                diagnostics.Add(CreateDiagnostic(
                    GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
                    source,
                    $"Primitive '{entry.FhirTypeName}' cannot use literal ToString " +
                    "behavior without a literal codec.",
                    entry));
            }

            return;
        }

        if (!entry.PreserveLiteral.Value ||
            !entry.LiteralConstructor.Value ||
            !string.Equals(
                entry.LiteralPropertyName,
                "Literal",
                StringComparison.Ordinal) ||
            toStringBehavior != PrimitiveToStringBehavior.LiteralOrInvariantValue)
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
                source,
                $"Literal codec '{entry.CodecKey}' for primitive " +
                $"'{entry.FhirTypeName}' requires literal preservation, a string " +
                "constructor, public 'Literal', and literal-or-invariant-value ToString behavior.",
                entry));
        }

        if (codecKey == PrimitiveCodecKey.DecimalLiteral &&
            (jsonToken != PrimitiveJsonToken.Number ||
             !string.Equals(entry.ClrValueType, "decimal?", StringComparison.Ordinal)))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
                source,
                "Codec 'decimal-literal' requires JSON number and CLR type 'decimal?'.",
                entry));
        }

        if (codecKey == PrimitiveCodecKey.Integer64Literal &&
            (jsonToken != PrimitiveJsonToken.String ||
             !string.Equals(entry.ClrValueType, "long?", StringComparison.Ordinal)))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
                source,
                "Codec 'integer64-literal' requires JSON string and CLR type 'long?'.",
                entry));
        }
    }

    private static void ValidateStandardCodecContract(
        PrimitiveGenerationPolicyEntryDocument entry,
        PrimitiveJsonToken? jsonToken,
        PrimitiveCodecKey? codecKey,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var expected = codecKey switch
        {
            PrimitiveCodecKey.String =>
                (Token: PrimitiveJsonToken.String, ClrType: "string"),
            PrimitiveCodecKey.Boolean =>
                (Token: PrimitiveJsonToken.Boolean, ClrType: "bool?"),
            PrimitiveCodecKey.Integer =>
                (Token: PrimitiveJsonToken.Number, ClrType: "int?"),
            _ => ((PrimitiveJsonToken Token, string ClrType)?)null
        };
        if (expected is null ||
            (jsonToken == expected.Value.Token &&
             string.Equals(
                 entry.ClrValueType,
                 expected.Value.ClrType,
                 StringComparison.Ordinal)))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            source,
            $"Codec '{entry.CodecKey}' for primitive '{entry.FhirTypeName}' " +
            $"requires JSON token '{FormatJsonToken(expected.Value.Token)}' and " +
            $"CLR type '{expected.Value.ClrType}'.",
            entry));
    }

    private static void ValidateValidatorContract(
        PrimitiveGenerationPolicyEntryDocument entry,
        PrimitiveValidatorKey? validatorKey,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (validatorKey is null ||
            !ValidatorClrValueTypes.TryGetValue(
                validatorKey.Value,
                out var expectedClrType) ||
            string.Equals(
                entry.ClrValueType,
                expectedClrType,
                StringComparison.Ordinal))
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            source,
            $"Validator '{entry.ValidatorKey}' for primitive '{entry.FhirTypeName}' " +
            $"requires CLR type '{expectedClrType}'.",
            entry));
    }

    private static void ValidateToStringContract(
        PrimitiveGenerationPolicyEntryDocument entry,
        PrimitiveToStringBehavior? behavior,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var valid = behavior switch
        {
            null => true,
            PrimitiveToStringBehavior.Inherited => true,
            PrimitiveToStringBehavior.BooleanLowercase =>
                string.Equals(entry.ClrValueType, "bool?", StringComparison.Ordinal),
            PrimitiveToStringBehavior.InvariantValue =>
                entry.ClrValueType is "int?" or "long?" or "decimal?",
            PrimitiveToStringBehavior.LiteralOrInvariantValue =>
                entry.PreserveLiteral == true,
            _ => false
        };
        if (valid)
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            source,
            $"ToString behavior '{entry.ToStringBehavior}' is not compatible with " +
            $"primitive '{entry.FhirTypeName}' CLR/literal shape.",
            entry));
    }

    private static string FormatJsonToken(PrimitiveJsonToken token)
    {
        return token.ToString().ToLowerInvariant();
    }

    private static PrimitiveSupportStatus? ParseSupportStatus(
        PrimitiveGenerationPolicyEntryDocument entry,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        if (string.Equals(entry.SupportStatus, "supported", StringComparison.Ordinal))
        {
            return PrimitiveSupportStatus.Supported;
        }

        if (string.Equals(entry.SupportStatus, "unsupported", StringComparison.Ordinal))
        {
            return PrimitiveSupportStatus.Unsupported;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            source,
            $"Primitive '{entry.FhirTypeName}' has unknown support status " +
            $"'{entry.SupportStatus}'.",
            entry));
        return null;
    }

    private static TEnum? ParseClosedKey<TEnum>(
        string? value,
        string fieldName,
        IReadOnlyDictionary<string, TEnum> values,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics,
        PrimitiveGenerationPolicyEntryDocument entry,
        string diagnosticCode)
        where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (values.TryGetValue(value, out var parsed))
        {
            return parsed;
        }

        diagnostics.Add(CreateDiagnostic(
            diagnosticCode,
            source,
            $"Primitive '{entry.FhirTypeName}' has unknown {fieldName} '{value}'.",
            entry));
        return null;
    }

    private static void ValidateUniqueness(
        IReadOnlyList<PrimitiveGenerationPolicyEntryDocument?> entries,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        AddDuplicateDiagnostics(
            entries,
            entry => entry?.FhirTypeName,
            "FHIR type name",
            StringComparer.Ordinal,
            source,
            diagnostics);
        AddDuplicateDiagnostics(
            entries,
            entry => entry?.Canonical,
            "canonical",
            StringComparer.Ordinal,
            source,
            diagnostics);
        AddDuplicateDiagnostics(
            entries.Where(entry => string.Equals(
                entry?.SupportStatus,
                "supported",
                StringComparison.Ordinal)).ToArray(),
            entry => entry?.WrapperName,
            "wrapper name",
            StringComparer.Ordinal,
            source,
            diagnostics);
        AddDuplicateDiagnostics(
            entries.Where(entry => string.Equals(
                entry?.SupportStatus,
                "supported",
                StringComparison.Ordinal)).ToArray(),
            entry => string.IsNullOrWhiteSpace(entry?.WrapperName)
                ? null
                : $"{entry.WrapperName}.g.cs",
            "output file name",
            StringComparer.OrdinalIgnoreCase,
            source,
            diagnostics);
    }

    private static void AddDuplicateDiagnostics(
        IReadOnlyList<PrimitiveGenerationPolicyEntryDocument?> entries,
        Func<PrimitiveGenerationPolicyEntryDocument?, string?> selector,
        string label,
        StringComparer comparer,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics)
    {
        var duplicate = entries
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, comparer)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (duplicate is null)
        {
            return;
        }

        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry,
            source,
            $"Duplicate primitive policy {label} '{duplicate.Key}'."));
    }

    private static void RequireSemanticVersion(
        string? value,
        string fieldName,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics,
        PrimitiveGenerationPolicyEntryDocument? entry = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddMissingField(fieldName, source, diagnostics, entry);
        }
        else if (!SemanticVersionPattern.IsMatch(value))
        {
            diagnostics.Add(CreateDiagnostic(
                GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
                source,
                $"Policy field '{fieldName}' value '{value}' is not a semantic version.",
                entry));
        }
    }

    private static void RequireText(
        string? value,
        string fieldName,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics,
        PrimitiveGenerationPolicyEntryDocument? entry = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddMissingField(fieldName, source, diagnostics, entry);
        }
    }

    private static void AddMissingField(
        string fieldName,
        string source,
        ICollection<GeneratorDiagnostic> diagnostics,
        PrimitiveGenerationPolicyEntryDocument? entry)
    {
        var scope = entry is null
            ? "policy"
            : $"primitive '{entry.FhirTypeName ?? "<unknown>"}'";
        diagnostics.Add(CreateDiagnostic(
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            source,
            $"Required {scope} field '{fieldName}' is missing.",
            entry));
    }

    private static bool IsValidNamespace(string value)
    {
        return value
            .Split('.', StringSplitOptions.None)
            .All(segment => SyntaxFacts.IsValidIdentifier(segment));
    }

    private static void AddIfPresent(
        ICollection<string> fields,
        string fieldName,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(fieldName);
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static GeneratorDiagnostic CreateDiagnostic(
        string code,
        string source,
        string message,
        PrimitiveGenerationPolicyEntryDocument? entry = null)
    {
        return new GeneratorDiagnostic(
            code,
            GeneratorDiagnosticSeverity.Error,
            message,
            source,
            entry?.Canonical,
            entry?.FhirVersion);
    }
}
