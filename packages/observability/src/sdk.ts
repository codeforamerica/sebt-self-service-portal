/**
 * Thin OpenTelemetry Node SDK wiring.
 *
 * This is Node-only: it pulls in OpenTelemetry packages that depend on
 * async_hooks / perf_hooks. Import it exclusively from a Node runtime entry
 * point (Next.js `instrumentation.node.ts`) — never from client or edge code.
 *
 * All decisions about *what* to export live in ./config. This file only turns
 * a resolved config into exporters/processors and starts the SDK.
 */
import { OTLPLogExporter as OTLPLogExporterGrpc } from '@opentelemetry/exporter-logs-otlp-grpc'
import { OTLPLogExporter as OTLPLogExporterProto } from '@opentelemetry/exporter-logs-otlp-proto'
import { OTLPMetricExporter as OTLPMetricExporterGrpc } from '@opentelemetry/exporter-metrics-otlp-grpc'
import { OTLPMetricExporter as OTLPMetricExporterProto } from '@opentelemetry/exporter-metrics-otlp-proto'
import { OTLPTraceExporter as OTLPTraceExporterGrpc } from '@opentelemetry/exporter-trace-otlp-grpc'
import { OTLPTraceExporter as OTLPTraceExporterProto } from '@opentelemetry/exporter-trace-otlp-proto'
import { UndiciInstrumentation } from '@opentelemetry/instrumentation-undici'
import { resourceFromAttributes } from '@opentelemetry/resources'
import { NodeSDK } from '@opentelemetry/sdk-node'
import {
  BatchLogRecordProcessor,
  ConsoleLogRecordExporter,
  type LogRecordProcessor,
  SimpleLogRecordProcessor
} from '@opentelemetry/sdk-logs'
import {
  ConsoleMetricExporter,
  type MetricReader,
  PeriodicExportingMetricReader
} from '@opentelemetry/sdk-metrics'
import {
  BatchSpanProcessor,
  ConsoleSpanExporter,
  SimpleSpanProcessor,
  type SpanProcessor
} from '@opentelemetry/sdk-trace-node'
import {
  buildResourceAttributes,
  type ExporterMode,
  isFullyDisabled,
  type OtelConfig,
  type OtlpProtocol,
  resolveOtelConfig,
  type ResolveOtelConfigOptions
} from './config'

/**
 * The SDK building blocks derived from a config. Constructing exporters is
 * side-effect-free — no network connection opens until the first export — so
 * this is safe to build (and assert on) in a test without starting the SDK.
 */
export interface SdkComponents {
  spanProcessors: SpanProcessor[]
  metricReader?: MetricReader
  logRecordProcessors: LogRecordProcessor[]
}

function traceProcessor(mode: ExporterMode, protocol: OtlpProtocol): SpanProcessor | undefined {
  switch (mode) {
    case 'otlp':
      return new BatchSpanProcessor(
        protocol === 'grpc' ? new OTLPTraceExporterGrpc() : new OTLPTraceExporterProto()
      )
    case 'console':
      return new SimpleSpanProcessor(new ConsoleSpanExporter())
    case 'none':
      return undefined
  }
}

function metricReader(mode: ExporterMode, protocol: OtlpProtocol): MetricReader | undefined {
  switch (mode) {
    case 'otlp':
      return new PeriodicExportingMetricReader({
        exporter: protocol === 'grpc' ? new OTLPMetricExporterGrpc() : new OTLPMetricExporterProto()
      })
    case 'console':
      return new PeriodicExportingMetricReader({ exporter: new ConsoleMetricExporter() })
    case 'none':
      return undefined
  }
}

function logProcessor(mode: ExporterMode, protocol: OtlpProtocol): LogRecordProcessor | undefined {
  switch (mode) {
    case 'otlp':
      return new BatchLogRecordProcessor({
        exporter: protocol === 'grpc' ? new OTLPLogExporterGrpc() : new OTLPLogExporterProto()
      })
    case 'console':
      return new SimpleLogRecordProcessor({ exporter: new ConsoleLogRecordExporter() })
    case 'none':
      return undefined
  }
}

/**
 * Node's `fetch` (undici) instrumentation. This is what injects the W3C
 * traceparent header on the proxy's outbound fetch to the .NET backend, so the
 * backend continues the same trace. Without it the web spans wouldn't connect
 * to the API. Only meaningful when traces are exported.
 */
export function buildInstrumentations(config: OtelConfig): UndiciInstrumentation[] {
  return config.traces === 'none' ? [] : [new UndiciInstrumentation()]
}

/** Turn a resolved config into the SDK's exporters/processors. */
export function buildSdkComponents(config: OtelConfig): SdkComponents {
  const span = traceProcessor(config.traces, config.protocol)
  const reader = metricReader(config.metrics, config.protocol)
  const log = logProcessor(config.logs, config.protocol)

  const components: SdkComponents = {
    spanProcessors: span ? [span] : [],
    logRecordProcessors: log ? [log] : []
  }
  if (reader) {
    components.metricReader = reader
  }
  return components
}

let started: NodeSDK | undefined

/**
 * Flush telemetry best-effort on shutdown so the final batch isn't lost when a
 * container receives SIGTERM. This does not block process exit — Next owns the
 * lifecycle — so a very late batch can still be dropped; that's an acceptable
 * trade for not interfering with the server's own shutdown.
 */
function registerGracefulShutdown(sdk: NodeSDK): void {
  const flush = (): void => {
    void sdk.shutdown().catch((error) => {
      console.error('[otel] error during shutdown', error)
    })
  }
  process.once('SIGTERM', flush)
  process.once('SIGINT', flush)
}

/**
 * Start the OpenTelemetry Node SDK from the environment.
 *
 * Idempotent: a second call returns the already-started SDK. Returns undefined
 * when every signal is disabled (nothing to export) — the "dark" state — so an
 * unconfigured environment pays no cost.
 *
 * Call once, as early as possible, from the Node runtime.
 */
export function startOtel(options: ResolveOtelConfigOptions): NodeSDK | undefined {
  if (started) {
    return started
  }

  const config = resolveOtelConfig(process.env, options)
  if (isFullyDisabled(config)) {
    return undefined
  }

  const { spanProcessors, metricReader, logRecordProcessors } = buildSdkComponents(config)

  // Assemble options conditionally: only pass a key when its signal is enabled.
  // This keeps us honest under exactOptionalPropertyTypes and avoids handing the
  // SDK an empty processor array for a disabled signal.
  const sdkConfig: ConstructorParameters<typeof NodeSDK>[0] = {
    resource: resourceFromAttributes(buildResourceAttributes(config))
  }
  if (spanProcessors.length > 0) {
    sdkConfig.spanProcessors = spanProcessors
  }
  if (metricReader) {
    sdkConfig.metricReader = metricReader
  }
  if (logRecordProcessors.length > 0) {
    sdkConfig.logRecordProcessors = logRecordProcessors
  }
  const instrumentations = buildInstrumentations(config)
  if (instrumentations.length > 0) {
    sdkConfig.instrumentations = instrumentations
  }

  const sdk = new NodeSDK(sdkConfig)
  sdk.start()
  registerGracefulShutdown(sdk)
  started = sdk
  return sdk
}
