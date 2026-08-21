using MyFhirSdk.CodeGen.Diagnostics;
using MyFhirSdk.CodeGen.Policy;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests.Policy;

public sealed class PrimitiveGenerationPolicyValidatorTests
{
    private const string SourceFile = "primitive-generation-policy.json";

    private readonly PrimitiveGenerationPolicyValidator _validator = new();

    [Fact]
    public async Task Validate_RepositoryPolicyProducesOrderedImmutableModel()
    {
        var document = await LoadRepositoryPolicyAsync();

        var result = _validator.Validate(document, SourceFile);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var policy = Assert.IsType<ValidatedPrimitiveGenerationPolicy>(result.Value);
        Assert.Equal(1, policy.SchemaVersion);
        Assert.Equal("1.0.0", policy.PolicyVersion);
        Assert.Equal("5.0.0", policy.FhirVersion);
        Assert.Equal("phase-a-v1", policy.RuntimeContractVersion);
        Assert.Equal("MyFhirSdk.Primitives", policy.PrimitiveNamespace);
        Assert.Equal(21, policy.Primitives.Count);
        Assert.Equal(
            policy.Primitives
                .Select(entry => entry.FhirTypeName)
                .Order(StringComparer.Ordinal),
            policy.Primitives.Select(entry => entry.FhirTypeName));
        Assert.Equal(17, policy.Primitives.Count(entry => entry.IsSupported));
        Assert.Equal(
            ["oid", "time", "uuid", "xhtml"],
            policy.Primitives
                .Where(entry => !entry.IsSupported)
                .Select(entry => entry.FhirTypeName));
        Assert.Throws<NotSupportedException>(
            () => ((IList<ValidatedPrimitivePolicyEntry>)policy.Primitives).Clear());
        var decimalEntry = Assert.Single(
            policy.Primitives,
            entry => entry.FhirTypeName == "decimal");
        Assert.Throws<NotSupportedException>(
            () => ((IList<PrimitivePublicConstant>)decimalEntry.PublicConstants).Clear());
    }

    [Fact]
    public async Task Validate_RepositoryPolicyMatchesPhaseBHandoffMatrix()
    {
        var document = await LoadRepositoryPolicyAsync();
        var result = _validator.Validate(document, SourceFile);
        var policy = Assert.IsType<ValidatedPrimitiveGenerationPolicy>(result.Value);
        var supported = policy.Primitives
            .Where(entry => entry.IsSupported)
            .ToDictionary(entry => entry.FhirTypeName, StringComparer.Ordinal);

        var expected = CreateExpectedHandoffMatrix();
        Assert.Equal(expected.Count, supported.Count);
        foreach (var (fhirTypeName, contract) in expected)
        {
            Assert.True(
                supported.TryGetValue(fhirTypeName, out var entry),
                $"Missing supported policy for '{fhirTypeName}'.");
            Assert.Equal(contract.WrapperName, entry.WrapperName);
            Assert.Equal(contract.ClrValueType, entry.ClrValueType);
            Assert.Equal(contract.JsonToken, entry.JsonToken);
            Assert.Equal(contract.CodecKey, entry.CodecKey);
            Assert.Equal(contract.ValidatorKey, entry.ValidatorKey);
            Assert.Equal(contract.PreserveLiteral, entry.PreserveLiteral);
            Assert.Equal(contract.PreserveLiteral, entry.LiteralConstructor);
            Assert.Equal(
                contract.PreserveLiteral ? "Literal" : null,
                entry.LiteralPropertyName);
            Assert.Equal(contract.ToStringBehavior, entry.ToStringBehavior);
        }
    }

