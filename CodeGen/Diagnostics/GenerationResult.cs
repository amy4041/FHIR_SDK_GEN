namespace MyFhirSdk.CodeGen.Diagnostics;

public sealed class GenerationResult<T>
{
    public GenerationResult(
        T value,
        IReadOnlyList<GeneratorDiagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    public T Value { get; }

    public IReadOnlyList<GeneratorDiagnostic> Diagnostics { get; }

    public bool IsSuccess =>
        Diagnostics.All(diagnostic =>
            diagnostic.Severity != GeneratorDiagnosticSeverity.Error);
}
