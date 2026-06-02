import { NodeSDK } from '@opentelemetry/sdk-node'
import { OTLPTraceExporter as OTLPTraceExporterHttp } from '@opentelemetry/exporter-trace-otlp-http'
import { OTLPTraceExporter as OTLPTraceExporterGrpc } from '@opentelemetry/exporter-trace-otlp-grpc'
import { OTLPMetricExporter as OTLPMetricExporterHttp } from '@opentelemetry/exporter-metrics-otlp-http'
import { OTLPMetricExporter as OTLPMetricExporterGrpc } from '@opentelemetry/exporter-metrics-otlp-grpc'
import { OTLPLogExporter as OTLPLogExporterHttp } from '@opentelemetry/exporter-logs-otlp-http'
import { OTLPLogExporter as OTLPLogExporterGrpc } from '@opentelemetry/exporter-logs-otlp-grpc'
import { PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics'
import { BatchLogRecordProcessor } from '@opentelemetry/sdk-logs'
import { UndiciInstrumentation } from '@opentelemetry/instrumentation-undici'

// Mirrors the backend's switch on UseTracingExporter / UseMetricsExporter.
// Defaults to grpc to match the backend default (port 4317).
const protocol = process.env.OTEL_EXPORTER_OTLP_PROTOCOL ?? 'grpc'
const isGrpc = protocol === 'grpc'

const tracesEnabled = (process.env.OTEL_TRACES_EXPORTER ?? 'otlp') !== 'none'
const metricsEnabled = (process.env.OTEL_METRICS_EXPORTER ?? 'otlp') !== 'none'
// Logs default off, matching the backend's "commented out, planned later" posture.
const logsEnabled = process.env.OTEL_LOGS_EXPORTER === 'otlp'

const sdk = new NodeSDK({
  serviceName: process.env.OTEL_SERVICE_NAME ?? 'sebt-portal-web',

  traceExporter: tracesEnabled
    ? isGrpc
      ? new OTLPTraceExporterGrpc()
      : new OTLPTraceExporterHttp()
    : undefined,

  metricReader: metricsEnabled
    ? new PeriodicExportingMetricReader({
        exporter: isGrpc ? new OTLPMetricExporterGrpc() : new OTLPMetricExporterHttp(),
      })
    : undefined,

  logRecordProcessor: logsEnabled
    ? new BatchLogRecordProcessor(
        isGrpc ? new OTLPLogExporterGrpc() : new OTLPLogExporterHttp()
      )
    : undefined,

  instrumentations: [new UndiciInstrumentation()],
})

sdk.start()
