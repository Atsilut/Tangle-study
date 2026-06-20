import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { getAccessToken } from '@/stores/authStore'
import type { LiveLocation, LocationSafetyAlert, LocationSafetyAlertType } from './api'

export const LOCATION_UPDATED_EVENT = 'LocationUpdated'
export const LOCATION_SESSION_ENDED_EVENT = 'LocationSessionEnded'
export const SAFETY_ALERT_RAISED_EVENT = 'SafetyAlertRaised'

let connection: HubConnection | null = null
let startPromise: Promise<void> | null = null
let handlersInstalled = false

const sessionListeners = new Map<number, Set<(location: LiveLocation) => void>>()
const sessionEndedListeners = new Map<number, Set<() => void>>()
const groupAlertListeners = new Map<number, Set<(alert: LocationSafetyAlert) => void>>()
const joinedSessions = new Set<number>()
const joinedGroupAlerts = new Set<number>()

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

function normalizeSafetyAlert(raw: unknown): LocationSafetyAlert | null {
  if (!raw || typeof raw !== 'object') return null
  const value = raw as Record<string, unknown>
  const type = value.type
  const groupId = Number(value.groupId)
  const sessionId = Number(value.sessionId)
  const userId = Number(value.userId)
  const userNickname = typeof value.userNickname === 'string' ? value.userNickname : ''
  const occurredAt = typeof value.occurredAt === 'string' ? value.occurredAt : new Date().toISOString()
  const message = typeof value.message === 'string' ? value.message : ''
  const latitude = value.latitude == null ? null : Number(value.latitude)
  const longitude = value.longitude == null ? null : Number(value.longitude)

  if (
    (type !== 'StalePosition' && type !== 'Sos') ||
    !Number.isFinite(groupId) ||
    !Number.isFinite(sessionId) ||
    !Number.isFinite(userId)
  ) {
    return null
  }

  return {
    type: type as LocationSafetyAlertType,
    groupId,
    sessionId,
    userId,
    userNickname,
    latitude: latitude != null && Number.isFinite(latitude) ? latitude : null,
    longitude: longitude != null && Number.isFinite(longitude) ? longitude : null,
    occurredAt,
    message,
  }
}

function normalizeSessionEnded(raw: unknown): Pick<LiveLocation, 'sessionId' | 'groupId' | 'userId'> | null {
  if (!raw || typeof raw !== 'object') return null
  const value = raw as Record<string, unknown>
  const sessionId = Number(value.sessionId)
  const groupId = Number(value.groupId)
  const userId = Number(value.userId)

  if (!Number.isFinite(sessionId) || !Number.isFinite(groupId) || !Number.isFinite(userId)) return null

  return { sessionId, groupId, userId }
}

function dispatchLocation(raw: unknown) {
  const location = normalizeLiveLocation(raw)
  if (!location) return
  sessionListeners.get(location.sessionId)?.forEach((listener) => listener(location))
}

function dispatchSafetyAlert(raw: unknown) {
  const alert = normalizeSafetyAlert(raw)
  if (!alert) return
  groupAlertListeners.get(alert.groupId)?.forEach((listener) => listener(alert))
}

function dispatchSessionEnded(raw: unknown) {
  const ended = normalizeSessionEnded(raw)
  if (!ended) return
  sessionEndedListeners.get(ended.sessionId)?.forEach((listener) => listener())
}

function installConnectionHandlers(conn: HubConnection) {
  if (handlersInstalled) return
  handlersInstalled = true

  conn.on(LOCATION_UPDATED_EVENT, dispatchLocation)
  conn.on(LOCATION_SESSION_ENDED_EVENT, dispatchSessionEnded)
  conn.on(SAFETY_ALERT_RAISED_EVENT, dispatchSafetyAlert)

  conn.onreconnected(async () => {
    await rejoinAllSessions(conn)
    await rejoinAllGroupAlerts(conn)
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

async function rejoinAllGroupAlerts(conn: HubConnection) {
  for (const groupId of joinedGroupAlerts) {
    try {
      await conn.invoke('JoinGroupAlerts', groupId)
    } catch {
      // Group access may have changed while disconnected.
    }
  }
}

async function joinGroupAlerts(conn: HubConnection, groupId: number) {
  if (joinedGroupAlerts.has(groupId)) return
  await conn.invoke('JoinGroupAlerts', groupId)
  joinedGroupAlerts.add(groupId)
}

async function leaveGroupAlerts(conn: HubConnection, groupId: number) {
  if (!joinedGroupAlerts.has(groupId)) return
  joinedGroupAlerts.delete(groupId)
  if (conn.state === HubConnectionState.Connected) {
    try {
      await conn.invoke('LeaveGroupAlerts', groupId)
    } catch {
      // Best-effort leave when unsubscribing.
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

export async function disconnectLocationHub(): Promise<void> {
  if (startPromise) {
    await startPromise.catch(() => {})
  }
  sessionListeners.clear()
  sessionEndedListeners.clear()
  groupAlertListeners.clear()
  joinedSessions.clear()
  joinedGroupAlerts.clear()
  if (connection) {
    try {
      await connection.stop()
    } catch {
      // Hub may already be stopped.
    }
    connection = null
  }
  handlersInstalled = false
  startPromise = null
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
  onEnded?: () => void,
): Promise<() => void> {
  const conn = await ensureLocationConnected()

  let listeners = sessionListeners.get(sessionId)
  if (!listeners) {
    listeners = new Set()
    sessionListeners.set(sessionId, listeners)
  }
  listeners.add(onLocation)

  if (onEnded) {
    let endedListeners = sessionEndedListeners.get(sessionId)
    if (!endedListeners) {
      endedListeners = new Set()
      sessionEndedListeners.set(sessionId, endedListeners)
    }
    endedListeners.add(onEnded)
  }

  await joinSession(conn, sessionId)

  return () => {
    const set = sessionListeners.get(sessionId)
    set?.delete(onLocation)
    if (set?.size === 0) {
      sessionListeners.delete(sessionId)
      void leaveSession(conn, sessionId)
    }

    if (onEnded) {
      const endedSet = sessionEndedListeners.get(sessionId)
      endedSet?.delete(onEnded)
      if (endedSet?.size === 0) sessionEndedListeners.delete(sessionId)
    }
  }
}

export async function subscribeToGroupSafetyAlerts(
  groupId: number,
  onAlert: (alert: LocationSafetyAlert) => void,
): Promise<() => void> {
  const conn = await ensureLocationConnected()

  let listeners = groupAlertListeners.get(groupId)
  if (!listeners) {
    listeners = new Set()
    groupAlertListeners.set(groupId, listeners)
  }
  listeners.add(onAlert)

  await joinGroupAlerts(conn, groupId)

  return () => {
    const set = groupAlertListeners.get(groupId)
    set?.delete(onAlert)
    if (set?.size === 0) {
      groupAlertListeners.delete(groupId)
      void leaveGroupAlerts(conn, groupId)
    }
  }
}
