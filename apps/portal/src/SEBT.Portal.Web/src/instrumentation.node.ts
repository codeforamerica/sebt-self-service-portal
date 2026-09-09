/**
 * Node-runtime OpenTelemetry bootstrap for the portal web server.
 *
 * Starts the shared SDK, which emits OTLP to the collector configured via the
 * standard OTEL_* environment variables. With no endpoint configured it is a
 * no-op, so this is safe to ship dark. The service name can be overridden with
 * OTEL_SERVICE_NAME; the default below matches the backend's "sebt-portal-api"
 * naming.
 */
import { startOtel } from '@sebt/observability'

startOtel({ defaultServiceName: 'sebt-portal-web' })
