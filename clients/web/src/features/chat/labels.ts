import { ChatRoomKind } from '@/types/api'
import type { ChatRoom, ChatRoomSummary } from './api'

export const chatRoomKindLabels: Record<ChatRoomKind, string> = {
  [ChatRoomKind.Direct]: 'Direct',
  [ChatRoomKind.Multi]: 'Group',
  [ChatRoomKind.PlatformGroup]: 'Group chat',
}

// Room title for list rows; untitled rooms use other participants when available.
export function summaryLabel(room: ChatRoomSummary): string {
  if (room.title?.trim()) return room.title.trim()
  const others = room.otherParticipantNicknames ?? []
  if (others.length > 0) return others.join(', ')
  return chatRoomKindLabels[room.kind]
}

export function summaryLastMessagePreview(
  room: ChatRoomSummary,
  currentUserId: number | null,
): string | null {
  const last = room.lastMessage
  if (!last) return null

  const body = last.body.trim()
  const text = body.length > 0 ? body : last.hasMedia ? 'Attachment' : ''
  if (!text) return null

  if (room.kind === ChatRoomKind.Direct) return text

  const sender =
    currentUserId != null && last.senderUserId === currentUserId ? 'You' : last.senderNickname
  return `${sender}: ${text}`
}

// Full name once participants are loaded: direct rooms show the other person.
export function roomLabel(room: ChatRoom, currentUserId: number | null): string {
  if (room.title?.trim()) return room.title.trim()
  if (room.kind === ChatRoomKind.Direct) {
    const other = room.participants.find((p) => p.userId !== currentUserId)
    return other?.nickname ?? 'Direct'
  }
  const others = room.participants
    .filter((p) => p.userId !== currentUserId)
    .map((p) => p.nickname)
  return others.length > 0 ? others.join(', ') : chatRoomKindLabels[room.kind]
}
