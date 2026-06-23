import { queryClient } from '@/lib/queryClient'
import { stopLocationSession } from './api'
import { locationKeys } from './hooks'

export interface ActiveLocationSession {
  sessionId: number
  groupId: number
}

let activeSession: ActiveLocationSession | null = null

export function registerActiveLocationSession(sessionId: number, groupId: number): void {
  activeSession = { sessionId, groupId }
}

export function clearActiveLocationSession(): void {
  activeSession = null
}

export function getActiveLocationSession(): ActiveLocationSession | null {
  return activeSession
}

/** Best-effort stop for logout, navigation, and group switches. */
export async function stopActiveLocationSession(): Promise<void> {
  const session = activeSession
  if (session == null) return

  activeSession = null
  try {
    await stopLocationSession(session.sessionId)
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: locationKeys.mySession(session.groupId) }),
      queryClient.invalidateQueries({ queryKey: locationKeys.activeGroup(session.groupId) }),
      queryClient.invalidateQueries({ queryKey: locationKeys.memberStatus(session.groupId) }),
    ])
  } catch {
    // Token may already be invalid during logout/expiry; server session will age out.
  }
}
