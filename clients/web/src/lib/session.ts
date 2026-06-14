import { queryClient } from '@/lib/queryClient'
import { clearAuth } from '@/stores/authStore'

/** Drop persisted auth and all cached server state (logout or session expiry). */
export function clearSession() {
  clearAuth()
  queryClient.clear()
}
