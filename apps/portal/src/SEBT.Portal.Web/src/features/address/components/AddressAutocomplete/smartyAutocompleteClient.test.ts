import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'

import { server } from '@/mocks/server'

import { fetchAutocompleteSuggestions, formatSelected } from './smartyAutocompleteClient'

const SMARTY_URL = 'https://us-autocomplete-pro.api.smarty.com/lookup'

describe('fetchAutocompleteSuggestions', () => {
  it('returns parsed suggestions for a valid search', async () => {
    server.use(
      http.get(SMARTY_URL, () =>
        HttpResponse.json({
          suggestions: [
            {
              street_line: '123 Main St',
              secondary: '',
              city: 'Washington',
              state: 'DC',
              zipcode: '20001',
              entries: 0
            }
          ]
        })
      )
    )

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toHaveLength(1)
    expect(results[0]).toEqual({
      street_line: '123 Main St',
      secondary: '',
      city: 'Washington',
      state: 'DC',
      zipcode: '20001',
      entries: 0
    })
  })

  it('passes prefer_states as a query parameter', async () => {
    let capturedUrl = ''
    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json({ suggestions: [] })
      })
    )

    await fetchAutocompleteSuggestions({
      search: '123',
      key: 'test-key',
      preferStates: 'DC,VA,MD'
    })

    const url = new URL(capturedUrl)
    expect(url.searchParams.get('prefer_states')).toBe('DC,VA,MD')
  })

  it('passes selected parameter for secondary lookups', async () => {
    let capturedUrl = ''
    server.use(
      http.get(SMARTY_URL, ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json({ suggestions: [] })
      })
    )

    await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key',
      selected: '123 Main St Apt (5) Washington DC 20001'
    })

    const url = new URL(capturedUrl)
    expect(url.searchParams.get('selected')).toBe('123 Main St Apt (5) Washington DC 20001')
  })

  it('returns empty array when API responds with non-OK status', async () => {
    server.use(http.get(SMARTY_URL, () => new HttpResponse(null, { status: 500 })))

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toEqual([])
  })

  it('returns empty array on network error', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.error()))

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toEqual([])
  })

  it('returns empty array when response has no suggestions field', async () => {
    server.use(http.get(SMARTY_URL, () => HttpResponse.json({})))

    const results = await fetchAutocompleteSuggestions({
      search: '123 Main',
      key: 'test-key'
    })

    expect(results).toEqual([])
  })

  it('supports AbortController cancellation', async () => {
    server.use(
      http.get(SMARTY_URL, async () => {
        await new Promise((resolve) => setTimeout(resolve, 1000))
        return HttpResponse.json({ suggestions: [] })
      })
    )

    const controller = new AbortController()
    const promise = fetchAutocompleteSuggestions(
      { search: '123 Main', key: 'test-key' },
      controller.signal
    )
    controller.abort()

    const results = await promise
    expect(results).toEqual([])
  })
})

describe('formatSelected', () => {
  it('formats a suggestion with secondary into the selected parameter', () => {
    const result = formatSelected({
      street_line: '123 Main St',
      secondary: 'Apt',
      city: 'Washington',
      state: 'DC',
      zipcode: '20001',
      entries: 5
    })

    expect(result).toBe('123 Main St Apt (5) Washington DC 20001')
  })

  it('formats a suggestion without secondary', () => {
    const result = formatSelected({
      street_line: '456 Oak Ave',
      secondary: '',
      city: 'Denver',
      state: 'CO',
      zipcode: '80202',
      entries: 3
    })

    expect(result).toBe('456 Oak Ave (3) Denver CO 80202')
  })
})
