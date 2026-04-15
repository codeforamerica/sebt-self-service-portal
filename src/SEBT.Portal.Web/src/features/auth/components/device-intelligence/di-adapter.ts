/**
 * Adapter for the Socure Device Intelligence SDK.
 *
 * Loads the DI SDK script, initializes SigmaDeviceManager, and
 * provides a method to retrieve the device session token.
 * The token is passed to the backend with ID proofing submissions
 * and is required by the DocV step-up transaction flow.
 */

const DI_SDK_URL = 'https://sdk.dv.socure.io/latest/device-risk-sdk.js'

declare global {
  interface Window {
    SigmaDeviceManager?: {
      initialize: (config: { sdkKey: string; [key: string]: unknown }) => void
      getSessionToken: () => Promise<string>
    }
  }
}

let scriptLoadPromise: Promise<void> | null = null

function loadDiScript(): Promise<void> {
  if (scriptLoadPromise && !window.SigmaDeviceManager) {
    scriptLoadPromise = null
  }

  if (scriptLoadPromise) {
    return scriptLoadPromise
  }

  if (window.SigmaDeviceManager) {
    scriptLoadPromise = Promise.resolve()
    return scriptLoadPromise
  }

  scriptLoadPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = DI_SDK_URL
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => {
      scriptLoadPromise = null
      reject(new Error('Failed to load Socure DI SDK script'))
    }
    document.head.appendChild(script)
  })

  return scriptLoadPromise
}

let initialized = false

export async function initializeDeviceIntelligence(sdkKey: string): Promise<void> {
  if (initialized) return

  await loadDiScript()

  if (!window.SigmaDeviceManager) {
    throw new Error('SigmaDeviceManager not available after script load')
  }

  window.SigmaDeviceManager.initialize({ sdkKey })
  initialized = true
}

export async function getDeviceSessionToken(): Promise<string | null> {
  if (!window.SigmaDeviceManager) {
    return null
  }

  try {
    return await window.SigmaDeviceManager.getSessionToken()
  } catch {
    return null
  }
}

/** Reset for testing */
export function resetDiState(): void {
  initialized = false
  scriptLoadPromise = null
}
