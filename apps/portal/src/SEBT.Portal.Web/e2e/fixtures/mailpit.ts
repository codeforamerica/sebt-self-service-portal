const MAILPIT_API_URL = process.env.MAILPIT_API_URL ?? 'http://localhost:8025'

interface MailpitMessageSummary {
  ID: string
  To?: Array<{ Address: string }>
  Subject?: string
}

interface MailpitSearchResponse {
  messages?: MailpitMessageSummary[]
}

interface MailpitMessage {
  Text?: string
  HTML?: string
}

const OTP_PATTERN = /\b(\d{6})\b/

/** Removes all captured messages so OTP lookups are deterministic between tests. */
export async function clearMailpitMessages(): Promise<void> {
  await fetch(`${MAILPIT_API_URL}/api/v1/messages`, { method: 'DELETE' })
}

async function sleep(ms: number): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, ms))
}

/**
 * Polls Mailpit until an OTP email arrives for the given address.
 * The API runs in Node (Playwright test process), not in the browser.
 */
export async function waitForOtpEmail(recipientEmail: string, timeoutMs = 30_000): Promise<string> {
  const deadline = Date.now() + timeoutMs
  const normalizedRecipient = recipientEmail.toLowerCase()

  while (Date.now() < deadline) {
    const searchUrl = `${MAILPIT_API_URL}/api/v1/search?query=${encodeURIComponent(recipientEmail)}`
    const searchResponse = await fetch(searchUrl)

    if (searchResponse.ok) {
      const searchBody = (await searchResponse.json()) as MailpitSearchResponse
      const messages = searchBody.messages ?? []

      for (const summary of messages) {
        const recipients = summary.To?.map((to) => to.Address.toLowerCase()) ?? []
        if (recipients.length > 0 && !recipients.some((to) => to.includes(normalizedRecipient))) {
          continue
        }

        const messageResponse = await fetch(`${MAILPIT_API_URL}/api/v1/message/${summary.ID}`)
        if (!messageResponse.ok) {
          continue
        }

        const message = (await messageResponse.json()) as MailpitMessage
        const body = message.Text ?? message.HTML ?? ''
        const match = body.match(OTP_PATTERN)
        const code = match?.[1]
        if (code) {
          return code
        }
      }
    }

    await sleep(500)
  }

  throw new Error(`Timed out waiting for OTP email to ${recipientEmail} in Mailpit`)
}
