import { queryClient } from '@/lib/queryClient'
import { claimSessionExpiryHandling, resetSessionExpiryGuard } from '@/lib/sessionExpiry'
import { disconnectChatHub } from '@/features/chat/signalr'
import { stopActiveLocationSession } from '@/features/location/liveSharingRegistry'
import { disconnectLocationHub } from '@/features/location/signalr'
import { clearAuth } from '@/stores/authStore'

function stopRealtimeConnections() {
  void disconnectLocationHub()
  void disconnectChatHub()
}

function finishSessionTeardown() {
  clearAuth()
  queryClient.clear()
  stopRealtimeConnections()
}

/** Drop persisted auth and all cached server state (logout). */
export function clearSession() {
  resetSessionExpiryGuard()
  void queryClient.cancelQueries()
  void stopActiveLocationSession().finally(finishSessionTeardown)
}

/** Session expired or unauthorized; redirect once and tear down in-flight work. */
export function handleSessionExpired() {
  if (!claimSessionExpiryHandling()) return

  void queryClient.cancelQueries()
  void stopActiveLocationSession().finally(() => {
    finishSessionTeardown()

    const path = window.location.pathname
    if (path !== '/login' && path !== '/register') {
      window.location.assign('/login')
    }
  })
}
