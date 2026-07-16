/**
 * Vendor-agnostic interface for document verification SDKs (D5).
 *
 * App code depends on this interface — never on the Socure global directly.
 * Two implementations exist: SocureDocVAdapter (real SDK) and MockDocVAdapter
 * (for dev/test when no sandbox credentials are available).
 */

export interface DocVAdapterConfig {
  /** Socure SDK key (API credential) */
  sdkKey: string
  /** One-time transaction token from GET /challenges/:id/start */
  token: string
  /** DOM element ID where the SDK renders its capture UI */
  containerId: string
  /** Called when document capture completes successfully */
  onSuccess: (result: unknown) => void
  /** Called when the SDK encounters an error */
  onError: (error: unknown) => void
  /** Called on progress events (e.g., document detected, uploading) */
  onProgress?: (event: unknown) => void
}

export interface DocVAdapter {
  /** Initialize and launch the capture UI. Must be called from a user gesture (D9). */
  launch(config: DocVAdapterConfig): Promise<void>
  /** Tear down SDK state and remove injected DOM elements. Safe to call multiple times. */
  reset(): void
  /** Whether the SDK script has been loaded and is ready to launch. */
  isLoaded(): boolean
}
