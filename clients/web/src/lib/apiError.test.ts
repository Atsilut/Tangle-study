import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { getErrorMessage } from './apiError'

describe('getErrorMessage', () => {
  it('returns ProblemDetails detail', () => {
    const error = new AxiosError('Request failed')
    error.response = {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { title: 'Bad Request', detail: 'Nickname is taken.' },
    }

    expect(getErrorMessage(error)).toBe('Nickname is taken.')
  })

  it('returns first validation error from errors map', () => {
    const error = new AxiosError('Request failed')
    error.response = {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { errors: { Name: ['Name is required.'] } },
    }

    expect(getErrorMessage(error)).toBe('Name is required.')
  })

  it('returns plain string response body', () => {
    const error = new AxiosError('Request failed')
    error.response = {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: 'Invalid invitation.',
    }

    expect(getErrorMessage(error)).toBe('Invalid invitation.')
  })

  it('falls back for generic Error', () => {
    expect(getErrorMessage(new Error('network down'), 'fallback')).toBe('network down')
  })

  it('uses fallback for unknown values', () => {
    expect(getErrorMessage(null, 'fallback')).toBe('fallback')
  })
})