    [Fact]
    public async Task Validate_RepositoryPolicyPreservesApprovedCompatibilityConstants()
    {
        var document = await LoadRepositoryPolicyAsync();
        var result = _validator.Validate(document, SourceFile);
        var policy = Assert.IsType<ValidatedPrimitiveGenerationPolicy>(result.Value);

        var constants = policy.Primitives
            .SelectMany(entry => entry.PublicConstants.Select(constant =>
                $"{entry.WrapperName}.{constant.Name}:{constant.ClrType}={constant.Value}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "FhirDecimal.MaxExponentDigits:Int32=9",
                "FhirDecimal.MaxFractionDigits:Int32=17",
                "FhirDecimal.MaxIntegerDigits:Int32=18",
                "FhirMarkdown.MaxLength:Int32=1048576",
                "FhirString.MaxLength:Int32=1048576"
            ],
            constants);
    }

    [Fact]
    public async Task Validate_RepositoryPolicyUnsupportedEntriesHaveReasonsAndNoGenerationShape()
    {
        var document = await LoadRepositoryPolicyAsync();
        var result = _validator.Validate(document, SourceFile);
        var policy = Assert.IsType<ValidatedPrimitiveGenerationPolicy>(result.Value);

        Assert.All(
            policy.Primitives.Where(entry => !entry.IsSupported),
            entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.UnsupportedReason));
                Assert.Null(entry.WrapperName);
                Assert.Null(entry.ClrValueType);
                Assert.Null(entry.JsonToken);
                Assert.Null(entry.CodecKey);
                Assert.Null(entry.ValidatorKey);
                Assert.False(entry.PreserveLiteral);
                Assert.False(entry.LiteralConstructor);
                Assert.Null(entry.LiteralPropertyName);
                Assert.Null(entry.ToStringBehavior);
                Assert.Empty(entry.PublicConstants);
            });
    }

    [Fact]
    public async Task Validate_ShuffledInputProducesSameOrdinalModel()
    {
        var document = await LoadRepositoryPolicyAsync();
        var shuffled = CopyPolicy(
            document,
            document.Primitives!.Reverse().ToArray());

        var originalResult = _validator.Validate(document, SourceFile);
        var shuffledResult = _validator.Validate(shuffled, SourceFile);

        Assert.True(shuffledResult.IsSuccess);
        Assert.Equal(
            Assert.IsType<ValidatedPrimitiveGenerationPolicy>(originalResult.Value)
                .Primitives.Select(Describe),
            Assert.IsType<ValidatedPrimitiveGenerationPolicy>(shuffledResult.Value)
                .Primitives.Select(Describe));
    }

    [Fact]
    public void Validate_WithUnsupportedSchemaReturnsFsg0014()
    {
        var policy = CreatePolicy(schemaVersion: 2);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.UnsupportedPrimitivePolicySchema,
            "schema version");
    }

    [Fact]
    public void Validate_WithInvalidTopLevelFieldsReturnsPreciseDiagnostics()
    {
        var policy = new PrimitiveGenerationPolicyDocument
        {
            SchemaVersion = null,
            PolicyVersion = "v1",
            FhirVersion = "R5",
            RuntimeContractVersion = " ",
            PrimitiveNamespace = "MyFhirSdk..Primitives",
            Primitives = []
        };

        var result = _validator.Validate(policy, SourceFile);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(
            "'schemaVersion'",
            StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(
            "'policyVersion'",
            StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(
            "'fhirVersion'",
            StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(
            "'runtimeContractVersion'",
            StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(
            "C# namespace",
            StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(
            "at least one entry",
            StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithCanonicalMismatchReturnsFsg0015()
    {
        var entry = new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = "sample",
            Canonical = "http://example.test/StructureDefinition/sample",
            FhirVersion = "5.0.0",
            WrapperName = "FhirSample",
            ClrValueType = "string",
            JsonToken = "string",
            CodecKey = "string",
            ValidatorKey = "string",
            PreserveLiteral = false,
            LiteralConstructor = false,
            SupportStatus = "supported",
            ToStringBehavior = "inherited",
            PublicConstants = []
        };

        var result = _validator.Validate(
            CreatePolicy(primitives: [entry]),
            SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "does not match");
    }

    [Fact]
    public void Validate_WithUnknownSupportStatusReturnsFsg0015()
    {
        var entry = new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = "sample",
            Canonical = "http://hl7.org/fhir/StructureDefinition/sample",
            FhirVersion = "5.0.0",
            SupportStatus = "experimental"
        };

        var result = _validator.Validate(
            CreatePolicy(primitives: [entry]),
            SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "unknown support status");
    }

    [Fact]
    public void Validate_WithDuplicateIdentityAndWrapperReturnsFsg0016()
    {
        var first = CreateSupportedEntry();
        var duplicate = CreateSupportedEntry();
        var policy = CreatePolicy(primitives: [first, duplicate]);

        var result = _validator.Validate(policy, SourceFile);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            item =>
                item.Code == GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry &&
                item.Message.Contains("FHIR type name", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            item =>
                item.Code == GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry &&
                item.Message.Contains("canonical", StringComparison.Ordinal));
        Assert.Contains(
            result.Diagnostics,
            item =>
                item.Code == GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry &&
                item.Message.Contains("wrapper name", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithCaseOnlyOutputFileCollisionReturnsFsg0016()
    {
        var first = CreateSupportedEntry(
            fhirTypeName: "alpha",
            wrapperName: "FhirSample");
        var second = CreateSupportedEntry(
            fhirTypeName: "beta",
            wrapperName: "fhirsample");
        var policy = CreatePolicy(primitives: [first, second]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry,
            "output file name");
    }

    [Fact]
    public void Validate_OrdinalIdentityComparisonTreatsDifferentCaseAsDistinct()
    {
        var lower = CreateSupportedEntry(
            fhirTypeName: "sample",
            wrapperName: "FhirLower");
        var upper = CreateSupportedEntry(
            fhirTypeName: "Sample",
            wrapperName: "FhirUpper");
        var policy = CreatePolicy(primitives: [lower, upper]);

        var result = _validator.Validate(policy, SourceFile);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["Sample", "sample"],
            Assert.IsType<ValidatedPrimitiveGenerationPolicy>(result.Value)
                .Primitives.Select(entry => entry.FhirTypeName));
    }

    [Fact]
    public void Validate_WithMissingSupportedShapeReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(
            wrapperName: null,
            clrValueType: null,
            includePublicConstants: false);
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "'wrapperName'");
        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "'clrValueType'");
        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "'publicConstants'");
    }

    [Fact]
    public void Validate_WithMissingLiteralFlagsReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(
            preserveLiteral: null,
            literalConstructor: null);
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "'preserveLiteral'");
        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "'literalConstructor'");
    }

    [Fact]
    public void Validate_WithInvalidWrapperIdentifierReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(wrapperName: "Fhir-Invalid");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "valid C# identifier");
    }

    [Fact]
    public void Validate_WithUnknownJsonTokenReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(jsonToken: "object");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "unknown jsonToken");
    }

    [Fact]
    public void Validate_WithUnknownClrValueTypeReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(clrValueType: "DateOnly?");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "CLR value type 'DateOnly?' is not supported");
    }

    [Fact]
    public void Validate_WithUnknownClosedKeysReturnsFsg0017()
    {
        var entry = CreateSupportedEntry(
            codecKey: "custom-codec",
            validatorKey: "custom-validator",
            toStringBehavior: "custom-behavior");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            3,
            result.Diagnostics.Count(item =>
                item.Code == GeneratorDiagnosticCodes.UnknownPrimitivePolicyKey));
    }

    [Fact]
    public void Validate_WithIncompatibleStandardCodecReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(
            clrValueType: "string",
            jsonToken: "string",
            codecKey: "boolean",
            validatorKey: "string");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "requires JSON token 'boolean' and CLR type 'bool?'");
    }

    [Fact]
    public void Validate_WithIncompatibleValidatorBackingTypeReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(validatorKey: "boolean");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "requires CLR type 'bool?'");
    }

    [Fact]
    public void Validate_WithIncompatibleToStringBehaviorReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(
            toStringBehavior: "boolean-lowercase");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "not compatible");
    }

    [Fact]
    public void Validate_WithInvalidDecimalLiteralContractReturnsFsg0018()
    {
        var entry = CreateSupportedEntry(
            clrValueType: "long?",
            jsonToken: "string",
            codecKey: "decimal-literal",
            validatorKey: "decimal",
            preserveLiteral: true,
            literalConstructor: true,
            literalPropertyName: "Literal",
            toStringBehavior: "literal-or-invariant-value");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
            "decimal?");
    }

    [Fact]
    public void Validate_WithLiteralCodecMissingLiteralShapeReturnsFsg0018()
    {
        var entry = CreateSupportedEntry(
            clrValueType: "decimal?",
            jsonToken: "number",
            codecKey: "decimal-literal",
            validatorKey: "decimal",
            preserveLiteral: false,
            literalConstructor: false,
            literalPropertyName: null,
            toStringBehavior: "invariant-value");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
            "requires literal preservation");
    }

    [Fact]
    public void Validate_WithInvalidInteger64LiteralContractReturnsFsg0018()
    {
        var entry = CreateSupportedEntry(
            clrValueType: "long?",
            jsonToken: "number",
            codecKey: "integer64-literal",
            validatorKey: "integer64",
            preserveLiteral: true,
            literalConstructor: true,
            literalPropertyName: "Literal",
            toStringBehavior: "literal-or-invariant-value");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
            "JSON string");
    }

    [Fact]
    public void Validate_WithLiteralShapeOnStandardCodecReturnsFsg0018()
    {
        var entry = CreateSupportedEntry(
            preserveLiteral: true,
            literalConstructor: true,
            literalPropertyName: "Literal");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitiveLiteralPolicy,
            "Non-literal codec");
    }

    [Fact]
    public void Validate_WithUnsupportedEntryWithoutReasonReturnsFsg0015()
    {
        var entry = CreateUnsupportedEntry(unsupportedReason: null);
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "'unsupportedReason'");
    }

    [Fact]
    public void Validate_WithUnsupportedGenerationShapeReturnsFsg0015()
    {
        var entry = new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = "sample",
            Canonical = "http://hl7.org/fhir/StructureDefinition/sample",
            FhirVersion = "5.0.0",
            SupportStatus = "unsupported",
            UnsupportedReason = "No Runtime contract.",
            WrapperName = "FhirSample",
            PreserveLiteral = false
        };
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "generation fields");
    }

    [Fact]
    public void Validate_WithInvalidConstantsReturnsPreciseDiagnostics()
    {
        var entry = CreateSupportedEntry(
            publicConstants:
            [
                new PrimitivePublicConstantDocument
                {
                    Name = "MaxLength",
                    ClrType = "int",
                    Value = (long)int.MaxValue + 1
                },
                new PrimitivePublicConstantDocument
                {
                    Name = "MaxLength",
                    ClrType = "int",
                    Value = 1
                }
            ]);
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "outside Int32 range");
        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.DuplicatePrimitivePolicyEntry,
            "duplicate public constant");
    }

    [Fact]
    public void Validate_WithUnsupportedConstantClrTypeReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(
            publicConstants:
            [
                new PrimitivePublicConstantDocument
                {
                    Name = "MaxLength",
                    ClrType = "decimal",
                    Value = 1
                }
            ]);
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "unsupported CLR type 'decimal'");
    }

    [Fact]
    public void Validate_WithEntryFhirVersionMismatchReturnsFsg0015()
    {
        var entry = CreateSupportedEntry(fhirVersion: "5.0.1");
        var policy = CreatePolicy(primitives: [entry]);

        var result = _validator.Validate(policy, SourceFile);

        AssertDiagnostic(
            result,
            GeneratorDiagnosticCodes.InvalidPrimitivePolicy,
            "does not match policy version");
    }

    [Fact]
    public void Validate_DiagnosticsAreDeterministicallySorted()
    {
        var policy = CreatePolicy(
            primitives:
            [
                CreateSupportedEntry(
                    fhirTypeName: "zeta",
                    codecKey: "unknown-zeta"),
                CreateSupportedEntry(
                    fhirTypeName: "alpha",
                    codecKey: "unknown-alpha")
            ]);

        var result = _validator.Validate(policy, SourceFile);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            result.Diagnostics
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.SourceFile, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionCanonical, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal),
            result.Diagnostics);
    }

    private static PrimitiveGenerationPolicyDocument CreatePolicy(
        int? schemaVersion = 1,
        IReadOnlyList<PrimitiveGenerationPolicyEntryDocument?>? primitives = null)
    {
        return new PrimitiveGenerationPolicyDocument
        {
            SchemaVersion = schemaVersion,
            PolicyVersion = "1.0.0",
            FhirVersion = "5.0.0",
            RuntimeContractVersion = "phase-a-v1",
            PrimitiveNamespace = "MyFhirSdk.Primitives",
            Primitives = primitives ?? [CreateSupportedEntry()]
        };
    }

    private static PrimitiveGenerationPolicyEntryDocument CreateSupportedEntry(
        string fhirTypeName = "sample",
        string fhirVersion = "5.0.0",
        string? wrapperName = "FhirSample",
        string? clrValueType = "string",
        string jsonToken = "string",
        string codecKey = "string",
        string validatorKey = "string",
        bool? preserveLiteral = false,
        bool? literalConstructor = false,
        string? literalPropertyName = null,
        string toStringBehavior = "inherited",
        IReadOnlyList<PrimitivePublicConstantDocument?>? publicConstants = null,
        bool includePublicConstants = true)
    {
        return new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = fhirTypeName,
            Canonical = $"http://hl7.org/fhir/StructureDefinition/{fhirTypeName}",
            FhirVersion = fhirVersion,
            WrapperName = wrapperName,
            ClrValueType = clrValueType,
            JsonToken = jsonToken,
            CodecKey = codecKey,
            ValidatorKey = validatorKey,
            PreserveLiteral = preserveLiteral,
            LiteralConstructor = literalConstructor,
            LiteralPropertyName = literalPropertyName,
            SupportStatus = "supported",
            UnsupportedReason = null,
            ToStringBehavior = toStringBehavior,
            PublicConstants = includePublicConstants
                ? publicConstants ?? []
                : null
        };
    }

    private static PrimitiveGenerationPolicyEntryDocument CreateUnsupportedEntry(
        string fhirTypeName = "sample",
        string? unsupportedReason = "No Runtime contract.")
    {
        return new PrimitiveGenerationPolicyEntryDocument
        {
            FhirTypeName = fhirTypeName,
            Canonical = $"http://hl7.org/fhir/StructureDefinition/{fhirTypeName}",
            FhirVersion = "5.0.0",
            SupportStatus = "unsupported",
            UnsupportedReason = unsupportedReason
        };
    }

    private static PrimitiveGenerationPolicyDocument CopyPolicy(
        PrimitiveGenerationPolicyDocument source,
        IReadOnlyList<PrimitiveGenerationPolicyEntryDocument?> primitives)
    {
        return new PrimitiveGenerationPolicyDocument
        {
            SchemaVersion = source.SchemaVersion,
            PolicyVersion = source.PolicyVersion,
            FhirVersion = source.FhirVersion,
            RuntimeContractVersion = source.RuntimeContractVersion,
            PrimitiveNamespace = source.PrimitiveNamespace,
            Primitives = primitives
        };
    }

    private static string Describe(ValidatedPrimitivePolicyEntry entry)
    {
        return string.Join(
            '|',
            entry.FhirTypeName,
            entry.Canonical,
            entry.SupportStatus,
            entry.WrapperName,
            entry.ClrValueType,
            entry.JsonToken,
            entry.CodecKey,
            entry.ValidatorKey,
            entry.PreserveLiteral,
            entry.LiteralConstructor,
            entry.LiteralPropertyName,
            entry.ToStringBehavior,
            entry.UnsupportedReason,
            string.Join(',', entry.PublicConstants));
    }

    private static void AssertDiagnostic(
        GenerationResult<ValidatedPrimitiveGenerationPolicy?> result,
        string code,
        string messageFragment)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(
            result.Diagnostics,
            item =>
                item.Code == code &&
                item.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    private static async Task<PrimitiveGenerationPolicyDocument>
        LoadRepositoryPolicyAsync()
    {
        var loader = new PrimitiveGenerationPolicyLoader();
        var result = await loader.LoadAsync(Path.Combine(
            AppContext.BaseDirectory,
            "Policy",
            "primitive-generation-policy.json"));

        Assert.True(result.IsSuccess);
        return Assert.IsType<PrimitiveGenerationPolicyDocument>(result.Value);
    }

    private static IReadOnlyDictionary<string, ExpectedPrimitiveContract>
        CreateExpectedHandoffMatrix()
    {
        return new Dictionary<string, ExpectedPrimitiveContract>(StringComparer.Ordinal)
        {
            ["base64Binary"] = new("FhirBase64Binary", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Base64Binary, false, PrimitiveToStringBehavior.Inherited),
            ["boolean"] = new("FhirBoolean", "bool?", PrimitiveJsonToken.Boolean, PrimitiveCodecKey.Boolean, PrimitiveValidatorKey.Boolean, false, PrimitiveToStringBehavior.BooleanLowercase),
            ["canonical"] = new("FhirCanonical", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Canonical, false, PrimitiveToStringBehavior.Inherited),
            ["code"] = new("FhirCode", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Code, false, PrimitiveToStringBehavior.Inherited),
            ["date"] = new("FhirDate", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Date, false, PrimitiveToStringBehavior.Inherited),
            ["dateTime"] = new("FhirDateTime", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.DateTime, false, PrimitiveToStringBehavior.Inherited),
            ["decimal"] = new("FhirDecimal", "decimal?", PrimitiveJsonToken.Number, PrimitiveCodecKey.DecimalLiteral, PrimitiveValidatorKey.Decimal, true, PrimitiveToStringBehavior.LiteralOrInvariantValue),
            ["id"] = new("FhirId", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Id, false, PrimitiveToStringBehavior.Inherited),
            ["instant"] = new("FhirInstant", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Instant, false, PrimitiveToStringBehavior.Inherited),
            ["integer"] = new("FhirInteger", "int?", PrimitiveJsonToken.Number, PrimitiveCodecKey.Integer, PrimitiveValidatorKey.Integer, false, PrimitiveToStringBehavior.InvariantValue),
            ["integer64"] = new("FhirInteger64", "long?", PrimitiveJsonToken.String, PrimitiveCodecKey.Integer64Literal, PrimitiveValidatorKey.Integer64, true, PrimitiveToStringBehavior.LiteralOrInvariantValue),
            ["markdown"] = new("FhirMarkdown", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Markdown, false, PrimitiveToStringBehavior.Inherited),
            ["positiveInt"] = new("FhirPositiveInt", "int?", PrimitiveJsonToken.Number, PrimitiveCodecKey.Integer, PrimitiveValidatorKey.PositiveInt, false, PrimitiveToStringBehavior.InvariantValue),
            ["string"] = new("FhirString", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.String, false, PrimitiveToStringBehavior.Inherited),
            ["unsignedInt"] = new("FhirUnsignedInt", "int?", PrimitiveJsonToken.Number, PrimitiveCodecKey.Integer, PrimitiveValidatorKey.UnsignedInt, false, PrimitiveToStringBehavior.InvariantValue),
            ["uri"] = new("FhirUri", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Uri, false, PrimitiveToStringBehavior.Inherited),
            ["url"] = new("FhirUrl", "string", PrimitiveJsonToken.String, PrimitiveCodecKey.String, PrimitiveValidatorKey.Url, false, PrimitiveToStringBehavior.Inherited)
        };
    }

    private sealed record ExpectedPrimitiveContract(
        string WrapperName,
        string ClrValueType,
        PrimitiveJsonToken JsonToken,
        PrimitiveCodecKey CodecKey,
        PrimitiveValidatorKey ValidatorKey,
        bool PreserveLiteral,
        PrimitiveToStringBehavior ToStringBehavior);
}
