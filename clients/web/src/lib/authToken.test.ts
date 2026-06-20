import { describe, expect, it } from 'vitest'
import { isAccessTokenExpired } from './authToken'

function tokenWithExp(exp: number): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ exp }))
  return `${header}.${payload}.sig`
}

describe('isAccessTokenExpired', () => {
  it('returns false for a token that expires in the future', () => {
    const exp = Math.floor(Date.now() / 1000) + 3600
    expect(isAccessTokenExpired(tokenWithExp(exp), 0)).toBe(false)
  })

  it('returns true for a token that is already expired', () => {
    const exp = Math.floor(Date.now() / 1000) - 60
    expect(isAccessTokenExpired(tokenWithExp(exp), 0)).toBe(true)
  })

  it('returns true for malformed tokens', () => {
    expect(isAccessTokenExpired('not-a-jwt')).toBe(true)
  })
})
