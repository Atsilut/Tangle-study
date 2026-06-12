import { api, getList } from '@/lib/apiClient'
import type { ChatRoomKind, ChatRoomParticipantRole, MediaAsset } from '@/types/api'

// Chat returns 401 when the caller is authenticated but not a room participant.
const asForbidden = { treatUnauthorizedAsForbidden: true }

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

export interface ChatRoomSummaryLastMessage {
  senderUserId: number
  body: string
  senderNickname: string
  sentAt: string
  hasMedia: boolean
}

export interface ChatRoomSummary {
  id: number
  kind: ChatRoomKind
  title: string | null
  platformGroupId: number | null
  updatedAt: string
  /** Present after #20 API deploy; omitted on older API builds. */
  otherParticipantNicknames?: string[]
  lastMessage?: ChatRoomSummaryLastMessage | null
}

function normalizeChatRoomSummary(room: ChatRoomSummary): ChatRoomSummary {
  return {
    ...room,
    otherParticipantNicknames: room.otherParticipantNicknames ?? [],
    lastMessage: room.lastMessage ?? null,
  }
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
export async function getMyRooms(): Promise<ChatRoomSummary[]> {
  const rooms = await getList<ChatRoomSummary>('/chat/rooms', asForbidden)
  return rooms.map(normalizeChatRoomSummary)
}

// GET /api/chat/rooms/{roomId} -> 200 (participants only)
export async function getRoom(roomId: number): Promise<ChatRoom> {
  const res = await api.get<ChatRoom>(`/chat/rooms/${roomId}`, asForbidden)
  return res.data
}

// POST /api/chat/rooms/direct -> 200 (get or create 1:1 with a friend)
export async function getOrCreateDirectRoom(otherUserId: number): Promise<ChatRoom> {
  const res = await api.post<ChatRoom>(
    '/chat/rooms/direct',
    { otherUserId },
    asForbidden,
  )
  return res.data
}

// POST /api/chat/rooms/multi -> 201 (creator added as owner)
export async function createMultiRoom(
  participantUserIds: number[],
  title?: string,
): Promise<ChatRoom> {
  const res = await api.post<ChatRoom>(
    '/chat/rooms/multi',
    {
      title: title?.trim() ? title.trim() : undefined,
      participantUserIds,
    },
    asForbidden,
  )
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
    asForbidden,
  )
  return res.data
}

// DELETE /api/chat/rooms/{roomId}/participants/me -> 204
export async function leaveRoom(roomId: number): Promise<void> {
  await api.delete(`/chat/rooms/${roomId}/participants/me`, asForbidden)
}

// GET /api/groups/{groupId}/chat-rooms -> 200 list | 204 (group members only)
export async function getGroupRooms(groupId: number): Promise<ChatRoomSummary[]> {
  const rooms = await getList<ChatRoomSummary>(`/groups/${groupId}/chat-rooms`, asForbidden)
  return rooms.map(normalizeChatRoomSummary)
}

// POST /api/groups/{groupId}/chat-rooms -> 201 (creator added as owner)
export async function createGroupRoom(
  groupId: number,
  participantUserIds: number[],
  title?: string,
): Promise<ChatRoom> {
  const res = await api.post<ChatRoom>(
    `/groups/${groupId}/chat-rooms`,
    { title: title?.trim() ? title.trim() : undefined, participantUserIds },
    asForbidden,
  )
  return res.data
}

// GET /api/chat/rooms/{roomId}/messages?before=&limit= -> 200 ascending | 204
export function getMessages(
  roomId: number,
  before?: number,
  limit = 50,
): Promise<ChatMessage[]> {
  const params = new URLSearchParams({ limit: String(limit) })
  if (before != null) params.set('before', String(before))
  return getList<ChatMessage>(`/chat/rooms/${roomId}/messages?${params.toString()}`, asForbidden)
}

// DELETE /api/chat/rooms/{roomId}/messages/{messageId} -> 204 (sender only)
export async function deleteChatMessage(roomId: number, messageId: number): Promise<void> {
  await api.delete(`/chat/rooms/${roomId}/messages/${messageId}`, asForbidden)
}

// POST /api/chat/rooms/{roomId}/messages -> 201
export async function sendMessage(
  roomId: number,
  body: string,
  mediaAssetId?: number,
): Promise<ChatMessage> {
  const res = await api.post<ChatMessage>(
    `/chat/rooms/${roomId}/messages`,
    { body, mediaAssetId },
    asForbidden,
  )
  return res.data
}
