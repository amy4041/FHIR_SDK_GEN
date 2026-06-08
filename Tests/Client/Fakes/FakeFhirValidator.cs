namespace MyFhirSdk.Tests.Client.Fakes;

public sealed class FakeFhirValidator : IFhirValidator
{
    public int ValidateCallCount { get; private set; }

    public Resource? LastResource { get; private set; }

    public ValidationResult Result { get; set; } = ValidationResult.Success;

    public Exception? ExceptionToThrow { get; set; }

    public ValidationResult Validate(Resource resource)
    {
        ValidateCallCount++;
        LastResource = resource;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Result;
    }
}
