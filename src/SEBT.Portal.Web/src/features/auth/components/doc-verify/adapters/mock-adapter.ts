import type { DocVAdapter, DocVAdapterConfig } from './types'

/**
 * Mock implementation of DocVAdapter for development and testing.
 *
 * Simulates the Socure DocV SDK lifecycle without loading any external scripts.
 * Renders a placeholder UI into the container and fires onSuccess after a
 * configurable delay. Controlled by NEXT_PUBLIC_MOCK_SOCURE=true.
 */

const DEFAULT_DELAY_MS = 1500

function buildMockCaptureUI(): HTMLDivElement {
  const wrapper = document.createElement('div')
  wrapper.style.cssText =
    'padding: 2rem; text-align: center; border: 2px dashed #71767a; border-radius: 8px;'

  const heading = document.createElement('p')
  heading.style.cssText = 'font-size: 1.25rem; font-weight: 600; margin-bottom: 0.5rem;'
  heading.textContent = 'Mock Document Capture'

  const description = document.createElement('p')
  description.style.cssText = 'color: #71767a;'
  description.textContent = 'Simulating document upload — this will complete automatically.'

  wrapper.appendChild(heading)
  wrapper.appendChild(description)
  return wrapper
}

export class MockDocVAdapter implements DocVAdapter {
  private launched = false
  private timeoutId: ReturnType<typeof setTimeout> | null = null

  launch(config: DocVAdapterConfig): Promise<void> {
    // Idempotent — prevent double-launch from React Strict Mode
    if (this.launched) {
      return Promise.resolve()
    }
    this.launched = true

    const container = document.getElementById(config.containerId)
    if (container) {
      container.replaceChildren(buildMockCaptureUI())
    }

    config.onProgress?.({ type: 'documentDetected' })

    this.timeoutId = setTimeout(() => {
      config.onSuccess({ documentType: 'mock-license', status: 'captured' })
    }, DEFAULT_DELAY_MS)

    return Promise.resolve()
  }

  reset(): void {
    if (this.timeoutId !== null) {
      clearTimeout(this.timeoutId)
      this.timeoutId = null
    }
    this.launched = false
  }

  isLoaded(): boolean {
    return true
  }
}
