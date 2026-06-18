internal static class ParserAssert
{
    public static void Equal<T>(T expected, T actual, string path)
    {
        Assert.True(
            EqualityComparer<T>.Default.Equals(expected, actual),
            $"{path}: expected '{expected}', actual '{actual}'.");
    }

    public static void Count<T>(int expected, ICollection<T> actual, string path)
    {
        Assert.True(
            actual.Count == expected,
            $"{path}: expected {expected} item(s), actual {actual.Count}.");
    }

    public static T NotNull<T>(T? value, string path)
        where T : class
    {
        if (value is null)
        {
            Assert.Fail($"{path}: expected a value, actual null.");
        }

        return value;
    }

    public static T IsType<T>(object? value, string path)
        where T : class
    {
        if (value is T typedValue)
        {
            return typedValue;
        }

        var actualTypeName = value?.GetType().Name ?? "null";
        Assert.Fail($"{path}: expected type {typeof(T).Name}, actual {actualTypeName}.");
        return default!;
    }
}
