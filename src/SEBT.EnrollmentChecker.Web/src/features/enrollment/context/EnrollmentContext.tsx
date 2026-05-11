'use client'

import { useRouter } from 'next/navigation'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { v4 as uuidv4 } from 'uuid'
import { z } from 'zod'
import { toDateOfBirth } from '../schemas/childSchema'
import type { ChildFormValues } from '../schemas/childSchema'
import { useIdleTimeout } from './useIdleTimeout'

// Must match the backend's EnrollmentCheckApiRequest.MaxChildren
export const MAX_CHILDREN = 20

// Auto-clear stored child info after inactivity to limit exposure on shared devices.
export const IDLE_TIMEOUT_MS = 15 * 60 * 1000

// ── Types ──────────────────────────────────────────────────────────────────

export interface Child {
  id: string
  firstName: string
  middleName?: string
  lastName: string
  dateOfBirth: string  // ISO date: YYYY-MM-DD
  schoolName?: string
  schoolCode?: string
}

interface EnrollmentState {
  children: Child[]
  editingChildId: string | null
}

interface EnrollmentActions {
  addChild: (values: ChildFormValues) => void
  updateChild: (id: string, values: ChildFormValues) => void
  removeChild: (id: string) => void
  setEditingChildId: (id: string | null) => void
  clearState: () => void
}

interface EnrollmentContextValue {
  state: EnrollmentState
  actions: EnrollmentActions
}

// Exposes state as a nested object plus actions flat on the return value
interface UseEnrollmentReturn extends EnrollmentActions {
  state: EnrollmentState
}

// ── Context ────────────────────────────────────────────────────────────────

const EnrollmentContext = createContext<EnrollmentContextValue | null>(null)

const STORAGE_KEY = 'enrollmentState'

const childStorageSchema = z.object({
  id: z.string(),
  firstName: z.string(),
  middleName: z.string().optional(),
  lastName: z.string(),
  dateOfBirth: z.string(),
  schoolName: z.string().optional(),
  schoolCode: z.string().optional()
})
const enrollmentStorageSchema = z.object({
  children: z.array(childStorageSchema),
  editingChildId: z.string().nullable()
})

const initialState: EnrollmentState = {
  children: [],
  editingChildId: null
}

function loadFromStorage(): EnrollmentState {
  if (typeof window === 'undefined') return initialState
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return initialState
    const parsed: unknown = JSON.parse(raw)
    const result = enrollmentStorageSchema.safeParse(parsed)
    if (!result.success) return initialState
    return {
      editingChildId: result.data.editingChildId,
      children: result.data.children.map(parsedChild => {
        const child: Child = {
          id: parsedChild.id,
          firstName: parsedChild.firstName,
          lastName: parsedChild.lastName,
          dateOfBirth: parsedChild.dateOfBirth,
          ...(parsedChild.middleName && { middleName: parsedChild.middleName }),
          ...(parsedChild.schoolName && { schoolName: parsedChild.schoolName }),
          ...(parsedChild.schoolCode && { schoolCode: parsedChild.schoolCode })
        }
        return child
      })
    }
  } catch {
    return initialState
  }
}

function saveToStorage(state: EnrollmentState): void {
  if (typeof window === 'undefined') return
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export function EnrollmentProvider({ children }: { children: ReactNode }) {
  const router = useRouter()
  const [state, setState] = useState<EnrollmentState>(initialState)

  // Hydrate from sessionStorage after mount (avoids SSR mismatch)
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setState(loadFromStorage())
  }, [])

  function update(updater: (prev: EnrollmentState) => EnrollmentState) {
    setState(prev => {
      const next = updater(prev)
      saveToStorage(next)
      return next
    })
  }

  function clearState() {
    if (typeof window !== 'undefined') sessionStorage.removeItem(STORAGE_KEY)
    setState(initialState)
  }

  useIdleTimeout(() => {
    clearState()
    router.push('/')
  }, IDLE_TIMEOUT_MS)

  const actions: EnrollmentActions = {
    addChild: (values) => update(s => {
      if (s.children.length >= MAX_CHILDREN) return s
      const child: Child = {
        id: uuidv4(),
        firstName: values.firstName,
        lastName: values.lastName,
        dateOfBirth: toDateOfBirth(values),
        ...(values.middleName && { middleName: values.middleName }),
        ...(values.schoolName && { schoolName: values.schoolName }),
        ...(values.schoolCode && { schoolCode: values.schoolCode })
      }
      return { ...s, children: [...s.children, child] }
    }),
    updateChild: (id, values) => update(s => ({
      ...s,
      children: s.children.map(child => {
        if (child.id !== id) return child
        const updated: Child = {
          id,
          firstName: values.firstName,
          lastName: values.lastName,
          dateOfBirth: toDateOfBirth(values),
          ...(values.middleName && { middleName: values.middleName }),
          ...(values.schoolName && { schoolName: values.schoolName }),
          ...(values.schoolCode && { schoolCode: values.schoolCode })
        }
        return updated
      })
    })),
    removeChild: (id) => update(s => ({
      ...s,
      children: s.children.filter(child => child.id !== id)
    })),
    setEditingChildId: (id) => update(s => ({ ...s, editingChildId: id })),
    clearState
  }

  return (
    <EnrollmentContext.Provider value={{ state, actions }}>
      {children}
    </EnrollmentContext.Provider>
  )
}

export function useEnrollment(): UseEnrollmentReturn {
  const ctx = useContext(EnrollmentContext)
  if (!ctx) throw new Error('useEnrollment must be used within EnrollmentProvider')
  const { state, actions } = ctx
  return { state, ...actions }
}
