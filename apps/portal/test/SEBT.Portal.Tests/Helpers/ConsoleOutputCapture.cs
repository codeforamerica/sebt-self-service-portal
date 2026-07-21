namespace SEBT.Portal.Tests.Helpers;

/// <summary>
/// Serializes <see cref="Console.SetOut"/> capture so parallel unit and integration
/// tests do not steal each other's stdout.
/// </summary>
internal static class ConsoleOutputCapture
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string Capture(Action act)
    {
        Gate.Wait();
        try
        {
            var original = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                act();
                return writer.ToString();
            }
            finally
            {
                Console.SetOut(original);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<string> CaptureAsync(Func<Task> act)
    {
        await Gate.WaitAsync();
        try
        {
            var original = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await act();
                return writer.ToString();
            }
            finally
            {
                Console.SetOut(original);
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
