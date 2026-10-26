using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Records every log call with its structured state, so tests can assert on properties and scopes.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private const string OriginalFormatKey = "{OriginalFormat}";

    private readonly List<object> activeScopes = new();

    public List<CapturedLogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        activeScopes.Add(state);
        return new ScopeHandle(() => EndScope(state));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = new Dictionary<string, object?>();
        string? originalFormat = null;

        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var (key, value) in pairs)
            {
                if (key == OriginalFormatKey)
                {
                    originalFormat = value as string;
                }
                else
                {
                    properties[key] = value;
                }
            }
        }

        Entries.Add(new CapturedLogEntry(
            logLevel,
            eventId,
            exception,
            formatter(state, exception),
            originalFormat,
            properties,
            activeScopes.ToArray()));
    }

    private void EndScope(object state)
    {
        var index = activeScopes.LastIndexOf(state);
        if (index >= 0)
        {
            activeScopes.RemoveAt(index);
        }
    }

    private sealed class ScopeHandle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}

/// <summary>
/// One captured log call, with the message template lifted out of <see cref="Properties"/>.
/// </summary>
internal sealed record CapturedLogEntry(
    LogLevel Level,
    EventId EventId,
    Exception? Exception,
    string Message,
    string? OriginalFormat,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyList<object> Scopes);
