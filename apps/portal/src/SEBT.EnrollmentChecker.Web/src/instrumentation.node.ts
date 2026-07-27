/**
 * Node-runtime OpenTelemetry bootstrap for the enrollment checker server.
 *
 * Starts the shared SDK, which emits OTLP to the collector configured via the
 * standard OTEL_* environment variables. With no endpoint configured it is a
 * no-op, so this is safe to ship dark and safe to run during a static export
 * build. The service name can be overridden with OTEL_SERVICE_NAME.
 */
import { startOtel } from '@sebt/observability'

startOtel({ defaultServiceName: 'sebt-enrollment-checker-web' })
