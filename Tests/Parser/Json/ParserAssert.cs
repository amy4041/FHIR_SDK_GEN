internal static class ParserAssert
{
    public static void Equal<T>(T expected, T actual, string path)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{path}: expected '{expected}', actual '{actual}'.");
        }
    }

    public static void Count<T>(int expected, ICollection<T> actual, string path)
    {
        if (actual.Count != expected)
        {
            throw new InvalidOperationException(
                $"{path}: expected {expected} item(s), actual {actual.Count}.");
        }
    }

    public static T NotNull<T>(T? value, string path)
        where T : class
    {
        if (value is null)
        {
            throw new InvalidOperationException($"{path}: expected a value, actual null.");
        }

        return value;
    }

    public static T IsType<T>(object? value, string path)
        where T : class
    {
        if (value is not T typedValue)
        {
            var actualTypeName = value?.GetType().Name ?? "null";
            throw new InvalidOperationException(
                $"{path}: expected type {typeof(T).Name}, actual {actualTypeName}.");
        }

        return typedValue;
    }
}
