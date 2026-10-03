// Both states export to the dashboard that Aspire runs.
//
// Compose has no destination for telemetry. appsettings.json still points the exporter
// at http://localhost:4317, a Jaeger container that the repository no longer runs.
//
// 3 resources export: the API through OpenTelemetrySetup, and the 2 Next.js applications
// through startOtel in packages/observability. Read
// docs/adr/0018-web-tier-opentelemetry.md.

import type { ConfigSetting } from "../requirements.mjs";
import type {
  TelemetryCapability,
  TelemetryContext,
  TelemetryProvider,
} from "./contract.mjs";

function provision({
  config,
  api,
  web,
  checker,
}: TelemetryContext): Promise<TelemetryCapability> {
  const settings: ConfigSetting[] = [
    {
      // appsettings.json sets this to `console`. With that value the API registers no
      // OpenTelemetry log provider, LoggingSetup keeps Serilog writeToProviders false,
      // and the structured logs of the dashboard stay empty. The only other file that
      // changes it is the gitignored appsettings.Development.json.
      //
      // The destination needs no override: the log exporter reads the standard
      // OTEL_EXPORTER_OTLP_* variables that the exporter below sets.
      key: "Otel__UseLogExporter",
      value: "otlp",
      why: "Without it the API registers no OTLP log provider, and the logs of the dashboard stay empty.",
    },
  ];

  // OpenTelemetrySetup binds OtlpExporterOptions from the `Otel:OtlpExporter` section,
  // which outranks the OTEL_EXPORTER_OTLP_ENDPOINT that the exporter sets. Without this
  // override the traces and the metrics go to the Jaeger address.
  //
  // The value comes from the launch profile, so it is absent with no profile. Read
  // `dashboardOtlpEndpoint` in ../../config.mts for why there is no default.
  if (config.dashboardOtlpEndpoint) {
    settings.push({
      key: "Otel__OtlpExporter__Endpoint",
      value: config.dashboardOtlpEndpoint,
      why: "The `Otel:OtlpExporter` section outranks OTEL_EXPORTER_OTLP_ENDPOINT and holds the old Jaeger address.",
    });
  }

  return Promise.resolve({
    requirements: {
      settings,
      exporters: [
        {
          resource: api,
          why: "OpenTelemetrySetup needs the address of the dashboard for its 3 signals.",
        },
        {
          // startOtel sets each signal to 'none' when OTEL_EXPORTER_OTLP_ENDPOINT is
          // absent, so the SDK never starts.
          //
          // `browserLogs` tracks a browser that Aspire starts, in a Chromium user data
          // directory that it controls. Its console output and its screenshots land on
          // the dashboard beside the telemetry of the server.
          resource: web,
          browserLogs: true,
          why: "Without an endpoint startOtel disables every signal, so the portal would send nothing.",
        },
        {
          resource: checker,
          browserLogs: true,
          why: "The checker registers the same startOtel as the portal.",
        },
      ],
      waits: [],
    },
  });
}

export const aspireDashboard: TelemetryProvider = {
  name: "the Aspire dashboard over OTLP",
  telemetryHint:
    "Traces, metrics, and structured logs are on the dashboard, with the browser console of the portal and the checker.",
  // The dashboard is part of Aspire, so the machine needs nothing for it.
  preflight: () => [],
  provision,
};
