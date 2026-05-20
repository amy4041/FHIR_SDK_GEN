using MyFhirSdk.Tests.Client.Authentication;
using MyFhirSdk.Tests.Client.Requests;
using MyFhirSdk.Tests.Client.Responses;
using MyFhirSdk.Tests.Client.Search;

namespace MyFhirSdk.Tests.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        var tests = new List<(string Name, Func<Task> Run)>
        {
            Test("Authentication.BearerTokenAuthProvider adds Authorization header", BearerTokenAuthProviderTests.ApplyAsyncAddsAuthorizationHeader),
            Test("Authentication.BearerTokenAuthProvider rejects empty token", BearerTokenAuthProviderTests.ConstructorRejectsEmptyToken),
            Test("Authentication.NoAuthProvider leaves Authorization header empty", NoAuthProviderTests.ApplyAsyncDoesNotMutateAuthorizationHeader),
            Test("Requests.FhirRequestBuilder builds read request", FhirRequestBuilderTests.BuildReadRequestCreatesGetResourceInstanceRequest),
            Test("Requests.FhirRequestBuilder builds create request", FhirRequestBuilderTests.BuildCreateRequestCreatesPostResourceTypeRequest),
            Test("Requests.FhirRequestBuilder builds update request", FhirRequestBuilderTests.BuildUpdateRequestCreatesPutResourceInstanceRequest),
            Test("Requests.FhirRequestBuilder rejects update without id", FhirRequestBuilderTests.BuildUpdateRequestRequiresResourceId),
            Test("Requests.FhirRequestBuilder builds raw search request", FhirRequestBuilderTests.BuildSearchRequestCreatesGetSearchRequestForRawQuery),
            Test("Requests.FhirRequestBuilder builds structured search request", FhirRequestBuilderTests.BuildSearchRequestCreatesGetSearchRequestForStructuredQuery),
            Test("Requests.FhirRequestUriBuilder preserves base path", FhirRequestUriBuilderTests.BuildResourceTypeUriPreservesBasePath),
            Test("Requests.FhirRequestUriBuilder handles trailing slash", FhirRequestUriBuilderTests.BuildResourceTypeUriHandlesTrailingSlash),
            Test("Requests.FhirRequestUriBuilder encodes resource id", FhirRequestUriBuilderTests.BuildResourceInstanceUriEncodesResourceId),
            Test("Requests.FhirRequestUriBuilder trims leading search question mark", FhirRequestUriBuilderTests.BuildSearchUriTrimsLeadingQuestionMark),
            Test("Requests.FhirResourceTypeResolver resolves generic resource type", FhirResourceTypeResolverTests.GetResourceTypeFromGenericType),
            Test("Requests.FhirResourceTypeResolver resolves instance resource type", FhirResourceTypeResolverTests.GetResourceTypeFromResourceInstance),
            Test("Responses.FhirResponseHandler parses successful resource", FhirResponseHandlerTests.HandleRequiredResourceAsyncParsesSuccessfulBody),
            Test("Responses.FhirResponseHandler returns null for 404 optional resource", FhirResponseHandlerTests.HandleOptionalResourceAsyncReturnsNullForNotFound),
            Test("Responses.FhirResponseHandler rejects empty successful body", FhirResponseHandlerTests.HandleRequiredResourceAsyncRejectsEmptyBody),
            Test("Responses.FhirResponseHandler preserves HTTP error details", FhirResponseHandlerTests.HandleRequiredResourceAsyncThrowsHttpExceptionForNonSuccess),
            Test("Responses.FhirResponseHandler wraps parser failures", FhirResponseHandlerTests.HandleRequiredResourceAsyncWrapsParserFailure),
            Test("Search.FhirSearchParameter encodes name and value", FhirSearchParameterTests.ToQueryStringEncodesNameAndValue),
            Test("Search.FhirSearchParameter rejects empty name", FhirSearchParameterTests.ConstructorRejectsEmptyName),
            Test("Search.FhirSearchQuery builds query in insertion order", FhirSearchQueryTests.ToQueryStringBuildsParametersInInsertionOrder),
            Test("Search.FhirSearchQuery supports empty query", FhirSearchQueryTests.ToQueryStringReturnsEmptyStringForNoParameters),
            Test("Search.FhirSearchQuery rejects negative count", FhirSearchQueryTests.CountRejectsNegativeValues),
            Test("FhirClient.ReadAsync sends request and parses response", FhirClientTests.ReadAsyncSendsRequestAndParsesResponse),
            Test("FhirClient.ReadAsync returns null for not found", FhirClientTests.ReadAsyncReturnsNullForNotFound),
            Test("FhirClient.CreateAsync serializes and sends resource", FhirClientTests.CreateAsyncSerializesAndSendsResource),
            Test("FhirClient.SearchAsync sends structured search query", FhirClientTests.SearchAsyncSendsStructuredSearchQuery)
        };

        var failures = new List<string>();

        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures.Add($"""
                    {test.Name}

                    {exception.GetType().Name}: {exception.Message}
                    {exception.StackTrace}
                    """);
                Console.Error.WriteLine($"FAIL {test.Name}");
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine($"All {tests.Count} Client tests passed.");
            return 0;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine($"{failures.Count} Client test(s) failed.");
        Console.Error.WriteLine();
        Console.Error.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failures));
        return 1;
    }

    private static (string Name, Func<Task> Run) Test(string name, Action run)
    {
        return (name, () =>
        {
            run();
            return Task.CompletedTask;
        });
    }

    private static (string Name, Func<Task> Run) Test(string name, Func<Task> run)
    {
        return (name, run);
    }
}

internal static class TestAssert
{
    public static void AreEqual<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? $"Expected '{expected}', but got '{actual}'.");
        }
    }

    public static void AreSame(object expected, object actual, string? message = null)
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new InvalidOperationException(message ?? "Expected both values to reference the same instance.");
        }
    }

    public static void IsNull(object? value, string? message = null)
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message ?? $"Expected null, but got '{value}'.");
        }
    }

    public static void IsNotNull(object? value, string? message = null)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message ?? "Expected a non-null value.");
        }
    }

    public static void IsTrue(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message ?? "Expected condition to be true.");
        }
    }

    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name}, but got {exception.GetType().Name}.",
                exception);
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name}, but got {exception.GetType().Name}.",
                exception);
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }
}
