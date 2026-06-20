import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { jwtDecode } from 'jwt-decode'
import { isAccessTokenExpired } from '@/lib/authToken'
import { resetSessionExpiryGuard } from '@/lib/sessionExpiry'

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

/** Prefer store userId; decode JWT when persist omitted it. */
export function getCurrentUserId(): number | null {
  const { userId, accessToken } = useAuthStore.getState()
  if (userId != null) return userId
  if (accessToken) return userIdFromToken(accessToken)
  return null
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
      setToken: (token) => {
        resetSessionExpiryGuard()
        set({ accessToken: token, userId: userIdFromToken(token), isAuthenticated: true })
      },
      clear: () => set({ accessToken: null, userId: null, isAuthenticated: false }),
    }),
    {
      name: 'tangle-auth',
      onRehydrateStorage: () => (state) => {
        if (!state?.accessToken) return
        if (isAccessTokenExpired(state.accessToken)) {
          state.accessToken = null
          state.userId = null
          state.isAuthenticated = false
          return
        }
        if (state.userId == null) {
          state.userId = userIdFromToken(state.accessToken)
        }
      },
    },
  ),
)

// Non-hook accessors for use outside React (e.g. axios interceptors).
export const getAccessToken = () => useAuthStore.getState().accessToken
export const clearAuth = () => useAuthStore.getState().clear()
