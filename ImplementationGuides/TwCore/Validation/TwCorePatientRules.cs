using MyFhirSdk.Core;
using MyFhirSdk.Resources;
using MyFhirSdk.Validation;
using MyFhirSdk.Validation.Profiles;

namespace MyFhirSdk.ImplementationGuides.TwCore.Validation;

internal static class TwCorePatientRules
{
    public static IReadOnlyList<IProfileValidationRule> Create()
    {
        return
        [
            new PatientIdentifierRequiredRule(),
            new PatientIdentifierSystemRequiredRule(),
            new PatientIdentifierValueRequiredRule()
        ];
    }

    private static void AddIssue(
        ProfileValidationContext context,
        ICollection<ValidationIssue> issues,
        string path,
        ValidationIssueCode code,
        string message)
    {
        issues.Add(new ValidationIssue
        {
            Path = path,
            Code = code,
            Severity = ValidationSeverity.Error,
            Message = message,
            Source = ValidationRuleSource.ImplementationGuide,
            PackageId = context.PackageId,
            ProfileUrl = context.ProfileUrl,
            RuleId = context.RuleId
        });
    }

    private static bool IsPresent(PrimitiveType<string>? value)
    {
        return value is not null
            && (value.HasValue || value.Extension.Count > 0);
    }

    private static bool HasIdentifier(Patient patient)
    {
        return patient.Identifier is not null
            && patient.Identifier.Any(identifier => identifier is not null);
    }

    private sealed class PatientIdentifierRequiredRule : IProfileValidationRule
    {
        public string RuleId => "TWCORE-PAT-002";

        public void Validate(
            ProfileValidationContext context,
            ICollection<ValidationIssue> issues)
        {
            if (context.Resource is not Patient patient)
            {
                return;
            }

            if (HasIdentifier(patient))
            {
                return;
            }

            AddIssue(
                context,
                issues,
                "Patient.identifier",
                ValidationIssueCode.Cardinality,
                "Patient.identifier must contain at least one item for TW Core Patient.");
        }
    }

    private sealed class PatientIdentifierSystemRequiredRule : IProfileValidationRule
    {
        public string RuleId => "TWCORE-PAT-003";

        public void Validate(
            ProfileValidationContext context,
            ICollection<ValidationIssue> issues)
        {
            if (context.Resource is not Patient patient || patient.Identifier is null)
            {
                return;
            }

            for (var index = 0; index < patient.Identifier.Count; index++)
            {
                var identifier = patient.Identifier[index];
                if (identifier is null || IsPresent(identifier.System))
                {
                    continue;
                }

                var path = "Patient.identifier[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].system";
                AddIssue(
                    context,
                    issues,
                    path,
                    ValidationIssueCode.Required,
                    path + " is required for TW Core Patient.");
            }
        }
    }

    private sealed class PatientIdentifierValueRequiredRule : IProfileValidationRule
    {
        public string RuleId => "TWCORE-PAT-004";

        public void Validate(
            ProfileValidationContext context,
            ICollection<ValidationIssue> issues)
        {
            if (context.Resource is not Patient patient || patient.Identifier is null)
            {
                return;
            }

            for (var index = 0; index < patient.Identifier.Count; index++)
            {
                var identifier = patient.Identifier[index];
                if (identifier is null || IsPresent(identifier.Value))
                {
                    continue;
                }

                var path = "Patient.identifier[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "].value";
                AddIssue(
                    context,
                    issues,
                    path,
                    ValidationIssueCode.Required,
                    path + " is required for TW Core Patient.");
            }
        }
    }
}
