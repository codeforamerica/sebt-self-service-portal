import { describe, expect, it } from 'vitest'
import {
  buildResourceAttributes,
  isFullyDisabled,
  resolveOtelConfig,
  type OtelConfig
} from './config'

const options = { defaultServiceName: 'sebt-test-web' }

describe('resolveOtelConfig', () => {
  it('disables every signal when no OTLP endpoint is configured', () => {
    const config = resolveOtelConfig({}, options)

    expect(config.traces).toBe('none')
    expect(config.metrics).toBe('none')
    expect(config.logs).toBe('none')
  })

  it('defaults every signal to otlp when a general endpoint is set', () => {
    const config = resolveOtelConfig(
      { OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:4318' },
      options
    )

    expect(config.traces).toBe('otlp')
    expect(config.metrics).toBe('otlp')
    expect(config.logs).toBe('otlp')
  })

  it('enables only the signal whose signal-specific endpoint is set', () => {
    const config = resolveOtelConfig(
      { OTEL_EXPORTER_OTLP_TRACES_ENDPOINT: 'http://localhost:4318/v1/traces' },
      options
    )

    expect(config.traces).toBe('otlp')
    expect(config.metrics).toBe('none')
    expect(config.logs).toBe('none')
  })

  it('uses the default service name when OTEL_SERVICE_NAME is unset', () => {
    expect(resolveOtelConfig({}, options).serviceName).toBe('sebt-test-web')
  })

  it('prefers OTEL_SERVICE_NAME over the default', () => {
    const config = resolveOtelConfig({ OTEL_SERVICE_NAME: 'sebt-portal-web' }, options)

    expect(config.serviceName).toBe('sebt-portal-web')
  })

  it('reads service version and deployment environment when present', () => {
    const config = resolveOtelConfig(
      {
        OTEL_SERVICE_VERSION: '1.2.3',
        OTEL_DEPLOYMENT_ENVIRONMENT: 'dev-dc'
      },
      options
    )

    expect(config.serviceVersion).toBe('1.2.3')
    expect(config.deploymentEnvironment).toBe('dev-dc')
  })

  it('falls back to NODE_ENV for deployment environment', () => {
    const config = resolveOtelConfig({ NODE_ENV: 'production' }, options)

    expect(config.deploymentEnvironment).toBe('production')
  })

  it('honors per-signal exporter overrides', () => {
    const config = resolveOtelConfig(
      {
        OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:4318',
        OTEL_TRACES_EXPORTER: 'console',
        OTEL_LOGS_EXPORTER: 'none'
      },
      options
    )

    expect(config.traces).toBe('console')
    expect(config.metrics).toBe('otlp')
    expect(config.logs).toBe('none')
  })

  it('can enable a single signal to the console with no endpoint', () => {
    const config = resolveOtelConfig({ OTEL_TRACES_EXPORTER: 'console' }, options)

    expect(config.traces).toBe('console')
    expect(config.metrics).toBe('none')
    expect(config.logs).toBe('none')
  })

  it('ignores an unrecognized exporter value and falls back to the default', () => {
    const config = resolveOtelConfig(
      {
        OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:4318',
        OTEL_TRACES_EXPORTER: 'nonsense'
      },
      options
    )

    expect(config.traces).toBe('otlp')
  })

  it('trims and case-folds exporter values and treats blank as unset', () => {
    const config = resolveOtelConfig(
      {
        OTEL_EXPORTER_OTLP_ENDPOINT: 'http://localhost:4318',
        OTEL_METRICS_EXPORTER: '  Console  ',
        OTEL_SERVICE_NAME: '   '
      },
      options
    )

    expect(config.metrics).toBe('console')
    expect(config.serviceName).toBe('sebt-test-web')
  })

  it('defaults the protocol to grpc (matching the backend)', () => {
    expect(resolveOtelConfig({}, options).protocol).toBe('grpc')
  })

  it('selects http/protobuf from OTEL_EXPORTER_OTLP_PROTOCOL (incl. the "http" shorthand)', () => {
    expect(
      resolveOtelConfig({ OTEL_EXPORTER_OTLP_PROTOCOL: 'http/protobuf' }, options).protocol
    ).toBe('http/protobuf')
    expect(resolveOtelConfig({ OTEL_EXPORTER_OTLP_PROTOCOL: 'http' }, options).protocol).toBe(
      'http/protobuf'
    )
  })

  it('falls back to grpc for an unrecognized protocol', () => {
    expect(
      resolveOtelConfig({ OTEL_EXPORTER_OTLP_PROTOCOL: 'carrier-pigeon' }, options).protocol
    ).toBe('grpc')
  })
})

describe('isFullyDisabled', () => {
  it('is true only when all three signals are none', () => {
    const base: OtelConfig = {
      serviceName: 's',
      protocol: 'grpc',
      traces: 'none',
      metrics: 'none',
      logs: 'none'
    }

    expect(isFullyDisabled(base)).toBe(true)
    expect(isFullyDisabled({ ...base, traces: 'otlp' })).toBe(false)
  })
})

describe('buildResourceAttributes', () => {
  it('always includes the service name', () => {
    const attributes = buildResourceAttributes({
      serviceName: 'sebt-portal-web',
      protocol: 'grpc',
      traces: 'otlp',
      metrics: 'otlp',
      logs: 'otlp'
    })

    expect(attributes['service.name']).toBe('sebt-portal-web')
  })

  it('omits version and environment when absent', () => {
    const attributes = buildResourceAttributes({
      serviceName: 'sebt-portal-web',
      protocol: 'grpc',
      traces: 'none',
      metrics: 'none',
      logs: 'none'
    })

    expect(attributes).not.toHaveProperty('service.version')
    expect(attributes).not.toHaveProperty('deployment.environment.name')
  })

  it('includes version and environment when present', () => {
    const attributes = buildResourceAttributes({
      serviceName: 'sebt-portal-web',
      protocol: 'grpc',
      serviceVersion: '1.2.3',
      deploymentEnvironment: 'dev-dc',
      traces: 'otlp',
      metrics: 'otlp',
      logs: 'otlp'
    })

    expect(attributes['service.version']).toBe('1.2.3')
    expect(attributes['deployment.environment.name']).toBe('dev-dc')
  })
})
