import { afterEach, describe, expect, it, vi } from 'vitest'
import { resolveMaintenanceMessage } from './resolveMaintenanceMessage'

const EN = 'The checker may be unavailable due to maintenance.'
const ES = 'El verificador puede no estar disponible debido a mantenimiento.'

describe('resolveMaintenanceMessage', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('returns null when the banner is disabled', () => {
    expect(resolveMaintenanceMessage(false, { en: EN, es: ES }, 'en')).toBeNull()
  })

  it('returns the message for the active language', () => {
    expect(resolveMaintenanceMessage(true, { en: EN, es: ES }, 'es')).toBe(ES)
  })

  it('matches regional language variants to their base language', () => {
    expect(resolveMaintenanceMessage(true, { en: EN, es: ES }, 'es-US')).toBe(ES)
  })

  it('matches config keys case-insensitively', () => {
    expect(resolveMaintenanceMessage(true, { En: EN, Es: ES }, 'es')).toBe(ES)
  })

  it('falls back to English when the active language has no message', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    expect(resolveMaintenanceMessage(true, { en: EN }, 'es')).toBe(EN)
    expect(warn).toHaveBeenCalledOnce()
  })

  it('defaults to English when the language is undefined', () => {
    expect(resolveMaintenanceMessage(true, { en: EN, es: ES }, undefined)).toBe(EN)
  })

  it('returns null and warns when no usable message exists', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    expect(resolveMaintenanceMessage(true, {}, 'en')).toBeNull()
    expect(warn).toHaveBeenCalledOnce()
  })

  it('treats whitespace-only messages as missing', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    expect(resolveMaintenanceMessage(true, { en: '   ', es: ES }, 'en')).toBeNull()
    expect(warn).toHaveBeenCalledOnce()
  })
})
