import { ChatRoomKind } from '@/types/api'
import type { ChatRoom, ChatRoomSummary } from './api'

export const chatRoomKindLabels: Record<ChatRoomKind, string> = {
  [ChatRoomKind.Direct]: 'Direct',
  [ChatRoomKind.Multi]: 'Group',
  [ChatRoomKind.PlatformGroup]: 'Group chat',
}

// Best-effort name for a list summary (no participants available).
export function summaryLabel(room: ChatRoomSummary): string {
  return room.title?.trim() || chatRoomKindLabels[room.kind]
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
