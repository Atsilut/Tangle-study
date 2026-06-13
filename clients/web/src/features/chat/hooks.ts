import { useCallback, useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getErrorMessage } from '@/lib/apiError'
import { getCurrentUserId, useAuthStore } from '@/stores/authStore'
import {
  createGroupRoom,
  createMultiRoom,
  deleteChatMessage,
  getGroupRooms,
  getMessages,
  getMyRooms,
  getOrCreateDirectRoom,
  getRoom,
  leaveRoom,
  markChatMessagesSeen,
  patchChatMessage,
  sendMessage,
  type ChatMessage,
} from './api'
import { subscribeToRoom } from './signalr'

export const chatKeys = {
  all: ['chat'] as const,
  rooms: () => [...chatKeys.all, 'rooms'] as const,
  room: (roomId: number) => [...chatKeys.all, 'room', roomId] as const,
  groupRooms: (groupId: number) => [...chatKeys.all, 'groupRooms', groupId] as const,
}

export function useMyRooms() {
  return useQuery({ queryKey: chatKeys.rooms(), queryFn: getMyRooms })
}

// Join every listed room over SignalR so the inbox re-sorts when new messages
// arrive, even while the user is not inside a conversation.
export function useChatRoomsRealtimeSync(roomIds: number[]) {
  const queryClient = useQueryClient()
  const roomKey = roomIds.length > 0 ? roomIds.join(',') : ''

  useEffect(() => {
    if (roomKey === '') return
    // Derive ids from the stable key so the effect only re-runs when the set
    // of rooms actually changes (not on every array identity change).
    const ids = roomKey.split(',').map(Number)
    let active = true
    const cleanups: Array<() => void> = []

    const connect = (attempt: number) => {
      Promise.all(
        ids.map((id) =>
          subscribeToRoom(id, () => {
            queryClient.invalidateQueries({ queryKey: chatKeys.rooms() })
          }),
        ),
      )
        .then((fns) => {
          if (!active) {
            fns.forEach((fn) => fn())
            return
          }
          cleanups.push(...fns)
        })
        .catch(() => {
          if (!active || attempt >= 3) return
          window.setTimeout(() => connect(attempt + 1), 1000 * attempt)
        })
    }
    connect(1)

    return () => {
      active = false
      cleanups.forEach((fn) => fn())
    }
  }, [roomKey, queryClient])
}

export function useRoom(roomId: number | null) {
  return useQuery({
    queryKey: chatKeys.room(roomId ?? -1),
    queryFn: () => getRoom(roomId as number),
    enabled: roomId != null,
  })
}

export function useGetOrCreateDirectRoom() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (otherUserId: number) => getOrCreateDirectRoom(otherUserId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chatKeys.rooms() }),
  })
}

export function useCreateMultiRoom() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userIds, title }: { userIds: number[]; title?: string }) =>
      createMultiRoom(userIds, title),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chatKeys.rooms() }),
  })
}

export function useLeaveRoom() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (roomId: number) => leaveRoom(roomId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chatKeys.rooms() }),
  })
}

export function useGroupRooms(groupId: number | null) {
  return useQuery({
    queryKey: chatKeys.groupRooms(groupId ?? -1),
    queryFn: () => getGroupRooms(groupId as number),
    enabled: groupId != null,
  })
}

export function useCreateGroupRoom(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userIds, title }: { userIds: number[]; title?: string }) =>
      createGroupRoom(groupId, userIds, title),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: chatKeys.groupRooms(groupId) })
      queryClient.invalidateQueries({ queryKey: chatKeys.rooms() })
    },
  })
}

const PAGE_SIZE = 50

export interface RoomMessages {
  messages: ChatMessage[]
  isLoading: boolean
  isError: boolean
  hasMore: boolean
  isLoadingMore: boolean
  loadOlder: () => void
  send: (body: string, mediaAssetId?: number) => Promise<void>
  isSending: boolean
  sendError: string | null
  clearSendError: () => void
  deleteMessage: (messageId: number) => Promise<void>
  isDeleting: boolean
  editMessage: (messageId: number, body: string) => Promise<void>
  isEditing: boolean
}

