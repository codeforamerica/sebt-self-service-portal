import { describe, expect, it } from 'vitest'
import { buildInstrumentations, buildSdkComponents } from './sdk'
import type { OtelConfig } from './config'

function config(overrides: Partial<OtelConfig>): OtelConfig {
  return {
    serviceName: 'sebt-test-web',
    protocol: 'grpc',
    traces: 'none',
    metrics: 'none',
    logs: 'none',
    ...overrides
  }
}

describe('buildSdkComponents', () => {
  it('builds nothing when all signals are disabled', () => {
    const components = buildSdkComponents(config({}))

    expect(components.spanProcessors).toHaveLength(0)
    expect(components.metricReader).toBeUndefined()
    expect(components.logRecordProcessors).toHaveLength(0)
  })

  it('builds a span processor when traces are enabled', () => {
    expect(buildSdkComponents(config({ traces: 'otlp' })).spanProcessors).toHaveLength(1)
    expect(buildSdkComponents(config({ traces: 'console' })).spanProcessors).toHaveLength(1)
  })

  it('builds a metric reader when metrics are enabled', () => {
    expect(buildSdkComponents(config({ metrics: 'otlp' })).metricReader).toBeDefined()
    expect(buildSdkComponents(config({ metrics: 'console' })).metricReader).toBeDefined()
  })

  it('builds a log record processor when logs are enabled', () => {
    expect(buildSdkComponents(config({ logs: 'otlp' })).logRecordProcessors).toHaveLength(1)
    expect(buildSdkComponents(config({ logs: 'console' })).logRecordProcessors).toHaveLength(1)
  })

  it('builds only the enabled signals independently', () => {
    const components = buildSdkComponents(config({ traces: 'otlp', logs: 'none', metrics: 'none' }))

    expect(components.spanProcessors).toHaveLength(1)
    expect(components.metricReader).toBeUndefined()
    expect(components.logRecordProcessors).toHaveLength(0)
  })

  it('builds OTLP exporters for either protocol without throwing', () => {
    expect(
      buildSdkComponents(config({ traces: 'otlp', metrics: 'otlp', logs: 'otlp', protocol: 'grpc' }))
        .spanProcessors
    ).toHaveLength(1)
    expect(
      buildSdkComponents(
        config({ traces: 'otlp', metrics: 'otlp', logs: 'otlp', protocol: 'http/protobuf' })
      ).spanProcessors
    ).toHaveLength(1)
  })
})

describe('buildInstrumentations', () => {
  it('includes undici (fetch) instrumentation when traces are enabled', () => {
    expect(buildInstrumentations(config({ traces: 'otlp' }))).toHaveLength(1)
    expect(buildInstrumentations(config({ traces: 'console' }))).toHaveLength(1)
  })

  it('includes nothing when traces are disabled', () => {
    expect(buildInstrumentations(config({ traces: 'none', metrics: 'otlp' }))).toHaveLength(0)
  })
})
