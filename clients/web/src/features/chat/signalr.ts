import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { getAccessToken } from '@/stores/authStore'
import { normalizeChatMessage } from './normalize'
import type { ChatMessage } from './api'

export const MESSAGE_CREATED_EVENT = 'MessageCreated'
export const MESSAGE_EDITED_EVENT = 'MessageEdited'
export const MESSAGE_DELETED_EVENT = 'MessageDeleted'

// Single shared hub connection for the whole app. The server copies the
// access_token query param into the bearer handler for /hubs paths.
let connection: HubConnection | null = null
let startPromise: Promise<void> | null = null
let handlersInstalled = false

const roomListeners = new Map<number, Set<(message: ChatMessage) => void>>()
const joinedRooms = new Set<number>()

function getConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl('/hubs/chat', {
        accessTokenFactory: () => getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    installConnectionHandlers(connection)
  }
  return connection
}

function dispatchMessage(raw: unknown) {
  const message = normalizeChatMessage(raw)
  if (!Number.isFinite(message.id) || !Number.isFinite(message.chatRoomId)) return

  roomListeners.get(message.chatRoomId)?.forEach((listener) => listener(message))
}

function installConnectionHandlers(conn: HubConnection) {
  if (handlersInstalled) return
  handlersInstalled = true

  conn.on(MESSAGE_CREATED_EVENT, dispatchMessage)
  conn.on(MESSAGE_EDITED_EVENT, dispatchMessage)
  conn.on(MESSAGE_DELETED_EVENT, dispatchMessage)

  // Automatic reconnect drops SignalR group membership; re-join every room
  // we still have active listeners for.
  conn.onreconnected(async () => {
    await rejoinAllRooms(conn)
  })
}

async function rejoinAllRooms(conn: HubConnection) {
  for (const roomId of joinedRooms) {
    try {
      await conn.invoke('JoinRoom', roomId)
    } catch {
      // Room access may have changed while disconnected.
    }
  }
}

async function joinRoom(conn: HubConnection, roomId: number) {
  if (joinedRooms.has(roomId)) return
  await conn.invoke('JoinRoom', roomId)
  joinedRooms.add(roomId)
}

async function leaveRoom(conn: HubConnection, roomId: number) {
  if (!joinedRooms.has(roomId)) return
  joinedRooms.delete(roomId)
  if (conn.state === HubConnectionState.Connected) {
    try {
      await conn.invoke('LeaveRoom', roomId)
    } catch {
      // Best-effort leave when unsubscribing.
    }
  }
}

export async function disconnectChatHub(): Promise<void> {
  if (startPromise) {
    await startPromise.catch(() => {})
  }
  roomListeners.clear()
  joinedRooms.clear()
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

export async function ensureConnected(): Promise<HubConnection> {
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

// Join a room's broadcast group and stream message updates until cleanup runs.
export async function subscribeToRoom(
  roomId: number,
  onMessage: (message: ChatMessage) => void,
): Promise<() => void> {
  const conn = await ensureConnected()

  let listeners = roomListeners.get(roomId)
  if (!listeners) {
    listeners = new Set()
    roomListeners.set(roomId, listeners)
  }
  listeners.add(onMessage)

  await joinRoom(conn, roomId)

  return () => {
    const set = roomListeners.get(roomId)
    set?.delete(onMessage)
    if (set?.size === 0) {
      roomListeners.delete(roomId)
      void leaveRoom(conn, roomId)
    }
  }
}
