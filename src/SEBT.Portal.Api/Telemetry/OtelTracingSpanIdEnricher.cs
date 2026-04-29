using System.Diagnostics;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

public class OtelTracingSpanIdEnricher : ILogEventEnricher
{
    private const string PropertyName = "span_id";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (Activity.Current?.SpanId is { } spanId)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, spanId.ToString()));
        }
    }
}

public static class OtelTracingSpanIdEnrichmenetExtensions
{
    public static LoggerConfiguration WithOtelTracingSpanId(this LoggerEnrichmentConfiguration enrich) =>
        enrich.With(new OtelTracingSpanIdEnricher());
}
