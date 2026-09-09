
using System.Diagnostics;
using System.Diagnostics.Metrics;
using SEBT.Portal.Kernel.Telemetry;

/// <summary>
/// 
/// </summary>
public sealed class InstrumentationSource : IInstrumentationSource, IDisposable
{
    internal const string ActivitySourceName = "sebt-portal-api";
    internal const string MeterName = "sebt-portal-api";
    private readonly Meter _meter;

    /// <summary>
    /// The <see cref="ActivitySource"/> for the portal, used to track named activities for tracing. 
    /// </summary>
    public ActivitySource ActivitySource { get; }

    // TODO - counters?

    /// <summary>
    /// Constructs a new <c>InstrumentationSource</c> wrapping a <see cref="ActivitySource"/>.
    /// </summary>
    public InstrumentationSource()
    {
        ActivitySource = new ActivitySource(ActivitySourceName);
        _meter = new Meter(MeterName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}
