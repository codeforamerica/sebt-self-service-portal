import { http, HttpResponse } from 'msw'

export const handlers = [
  http.post('/api/enrollment/check', () =>
    HttpResponse.json({
      results: [
        {
          checkId: 'test-1',
          firstName: 'Jane',
          lastName: 'Doe',
          dateOfBirth: '2015-04-12',
          status: 'Match'
        }
      ]
    })
  ),
  http.get('/api/enrollment/schools', () =>
    HttpResponse.json([
      { name: 'Adams Elementary', code: 'AES' },
      { name: 'Baker Middle School', code: 'BMS' }
    ])
  )
]
