'use client'

import { useEffect, useRef } from 'react'

import type { DocVAdapter, DocVAdapterConfig } from './adapters'

const SDK_CONTAINER_ID = 'websdk'

interface DocVerifyCaptureProps {
  adapter: DocVAdapter
  launchConfig: Omit<DocVAdapterConfig, 'containerId'>
}

/**
 * Renders the SDK container div and launches document capture on mount.
 *
 * The parent transitions to this sub-state from a click handler (satisfying
 * the D9 intent of not auto-launching on page load). The adapter launches
 * after the container div is in the DOM.
 */
export function DocVerifyCapture({ adapter, launchConfig }: DocVerifyCaptureProps) {
  // useRef prevents React from interfering with the SDK-managed DOM (D5, Gemini §3)
  const containerRef = useRef<HTMLDivElement>(null)
  const adapterRef = useRef(adapter)
  // eslint-disable-next-line react-hooks/refs -- Intentional: sync latest prop into ref so the mount effect reads fresh values without re-firing
  adapterRef.current = adapter

  // Stable ref to config so the effect doesn't re-fire on every render
  const configRef = useRef(launchConfig)
  // eslint-disable-next-line react-hooks/refs -- Intentional: same pattern as adapterRef above
  configRef.current = launchConfig

  useEffect(() => {
    const currentAdapter = adapterRef.current
    const config = configRef.current

    currentAdapter
      .launch({
        ...config,
        containerId: SDK_CONTAINER_ID
      })
      .catch((error: unknown) => {
        config.onError?.(error)
      })

    return () => {
      currentAdapter.reset()
    }
  }, [])

  return (
    <section aria-label="Document capture">
      <div
        ref={containerRef}
        id={SDK_CONTAINER_ID}
      />
    </section>
  )
}

export { SDK_CONTAINER_ID }
