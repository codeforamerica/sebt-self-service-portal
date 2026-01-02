'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

interface UseCountdownReturn {
  secondsRemaining: number
  isActive: boolean
  start: (seconds: number) => void
  reset: () => void
}

/**
 * Hook for managing countdown timers.
 * Encapsulates the interval/effect logic for a countdown.
 *
 * @returns Object with secondsRemaining, isActive, start, and reset
 *
 * @example
 * const { secondsRemaining, isActive, start } = useCountdown()
 *
 * function handleResend() {
 *   await sendCode()
 *   start(30) // Start 30 second countdown
 * }
 */
export function useCountdown(): UseCountdownReturn {
  const [secondsRemaining, setSecondsRemaining] = useState(0)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const clearTimer = useCallback(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current)
      intervalRef.current = null
    }
  }, [])

  const start = useCallback(
    (seconds: number) => {
      clearTimer()
      setSecondsRemaining(seconds)

      intervalRef.current = setInterval(() => {
        setSecondsRemaining((prev) => {
          if (prev <= 1) {
            clearTimer()
            return 0
          }
          return prev - 1
        })
      }, 1000)
    },
    [clearTimer]
  )

  const reset = useCallback(() => {
    clearTimer()
    setSecondsRemaining(0)
  }, [clearTimer])

  // Cleanup on unmount
  useEffect(() => {
    return clearTimer
  }, [clearTimer])

  return {
    secondsRemaining,
    isActive: secondsRemaining > 0,
    start,
    reset
  }
}
