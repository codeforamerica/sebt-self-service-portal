import type { DocVAdapter, DocVAdapterConfig } from './types'

/**
 * Real Socure DocV SDK adapter.
 *
 * Dynamically loads the Socure bundle.js script and wraps the global
 * SocureDocVSDK with idempotent launch/reset guards. Handles:
 * - Script load failures
 * - React Strict Mode double-mount (idempotent launch)
 * - Cleanup on unmount (reset)
 */

const SOCURE_BUNDLE_URL = 'https://websdk.socure.com/bundle.js'

// Extend Window to include the Socure SDK global
declare global {
  interface Window {
    SocureDocVSDK?: {
      launch: (
        sdkKey: string,
        token: string,
        containerId: string,
        config?: Record<string, unknown>
      ) => void
      reset: () => void
    }
  }
}

// Module-level script loading state — shared across adapter instances
// so the script is only loaded once even if multiple adapters are created.
let scriptLoadPromise: Promise<void> | null = null

function loadSocureScript(): Promise<void> {
  // If the cached promise resolved but the SDK global disappeared (e.g., script removed),
  // invalidate the cache so we re-load the script.
  if (scriptLoadPromise && !window.SocureDocVSDK) {
    scriptLoadPromise = null
  }

  if (scriptLoadPromise) {
    return scriptLoadPromise
  }

  // Already loaded from a previous page visit
  if (window.SocureDocVSDK) {
    scriptLoadPromise = Promise.resolve()
    return scriptLoadPromise
  }

  scriptLoadPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = SOCURE_BUNDLE_URL
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => {
      scriptLoadPromise = null // Allow retry on failure
      reject(new Error('Failed to load Socure DocV SDK script'))
    }
    document.head.appendChild(script)
  })

  return scriptLoadPromise
}

export class SocureDocVAdapter implements DocVAdapter {
  private launched = false

  async launch(config: DocVAdapterConfig): Promise<void> {
    // Idempotent — prevent double-launch from React Strict Mode
    if (this.launched) {
      return
    }
    this.launched = true

    await loadSocureScript()

    if (!window.SocureDocVSDK) {
      throw new Error('SocureDocVSDK not available after script load')
    }

    window.SocureDocVSDK.launch(config.sdkKey, config.token, `#${config.containerId}`, {
      type: 'docv',
      autoOpenTabOnMobile: true,
      closeCaptureWindowOnComplete: true,
      onSuccess: config.onSuccess,
      onError: config.onError,
      onProgress: config.onProgress
    })
  }

  reset(): void {
    this.launched = false
    if (window.SocureDocVSDK) {
      try {
        window.SocureDocVSDK.reset()
      } catch {
        // Swallow errors during cleanup — the SDK may already be torn down
      }
    }

    // The SDK caches internal DOM references that go stale after navigation.
    // Fully tear down so the next launch() gets a fresh instance.
    delete window.SocureDocVSDK
    scriptLoadPromise = null
    document.querySelectorAll(`script[src="${SOCURE_BUNDLE_URL}"]`).forEach((el) => el.remove())
  }

  isLoaded(): boolean {
    return !!window.SocureDocVSDK
  }
}
