import { describe, expect, it, beforeEach } from 'vitest'
import { getCurrentUserId, useAuthStore } from './authStore'

function tokenWithSub(sub: string): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub }))
  return `${header}.${payload}.sig`
}

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.setState({
      accessToken: null,
      userId: null,
      isAuthenticated: false,
    })
  })

  it('setToken decodes userId from JWT sub', () => {
    useAuthStore.getState().setToken(tokenWithSub('42'))

    expect(useAuthStore.getState().userId).toBe(42)
    expect(useAuthStore.getState().isAuthenticated).toBe(true)
  })

  it('getCurrentUserId decodes from token when userId missing', () => {
    useAuthStore.setState({
      accessToken: tokenWithSub('7'),
      userId: null,
      isAuthenticated: true,
    })

    expect(getCurrentUserId()).toBe(7)
  })

  it('clear resets session fields', () => {
    useAuthStore.getState().setToken(tokenWithSub('1'))
    useAuthStore.getState().clear()

    expect(useAuthStore.getState().isAuthenticated).toBe(false)
    expect(getCurrentUserId()).toBeNull()
  })
})
