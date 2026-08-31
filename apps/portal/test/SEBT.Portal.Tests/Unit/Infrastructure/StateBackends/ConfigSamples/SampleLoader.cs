namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends.ConfigSamples;

internal static class SampleLoader
{
    public static string Load(string name)
    {
        var assembly = typeof(SampleLoader).Assembly;
        var ns = typeof(SampleLoader).Namespace;
        var fullName = $"{ns}.{name}";

        using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Failed to locate embedded resource: {fullName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
