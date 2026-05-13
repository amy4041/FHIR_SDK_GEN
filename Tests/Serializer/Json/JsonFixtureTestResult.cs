internal sealed record JsonFixtureTestResult(bool Passed, string Message)
{
    public static JsonFixtureTestResult Pass()
    {
        return new JsonFixtureTestResult(true, string.Empty);
    }

    public static JsonFixtureTestResult Fail(string message)
    {
        return new JsonFixtureTestResult(false, message);
    }
}
