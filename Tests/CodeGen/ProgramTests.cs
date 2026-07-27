using MyFhirSdk.CodeGen;
using Xunit;

namespace MyFhirSdk.CodeGen.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Main_WithNoArguments_PrintsUsageAndReturnsNonZero()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();

        try
        {
            Console.SetError(error);

            var exitCode = Program.Main([]);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Main_WithHelp_PrintsUsageAndReturnsZero()
    {
        var originalOutput = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);

            var exitCode = Program.Main(["--help"]);

            Assert.Equal(0, exitCode);
            Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }
}
