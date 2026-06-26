const API_HEALTH_URL = process.env.API_HEALTH_URL ?? 'http://localhost:5280/health'
const WEB_URL = process.env.BASE_URL ?? 'http://localhost:3000'
const MAILPIT_API_URL = process.env.MAILPIT_API_URL ?? 'http://localhost:8025'

async function sleep(ms: number): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, ms))
}

async function waitForOk(url: string, label: string, timeoutMs = 180_000): Promise<void> {
  const deadline = Date.now() + timeoutMs

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url)
      if (response.ok) {
        return
      }
    } catch {
      // Service still starting
    }

    await sleep(2000)
  }

  throw new Error(`${label} did not become ready at ${url} within ${timeoutMs}ms`)
}

export default async function globalSetup(): Promise<void> {
  if (process.env.E2E_FULL_STACK !== '1') {
    return
  }

  await waitForOk(API_HEALTH_URL, 'API')
  await waitForOk(WEB_URL, 'Web app')
  await waitForOk(`${MAILPIT_API_URL}/api/v1/info`, 'Mailpit')
}
