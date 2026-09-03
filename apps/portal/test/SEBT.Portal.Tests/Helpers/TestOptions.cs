using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Helpers;

/// <summary>
/// Test analog to <see cref="Options.Create{TOptions}(TOptions)"/> for
/// <see cref="IOptionsSnapshot{TOptions}"/>, which has no framework factory. Wraps a fixed
/// value; both <see cref="IOptionsSnapshot{TOptions}.Get"/> and the inherited
/// <see cref="IOptions{TOptions}.Value"/> return it.
/// </summary>
public static class TestOptions
{
    /// <summary>Creates an <see cref="IOptionsSnapshot{T}"/> that always yields <paramref name="value"/>.</summary>
    public static IOptionsSnapshot<T> Snapshot<T>(T value) where T : class => new StaticSnapshot<T>(value);

    private sealed class StaticSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value { get; } = value;

        public T Get(string? name) => Value;
    }
}