// Owns a room's message list: loads the latest page from REST, streams new
// messages over SignalR, and supports paging older messages by cursor. The
// realtime handler dedupes by id so a sender's own POST + push won't double up.
// Mount this via a `key={roomId}` so state starts fresh per room (no
// synchronous resets in the effect).
export function useRoomMessages(roomId: number | null): RoomMessages {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading] = useState(roomId != null)
  const [isError, setIsError] = useState(false)
  const [hasMore, setHasMore] = useState(false)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [isSending, setIsSending] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const [isEditing, setIsEditing] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)

  const storeUserId = useAuthStore((s) => s.userId)
  const accessToken = useAuthStore((s) => s.accessToken)
  const currentUserId = storeUserId ?? (accessToken ? getCurrentUserId() : null)

  const markOthersSeen = useCallback(
    (incoming: ChatMessage[]) => {
      if (roomId == null || currentUserId == null) return
      const ids = incoming
        .filter((m) => !m.isDeleted && m.senderUserId !== currentUserId)
        .map((m) => m.id)
      if (ids.length === 0) return
      void markChatMessagesSeen(roomId, ids).catch(() => {})
    },
    [roomId, currentUserId],
  )

  const upsert = useCallback((incoming: ChatMessage[]) => {
    setMessages((prev) => {
      const byId = new Map(prev.map((m) => [m.id, m]))
      for (const m of incoming) byId.set(m.id, m)
      return [...byId.values()].sort((a, b) => a.id - b.id)
    })
  }, [])

  useEffect(() => {
    if (roomId == null) return
    let active = true

    getMessages(roomId, undefined, PAGE_SIZE)
      .then((page) => {
        if (!active) return
        setMessages(page)
        setHasMore(page.length === PAGE_SIZE)
        markOthersSeen(page)
      })
      .catch(() => active && setIsError(true))
      .finally(() => active && setIsLoading(false))

    let cleanup: (() => void) | undefined
    const connectRealtime = (attempt: number) => {
      subscribeToRoom(roomId, (message) => {
        upsert([message])
        markOthersSeen([message])
      })
        .then((fn) => {
          if (active) cleanup = fn
          else fn()
        })
        .catch(() => {
          if (!active || attempt >= 3) return
          window.setTimeout(() => connectRealtime(attempt + 1), 1000 * attempt)
        })
    }
    connectRealtime(1)

    return () => {
      active = false
      cleanup?.()
    }
  }, [roomId, upsert, markOthersSeen])

  const loadOlder = useCallback(() => {
    if (roomId == null || messages.length === 0 || isLoadingMore) return
    const oldestId = messages[0].id
    setIsLoadingMore(true)
    getMessages(roomId, oldestId, PAGE_SIZE)
      .then((page) => {
        upsert(page)
        markOthersSeen(page)
        setHasMore(page.length === PAGE_SIZE)
      })
      .finally(() => setIsLoadingMore(false))
  }, [roomId, messages, isLoadingMore, upsert, markOthersSeen])

  const send = useCallback(
    async (body: string, mediaAssetId?: number) => {
      const trimmed = body.trim()
      if (roomId == null || (trimmed === '' && mediaAssetId == null)) return
      setIsSending(true)
      setSendError(null)
      try {
        const message = await sendMessage(roomId, trimmed, mediaAssetId)
        upsert([message])
      } catch (error) {
        setSendError(getErrorMessage(error, 'Could not send message.'))
        throw error
      } finally {
        setIsSending(false)
      }
    },
    [roomId, upsert],
  )

  const clearSendError = useCallback(() => setSendError(null), [])

  const deleteMessage = useCallback(
    async (messageId: number) => {
      if (roomId == null) return
      setIsDeleting(true)
      try {
        await deleteChatMessage(roomId, messageId)
      } finally {
        setIsDeleting(false)
      }
    },
    [roomId],
  )

  const editMessage = useCallback(
    async (messageId: number, body: string) => {
      if (roomId == null) return
      setIsEditing(true)
      try {
        const updated = await patchChatMessage(roomId, messageId, body)
        upsert([updated])
      } finally {
        setIsEditing(false)
      }
    },
    [roomId, upsert],
  )

  return {
    messages,
    isLoading,
    isError,
    hasMore,
    isLoadingMore,
    loadOlder,
    send,
    isSending,
    sendError,
    clearSendError,
    deleteMessage,
    isDeleting,
    editMessage,
    isEditing,
  }
}
