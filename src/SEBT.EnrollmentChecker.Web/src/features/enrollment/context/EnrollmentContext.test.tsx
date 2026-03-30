import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { EnrollmentProvider, useEnrollment } from './EnrollmentContext'

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <EnrollmentProvider>{children}</EnrollmentProvider>
)

const child = {
  firstName: 'Jane',
  lastName: 'Doe',
  month: '4',
  day: '12',
  year: '2015'
}

describe('EnrollmentContext', () => {
  beforeEach(() => sessionStorage.clear())
  afterEach(() => sessionStorage.clear())

  it('starts with no children', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    expect(result.current.state.children).toHaveLength(0)
    expect(result.current.state.editingChildId).toBeNull()
  })

  it('adds a child', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    expect(result.current.state.children).toHaveLength(1)
    expect(result.current.state.children[0]?.firstName).toBe('Jane')
  })

  it('generates a unique id for each child', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => {
      result.current.addChild(child)
      result.current.addChild({ ...child, firstName: 'John' })
    })
    const ids = result.current.state.children.map(c => c.id)
    expect(new Set(ids).size).toBe(2)
  })

  it('removes a child by id', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    const id = result.current.state.children[0]!.id
    act(() => result.current.removeChild(id))
    expect(result.current.state.children).toHaveLength(0)
  })

  it('edits a child', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    const id = result.current.state.children[0]!.id
    act(() => result.current.updateChild(id, { ...child, firstName: 'Janet' }))
    expect(result.current.state.children[0]?.firstName).toBe('Janet')
  })

  it('persists to sessionStorage and restores on mount', () => {
    const { result, unmount } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    unmount()

    const { result: result2 } = renderHook(() => useEnrollment(), { wrapper })
    expect(result2.current.state.children).toHaveLength(1)
    expect(result2.current.state.children[0]?.firstName).toBe('Jane')
  })

  it('clearState removes children and sessionStorage', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    act(() => result.current.clearState())
    expect(result.current.state.children).toHaveLength(0)
    expect(sessionStorage.getItem('enrollmentState')).toBeNull()
  })

  it('setEditingChildId updates editingChildId', () => {
    const { result } = renderHook(() => useEnrollment(), { wrapper })
    act(() => result.current.addChild(child))
    const id = result.current.state.children[0]!.id
    act(() => result.current.setEditingChildId(id))
    expect(result.current.state.editingChildId).toBe(id)
    act(() => result.current.setEditingChildId(null))
    expect(result.current.state.editingChildId).toBeNull()
  })
})
