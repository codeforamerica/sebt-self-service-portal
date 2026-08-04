import { isFullStackE2E } from './full-stack'

const API_BASE_URL = process.env.BACKEND_URL ?? process.env.API_BASE_URL ?? 'http://localhost:5280'

/**
 * Deletes and recreates a known seed scenario user via the local/CI seed helper
 * (`Seeding:EnableDevEndpoints` must be true on the API).
 * Use in beforeAll for suites that mutate seed personas.
 */
export async function reseedUserScenario(scenarioName: string): Promise<void> {
  if (!isFullStackE2E) {
    return
  }

  const response = await fetch(
    `${API_BASE_URL}/api/dev/seed/reseed/${encodeURIComponent(scenarioName)}`,
    { method: 'POST' }
  )

  if (!response.ok) {
    const body = await response.text()
    throw new Error(
      `Failed to reseed scenario '${scenarioName}' (${response.status}): ${body || response.statusText}`
    )
  }
}
