import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { jwtDecode } from 'jwt-decode'

interface JwtClaims {
  sub?: string
}

function userIdFromToken(token: string): number | null {
  try {
    const { sub } = jwtDecode<JwtClaims>(token)
    const id = sub ? Number(sub) : NaN
    return Number.isFinite(id) ? id : null
  } catch {
    return null
  }
}

interface AuthState {
  accessToken: string | null
  userId: number | null
  isAuthenticated: boolean
  setToken: (token: string) => void
  clear: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      userId: null,
      isAuthenticated: false,
      setToken: (token) =>
        set({ accessToken: token, userId: userIdFromToken(token), isAuthenticated: true }),
      clear: () => set({ accessToken: null, userId: null, isAuthenticated: false }),
    }),
    { name: 'tangle-auth' },
  ),
)

// Non-hook accessors for use outside React (e.g. axios interceptors).
export const getAccessToken = () => useAuthStore.getState().accessToken
export const clearAuth = () => useAuthStore.getState().clear()
