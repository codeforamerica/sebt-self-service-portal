'use client'

import { useEffect, useRef } from 'react'

const ACTIVITY_EVENTS = ['mousedown', 'keydown', 'touchstart', 'scroll'] as const

// Resets the timer when the tab returns to the foreground but not when it
// hides — a backgrounded tab should still time out.
export function useIdleTimeout(onTimeout: () => void, timeoutMs: number): void {
  const onTimeoutRef = useRef(onTimeout)
  useEffect(() => {
    onTimeoutRef.current = onTimeout
  }, [onTimeout])

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>

    function resetTimer() {
      clearTimeout(timer)
      timer = setTimeout(() => onTimeoutRef.current(), timeoutMs)
    }

    function handleActivity() {
      if (document.visibilityState !== 'hidden') resetTimer()
    }

    resetTimer()

    for (const event of ACTIVITY_EVENTS) {
      window.addEventListener(event, handleActivity, { passive: true })
    }
    document.addEventListener('visibilitychange', handleActivity)

    return () => {
      clearTimeout(timer)
      for (const event of ACTIVITY_EVENTS) {
        window.removeEventListener(event, handleActivity)
      }
      document.removeEventListener('visibilitychange', handleActivity)
    }
  }, [timeoutMs])
}
