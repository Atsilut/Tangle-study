import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { getAccessToken } from '@/stores/authStore'
import type { LiveLocation } from './api'

export const LOCATION_UPDATED_EVENT = 'LocationUpdated'

let connection: HubConnection | null = null
let startPromise: Promise<void> | null = null
let handlersInstalled = false

const sessionListeners = new Map<number, Set<(location: LiveLocation) => void>>()
const joinedSessions = new Set<number>()

function getConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl('/hubs/location', {
        accessTokenFactory: () => getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    installConnectionHandlers(connection)
  }
  return connection
}

function normalizeLiveLocation(raw: unknown): LiveLocation | null {
  if (!raw || typeof raw !== 'object') return null
  const value = raw as Record<string, unknown>
  const sessionId = Number(value.sessionId)
  const groupId = Number(value.groupId)
  const userId = Number(value.userId)
  const latitude = Number(value.latitude)
  const longitude = Number(value.longitude)
  const userNickname = typeof value.userNickname === 'string' ? value.userNickname : ''
  const updatedAt = typeof value.updatedAt === 'string' ? value.updatedAt : new Date().toISOString()

  if (
    !Number.isFinite(sessionId) ||
    !Number.isFinite(groupId) ||
    !Number.isFinite(userId) ||
    !Number.isFinite(latitude) ||
    !Number.isFinite(longitude)
  ) {
    return null
  }

  return { sessionId, groupId, userId, userNickname, latitude, longitude, updatedAt }
}

function dispatchLocation(raw: unknown) {
  const location = normalizeLiveLocation(raw)
  if (!location) return
  sessionListeners.get(location.sessionId)?.forEach((listener) => listener(location))
}

function installConnectionHandlers(conn: HubConnection) {
  if (handlersInstalled) return
  handlersInstalled = true

  conn.on(LOCATION_UPDATED_EVENT, dispatchLocation)

  conn.onreconnected(async () => {
    await rejoinAllSessions(conn)
  })
}

async function rejoinAllSessions(conn: HubConnection) {
  for (const sessionId of joinedSessions) {
    try {
      await conn.invoke('JoinSession', sessionId)
    } catch {
      // Session access may have changed while disconnected.
    }
  }
}

async function joinSession(conn: HubConnection, sessionId: number) {
  if (joinedSessions.has(sessionId)) return
  await conn.invoke('JoinSession', sessionId)
  joinedSessions.add(sessionId)
}

async function leaveSession(conn: HubConnection, sessionId: number) {
  if (!joinedSessions.has(sessionId)) return
  joinedSessions.delete(sessionId)
  if (conn.state === HubConnectionState.Connected) {
    try {
      await conn.invoke('LeaveSession', sessionId)
    } catch {
      // Best-effort leave when unsubscribing.
    }
  }
}

export async function ensureLocationConnected(): Promise<HubConnection> {
  const conn = getConnection()
  if (conn.state === HubConnectionState.Connected) return conn
  if (!startPromise) {
    startPromise = conn.start().finally(() => {
      startPromise = null
    })
  }
  await startPromise
  return conn
}

export async function subscribeToLocationSession(
  sessionId: number,
  onLocation: (location: LiveLocation) => void,
): Promise<() => void> {
  const conn = await ensureLocationConnected()

  let listeners = sessionListeners.get(sessionId)
  if (!listeners) {
    listeners = new Set()
    sessionListeners.set(sessionId, listeners)
  }
  listeners.add(onLocation)

  await joinSession(conn, sessionId)

  return () => {
    const set = sessionListeners.get(sessionId)
    set?.delete(onLocation)
    if (set?.size === 0) {
      sessionListeners.delete(sessionId)
      void leaveSession(conn, sessionId)
    }
  }
}
