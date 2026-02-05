/**
 * Home Page Unit Test (Co-located)
 *
 * Tests that the root page redirects to /login
 */
import { redirect } from 'next/navigation'
import { describe, expect, it, vi } from 'vitest'
import Home from './page'

vi.mock('next/navigation', () => ({
  redirect: vi.fn()
}))

describe('Home Page', () => {
  it('should redirect to /login', () => {
    Home()
    expect(redirect).toHaveBeenCalledWith('/login')
  })
})
