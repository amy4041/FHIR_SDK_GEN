namespace MyFhirSdk.Primitives;

internal static class PrimitiveValidators
{
    internal static IPrimitiveValidator Base64Binary { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Base64Binary);
    internal static IPrimitiveValidator Boolean { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Boolean);
    internal static IPrimitiveValidator Canonical { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Canonical);
    internal static IPrimitiveValidator Code { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Code);
    internal static IPrimitiveValidator Date { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Date);
    internal static IPrimitiveValidator DateTime { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.DateTime);
    internal static IPrimitiveValidator Decimal { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Decimal);
    internal static IPrimitiveValidator Id { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Id);
    internal static IPrimitiveValidator Instant { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Instant);
    internal static IPrimitiveValidator Integer { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Integer);
    internal static IPrimitiveValidator Integer64 { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Integer64);
    internal static IPrimitiveValidator Markdown { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Markdown);
    internal static IPrimitiveValidator Oid { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Oid);
    internal static IPrimitiveValidator PositiveInt { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.PositiveInt);
    internal static IPrimitiveValidator String { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.String);
    internal static IPrimitiveValidator Time { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Time);
    internal static IPrimitiveValidator UnsignedInt { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.UnsignedInt);
    internal static IPrimitiveValidator Uri { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Uri);
    internal static IPrimitiveValidator Url { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Url);
    internal static IPrimitiveValidator Uuid { get; } = new ValidatorAdapter(ModelSupport.PrimitiveValidators.Uuid);

    private sealed class ValidatorAdapter(ModelContracts.IPrimitiveValidator inner) : IPrimitiveValidator
    {
        public bool IsValid(object value) => inner.IsValid(value);
        public bool IsValidValue(object? value) => inner.IsValidValue(value);
    }
}
