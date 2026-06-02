using MyFhirSdk.Resources;

namespace MyFhirSdk.Validation.Rules;

internal sealed class ResourceRuleRegistry
{
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<IFhirValidationRule>> _rules;

    private ResourceRuleRegistry(IReadOnlyDictionary<Type, IReadOnlyList<IFhirValidationRule>> rules)
    {
        _rules = rules;
    }

    public static ResourceRuleRegistry CreateDefault()
    {
        var rules = new Dictionary<Type, IReadOnlyList<IFhirValidationRule>>
        {
            [typeof(Bundle)] =
            [
                RequiredFieldRule<Bundle>.For("type", bundle => bundle.Type)
            ],
            [typeof(BundleLink)] =
            [
                RequiredFieldRule<BundleLink>.For("relation", link => link.Relation),
                RequiredFieldRule<BundleLink>.For("url", link => link.Url)
            ],
            [typeof(Claim)] =
            [
                RequiredFieldRule<Claim>.For("status", claim => claim.Status),
                RequiredFieldRule<Claim>.For("type", claim => claim.Type),
                RequiredFieldRule<Claim>.For("use", claim => claim.Use),
                RequiredFieldRule<Claim>.For("patient", claim => claim.Patient),
                RequiredFieldRule<Claim>.For("created", claim => claim.Created)
            ],
            [typeof(Coverage)] =
            [
                RequiredFieldRule<Coverage>.For("status", coverage => coverage.Status),
                RequiredFieldRule<Coverage>.For("kind", coverage => coverage.Kind),
                RequiredFieldRule<Coverage>.For("beneficiary", coverage => coverage.Beneficiary)
            ],
            [typeof(CoveragePaymentBy)] =
            [
                RequiredFieldRule<CoveragePaymentBy>.For("party", paymentBy => paymentBy.Party)
            ],
            [typeof(CoverageClass)] =
            [
                RequiredFieldRule<CoverageClass>.For("type", coverageClass => coverageClass.Type),
                RequiredFieldRule<CoverageClass>.For("value", coverageClass => coverageClass.Value)
            ],
            [typeof(Encounter)] =
            [
                RequiredFieldRule<Encounter>.For("status", encounter => encounter.Status)
            ],
            [typeof(EncounterLocation)] =
            [
                RequiredFieldRule<EncounterLocation>.For("location", location => location.Location)
            ],
            [typeof(Patient)] =
            [
                ChoiceElementRule<Patient>.AtMostOne(
                    "deceased[x]",
                    patient => patient.DeceasedBoolean,
                    patient => patient.DeceasedDateTime),
                ChoiceElementRule<Patient>.AtMostOne(
                    "multipleBirth[x]",
                    patient => patient.MultipleBirthBoolean,
                    patient => patient.MultipleBirthInteger)
            ],
            [typeof(Practitioner)] =
            [
                ChoiceElementRule<Practitioner>.AtMostOne(
                    "deceased[x]",
                    practitioner => practitioner.DeceasedBoolean,
                    practitioner => practitioner.DeceasedDateTime)
            ]
        };

        return new ResourceRuleRegistry(rules);
    }

    public IReadOnlyList<IFhirValidationRule> GetRules(Type type)
    {
        return _rules.TryGetValue(type, out var rules)
            ? rules
            : Array.Empty<IFhirValidationRule>();
    }
}
