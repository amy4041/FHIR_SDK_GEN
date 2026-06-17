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
            [typeof(BundleEntryRequest)] =
            [
                RequiredFieldRule<BundleEntryRequest>.For("method", request => request.Method),
                RequiredFieldRule<BundleEntryRequest>.For("url", request => request.Url)
            ],
            [typeof(BundleEntryResponse)] =
            [
                RequiredFieldRule<BundleEntryResponse>.For("status", response => response.Status)
            ],
            [typeof(Claim)] =
            [
                RequiredFieldRule<Claim>.For("status", claim => claim.Status),
                RequiredFieldRule<Claim>.For("type", claim => claim.Type),
                RequiredFieldRule<Claim>.For("use", claim => claim.Use),
                RequiredFieldRule<Claim>.For("patient", claim => claim.Patient),
                RequiredFieldRule<Claim>.For("created", claim => claim.Created)
            ],
            [typeof(ClaimPayee)] =
            [
                RequiredFieldRule<ClaimPayee>.For("type", payee => payee.Type)
            ],
            [typeof(ClaimEvent)] =
            [
                RequiredFieldRule<ClaimEvent>.For("type", claimEvent => claimEvent.Type),
                ChoiceElementRule<ClaimEvent>.ExactlyOne(
                    "when[x]",
                    claimEvent => claimEvent.WhenDateTime,
                    claimEvent => claimEvent.WhenPeriod)
            ],
            [typeof(ClaimCareTeam)] =
            [
                RequiredFieldRule<ClaimCareTeam>.For("sequence", careTeam => careTeam.Sequence),
                RequiredFieldRule<ClaimCareTeam>.For("provider", careTeam => careTeam.Provider)
            ],
            [typeof(ClaimSupportingInfo)] =
            [
                RequiredFieldRule<ClaimSupportingInfo>.For("sequence", supportingInfo => supportingInfo.Sequence),
                RequiredFieldRule<ClaimSupportingInfo>.For("category", supportingInfo => supportingInfo.Category),
                ChoiceElementRule<ClaimSupportingInfo>.AtMostOne(
                    "timing[x]",
                    supportingInfo => supportingInfo.TimingDate,
                    supportingInfo => supportingInfo.TimingPeriod),
                ChoiceElementRule<ClaimSupportingInfo>.AtMostOne(
                    "value[x]",
                    supportingInfo => supportingInfo.ValueBoolean,
                    supportingInfo => supportingInfo.ValueString,
                    supportingInfo => supportingInfo.ValueQuantity,
                    supportingInfo => supportingInfo.ValueAttachment,
                    supportingInfo => supportingInfo.ValueReference,
                    supportingInfo => supportingInfo.ValueIdentifier)
            ],
            [typeof(ClaimDiagnosis)] =
            [
                RequiredFieldRule<ClaimDiagnosis>.For("sequence", diagnosis => diagnosis.Sequence),
                ChoiceElementRule<ClaimDiagnosis>.ExactlyOne(
                    "diagnosis[x]",
                    diagnosis => diagnosis.DiagnosisCodeableConcept,
                    diagnosis => diagnosis.DiagnosisReference)
            ],
            [typeof(ClaimProcedure)] =
            [
                RequiredFieldRule<ClaimProcedure>.For("sequence", procedure => procedure.Sequence),
                ChoiceElementRule<ClaimProcedure>.ExactlyOne(
                    "procedure[x]",
                    procedure => procedure.ProcedureCodeableConcept,
                    procedure => procedure.ProcedureReference)
            ],
            [typeof(ClaimInsurance)] =
            [
                RequiredFieldRule<ClaimInsurance>.For("sequence", insurance => insurance.Sequence),
                RequiredFieldRule<ClaimInsurance>.For("focal", insurance => insurance.Focal),
                RequiredFieldRule<ClaimInsurance>.For("coverage", insurance => insurance.Coverage)
            ],
            [typeof(ClaimAccident)] =
            [
                RequiredFieldRule<ClaimAccident>.For("date", accident => accident.Date),
                ChoiceElementRule<ClaimAccident>.AtMostOne(
                    "location[x]",
                    accident => accident.LocationAddress,
                    accident => accident.LocationReference)
            ],
            [typeof(ClaimItem)] =
            [
                RequiredFieldRule<ClaimItem>.For("sequence", item => item.Sequence),
                ChoiceElementRule<ClaimItem>.AtMostOne(
                    "serviced[x]",
                    item => item.ServicedDate,
                    item => item.ServicedPeriod),
                ChoiceElementRule<ClaimItem>.AtMostOne(
                    "location[x]",
                    item => item.LocationCodeableConcept,
                    item => item.LocationAddress,
                    item => item.LocationReference)
            ],
            [typeof(ClaimDetail)] =
            [
                RequiredFieldRule<ClaimDetail>.For("sequence", detail => detail.Sequence)
            ],
            [typeof(ClaimSubDetail)] =
            [
                RequiredFieldRule<ClaimSubDetail>.For("sequence", subDetail => subDetail.Sequence)
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
            [typeof(ClaimBodySite)] =
            [
                RequiredFieldRule<ClaimBodySite>.ForList("site", bodySite => bodySite.Site)
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
