import axios, { type AxiosRequestConfig } from 'axios'
import { clearAuth, getAccessToken } from '@/stores/authStore'

declare module 'axios' {
  interface AxiosRequestConfig {
    /** Business-rule 401 (e.g. privacy); do not clear session or redirect to login. */
    treatUnauthorizedAsForbidden?: boolean
  }
}

// All REST endpoints live under /api; the dev proxy / Nginx make this
// same-origin. SignalR (/hubs) is handled separately in the chat feature.
export const api = axios.create({
  baseURL: '/api',
})

api.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    // JWT expires after ~15 min with no refresh token: drop session and
    // bounce to login. Skip if already on the login/register pages.
    if (error.response?.status === 401 && !error.config?.treatUnauthorizedAsForbidden) {
      clearAuth()
      const path = window.location.pathname
      if (path !== '/login' && path !== '/register') {
        window.location.assign('/login')
      }
    }
    return Promise.reject(error)
  },
)

// Many list endpoints return 204 No Content when empty. Normalize those to [].
export async function getList<T>(url: string, config?: AxiosRequestConfig): Promise<T[]> {
  const res = await api.get<T[] | ''>(url, config)
  if (res.status === 204 || !res.data) return []
  return res.data as T[]
}
