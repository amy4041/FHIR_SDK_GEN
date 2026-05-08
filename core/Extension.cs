namespace MyFhirSdk.Core;

/// <summary>
/// Additional content defined by implementations.
/// </summary>
public sealed class Extension : DataType
{
    /// <summary>
    /// Source of the definition for the extension code.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Value of extension. Nested extensions are represented by the inherited Extension collection.
    /// MVP可以，但是Special types是Element
    /// </summary>
    public DataType? Value { get; set; }  
}
