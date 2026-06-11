import { api, getList } from '@/lib/apiClient'
import type { ChatRoomKind, ChatRoomParticipantRole, MediaAsset } from '@/types/api'

export interface ChatRoomParticipant {
  id: number
  userId: number
  nickname: string
  role: ChatRoomParticipantRole
  joinedAt: string
}

export interface ChatRoom {
  id: number
  kind: ChatRoomKind
  title: string | null
  platformGroupId: number | null
  createdByUserId: number
  createdAt: string
  updatedAt: string
  participants: ChatRoomParticipant[]
}

export interface ChatRoomSummary {
  id: number
  kind: ChatRoomKind
  title: string | null
  platformGroupId: number | null
  updatedAt: string
}

export interface ChatMessage {
  id: number
  chatRoomId: number
  senderUserId: number
  senderNickname: string
  body: string
  sentAt: string
  media: MediaAsset | null
}

// GET /api/chat/rooms -> 200 list | 204 (rooms I participate in)
export function getMyRooms(): Promise<ChatRoomSummary[]> {
  return getList<ChatRoomSummary>('/chat/rooms')
}

// GET /api/chat/rooms/{roomId} -> 200 (participants only)
export async function getRoom(roomId: number): Promise<ChatRoom> {
  const res = await api.get<ChatRoom>(`/chat/rooms/${roomId}`, {
    treatUnauthorizedAsForbidden: true,
  })
  return res.data
}

// POST /api/chat/rooms/direct -> 200 (get or create 1:1 with a friend)
export async function getOrCreateDirectRoom(otherUserId: number): Promise<ChatRoom> {
  const res = await api.post<ChatRoom>(
    '/chat/rooms/direct',
    { otherUserId },
    { treatUnauthorizedAsForbidden: true },
  )
  return res.data
}

// POST /api/chat/rooms/multi -> 201 (creator added as owner)
export async function createMultiRoom(
  participantUserIds: number[],
  title?: string,
): Promise<ChatRoom> {
  const res = await api.post<ChatRoom>('/chat/rooms/multi', {
    title: title?.trim() ? title.trim() : undefined,
    participantUserIds,
  })
  return res.data
}

// POST /api/chat/rooms/{roomId}/participants -> 201
export async function addParticipant(
  roomId: number,
  userId: number,
): Promise<ChatRoomParticipant> {
  const res = await api.post<ChatRoomParticipant>(
    `/chat/rooms/${roomId}/participants`,
    { userId },
    { treatUnauthorizedAsForbidden: true },
  )
  return res.data
}

// DELETE /api/chat/rooms/{roomId}/participants/me -> 204
export async function leaveRoom(roomId: number): Promise<void> {
  await api.delete(`/chat/rooms/${roomId}/participants/me`)
}

// GET /api/chat/rooms/{roomId}/messages?before=&limit= -> 200 ascending | 204
export function getMessages(
  roomId: number,
  before?: number,
  limit = 50,
): Promise<ChatMessage[]> {
  const params = new URLSearchParams({ limit: String(limit) })
  if (before != null) params.set('before', String(before))
  return getList<ChatMessage>(`/chat/rooms/${roomId}/messages?${params.toString()}`)
}

// POST /api/chat/rooms/{roomId}/messages -> 201
export async function sendMessage(
  roomId: number,
  body: string,
  mediaAssetId?: number,
): Promise<ChatMessage> {
  const res = await api.post<ChatMessage>(`/chat/rooms/${roomId}/messages`, {
    body,
    mediaAssetId,
  })
  return res.data
}
