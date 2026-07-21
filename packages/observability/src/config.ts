/**
 * Vendor-neutral OpenTelemetry configuration, resolved from the standard OTEL_*
 * environment variables.
 *
 * This module is pure: no SDK imports, no side effects. It only reads an
 * environment bag and returns a plain description of what to export. That keeps
 * it trivially unit-testable and lets the SDK layer stay a thin wiring shell.
 *
 * The web apps emit OTLP to a collector (an ADOT sidecar in ECS, Jaeger in local
 * dev); the collector routes to Datadog and/or Splunk. Nothing here is
 * vendor-specific — the same binary points anywhere via environment alone.
 */

/** How a single signal (traces, metrics, or logs) is exported. */
export type ExporterMode = 'otlp' | 'console' | 'none'

/**
 * OTLP wire protocol. gRPC (:4317) is the default to match the .NET backend;
 * http/protobuf (:4318) is available for environments where gRPC is awkward
 * (e.g. behind an HTTP-only proxy or load balancer).
 */
export type OtlpProtocol = 'grpc' | 'http/protobuf'

export interface OtelConfig {
  serviceName: string
  serviceVersion?: string
  deploymentEnvironment?: string
  protocol: OtlpProtocol
  traces: ExporterMode
  metrics: ExporterMode
  logs: ExporterMode
}

export interface ResolveOtelConfigOptions {
  /** Service name to use when OTEL_SERVICE_NAME is not set. */
  defaultServiceName: string
}

const VALID_MODES: readonly ExporterMode[] = ['otlp', 'console', 'none']

/**
 * Parse a standard OTEL_*_EXPORTER value. Returns undefined for unset or
 * unrecognized values so the caller can fall back to the endpoint-derived
 * default rather than silently disabling a signal.
 */
function parseMode(value: string | undefined): ExporterMode | undefined {
  const normalized = value?.trim().toLowerCase()
  return VALID_MODES.find((mode) => mode === normalized)
}

/** Trim to a non-empty string, or undefined. */
function cleanString(value: string | undefined): string | undefined {
  const trimmed = value?.trim()
  return trimmed ? trimmed : undefined
}

/**
 * Parse the standard OTEL_EXPORTER_OTLP_PROTOCOL value. Accepts the "http"
 * shorthand for "http/protobuf". Anything unset or unrecognized defaults to
 * gRPC, matching the backend.
 */
function parseProtocol(value: string | undefined): OtlpProtocol {
  const normalized = value?.trim().toLowerCase()
  if (normalized === 'http/protobuf' || normalized === 'http' || normalized === 'http/proto') {
    return 'http/protobuf'
  }
  return 'grpc'
}

/**
 * Resolve OpenTelemetry configuration from the environment.
 *
 * The default per-signal mode is driven by whether any OTLP endpoint is
 * configured: endpoint present -> 'otlp'; absent -> 'none'. "None" is the
 * inert/dark state — e.g. a deployed environment not yet pointed at a collector.
 *
 * Each signal can be overridden independently with the standard
 * OTEL_{TRACES,METRICS,LOGS}_EXPORTER variables (otlp | console | none), which
 * is how local dev flips a signal to the console exporter for quick inspection.
 */
export function resolveOtelConfig(
  env: NodeJS.ProcessEnv,
  options: ResolveOtelConfigOptions
): OtelConfig {
  const hasEndpoint =
    cleanString(env.OTEL_EXPORTER_OTLP_ENDPOINT) !== undefined ||
    cleanString(env.OTEL_EXPORTER_OTLP_TRACES_ENDPOINT) !== undefined ||
    cleanString(env.OTEL_EXPORTER_OTLP_METRICS_ENDPOINT) !== undefined ||
    cleanString(env.OTEL_EXPORTER_OTLP_LOGS_ENDPOINT) !== undefined

  const defaultMode: ExporterMode = hasEndpoint ? 'otlp' : 'none'

  const config: OtelConfig = {
    serviceName: cleanString(env.OTEL_SERVICE_NAME) ?? options.defaultServiceName,
    protocol: parseProtocol(env.OTEL_EXPORTER_OTLP_PROTOCOL),
    traces: parseMode(env.OTEL_TRACES_EXPORTER) ?? defaultMode,
    metrics: parseMode(env.OTEL_METRICS_EXPORTER) ?? defaultMode,
    logs: parseMode(env.OTEL_LOGS_EXPORTER) ?? defaultMode
  }

  const serviceVersion = cleanString(env.OTEL_SERVICE_VERSION)
  if (serviceVersion) {
    config.serviceVersion = serviceVersion
  }
  const deploymentEnvironment =
    cleanString(env.OTEL_DEPLOYMENT_ENVIRONMENT) ?? cleanString(env.NODE_ENV)
  if (deploymentEnvironment) {
    config.deploymentEnvironment = deploymentEnvironment
  }

  return config
}

/** True when no signal is being exported — the SDK need not start at all. */
export function isFullyDisabled(config: OtelConfig): boolean {
  return config.traces === 'none' && config.metrics === 'none' && config.logs === 'none'
}

/**
 * Build OpenTelemetry Resource attributes from resolved config. Keys are OTEL
 * semantic-convention names; literal strings are used (rather than importing
 * the semantic-conventions package) to keep this module dependency-free.
 */
export function buildResourceAttributes(config: OtelConfig): Record<string, string> {
  const attributes: Record<string, string> = {
    'service.name': config.serviceName
  }
  if (config.serviceVersion) {
    attributes['service.version'] = config.serviceVersion
  }
  if (config.deploymentEnvironment) {
    attributes['deployment.environment.name'] = config.deploymentEnvironment
  }
  return attributes
}
