
namespace SEBT.Portal.Core.AppSettings;
/// <summary>
/// Represents the "Otel" section of appsettings.json, which configures OpenTelemetry exporters and options.
/// </summary>
/// <remarks>
/// This appsettings section is used by OpenTelemetrySetup to configure the OpenTelemetry SDK. 
/// It is also used by the OpenTelemetrySetupTests to verify that the appsettings.json configuration is valid and consistent with the SDK's expectations.
/// </remarks>
public class OpenTelemetrySettings
{
    public const string SectionName = "Otel";
    /// <summary>
    /// Gets or sets the kind of exporter to use for tracing. Valid values are "None", "Console", or "Otlp". Default is "Otlp".
    /// </summary>
    public ExporterKind UseTracingExporter { get; set; } = ExporterKind.Otlp;
    /// <summary>
    /// Gets or sets the kind of exporter to use for metrics. Valid values are "None", "Console", or "Otlp". Default is "Otlp".
    /// </summary>
    public ExporterKind UseMetricsExporter { get; set; } = ExporterKind.Otlp;
    /// <summary>
    /// Gets or sets the kind of exporter to use for logs. Valid values are "None", "Console", or "Otlp". Default is "Console".
    /// </summary>
    public ExporterKind UseLogExporter { get; set; } = ExporterKind.Console;
    /// <summary>
    /// Gets or sets the kind of histogram aggregation to use for metrics. Valid values are "Explicit" or "Exponential". Default is "Explicit".
    /// </summary>
    public HistogramAggregationKind HistogramAggregation { get; set; } = HistogramAggregationKind.Explicit;

    #region "Nested OpenTelemetry SDK Settings Classes"
    public OtlpExporterSettings OtlpExporter { get; set; } = new();
    public AspNetCoreInstrumentationSettings AspNetCoreInstrumentation { get; set; } = new();
    #endregion

    public class OtlpExporterSettings
    {
        public Uri Endpoint { get; set; } = new Uri("http://localhost:4317");
    }

    public class AspNetCoreInstrumentationSettings
    {
        public bool RecordException { get; set; } = true;   // binder coerces "true" string -> bool
    }

    public enum ExporterKind { None, Console, Otlp }
    public enum HistogramAggregationKind { Explicit, Exponential }

}