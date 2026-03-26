'use client'

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { v4 as uuidv4 } from 'uuid'
import { z } from 'zod'
import { toDateOfBirth } from '../schemas/childSchema'
import type { ChildFormValues } from '../schemas/childSchema'

// Must match the backend's EnrollmentCheckApiRequest.MaxChildren
export const MAX_CHILDREN = 20

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
    return result.success ? result.data : initialState
  } catch {
    return initialState
  }
}

function saveToStorage(state: EnrollmentState): void {
  if (typeof window === 'undefined') return
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export function EnrollmentProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<EnrollmentState>(initialState)

  // Hydrate from sessionStorage after mount (avoids SSR mismatch)
  useEffect(() => {
    setState(loadFromStorage())
  }, [])

  function update(updater: (prev: EnrollmentState) => EnrollmentState) {
    setState(prev => {
      const next = updater(prev)
      saveToStorage(next)
      return next
    })
  }

  const actions: EnrollmentActions = {
    addChild: (values) => update(s => {
      if (s.children.length >= MAX_CHILDREN) return s
      return {
      ...s,
      children: [...s.children, {
        id: uuidv4(),
        firstName: values.firstName,
        middleName: values.middleName,
        lastName: values.lastName,
        dateOfBirth: toDateOfBirth(values),
        schoolName: values.schoolName,
        schoolCode: values.schoolCode
      }]
    }}),
    updateChild: (id, values) => update(s => ({
      ...s,
      children: s.children.map(c => c.id === id ? {
        id,
        firstName: values.firstName,
        middleName: values.middleName,
        lastName: values.lastName,
        dateOfBirth: toDateOfBirth(values),
        schoolName: values.schoolName,
        schoolCode: values.schoolCode
      } : c)
    })),
    removeChild: (id) => update(s => ({
      ...s,
      children: s.children.filter(c => c.id !== id)
    })),
    setEditingChildId: (id) => update(s => ({ ...s, editingChildId: id })),
    clearState: () => {
      if (typeof window !== 'undefined') sessionStorage.removeItem(STORAGE_KEY)
      setState(initialState)
    }
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
