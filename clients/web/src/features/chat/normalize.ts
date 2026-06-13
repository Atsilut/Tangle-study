import { normalizeMediaAsset } from '@/features/media/normalize'
import type { ChatMessage, ChatMessageEditHistory } from './api'

function readOptionalBool(
  raw: Record<string, unknown>,
  camel: string,
  pascal: string,
): boolean | undefined {
  if (Object.prototype.hasOwnProperty.call(raw, camel)) return Boolean(raw[camel])
  if (Object.prototype.hasOwnProperty.call(raw, pascal)) return Boolean(raw[pascal])
  return undefined
}

export function normalizeEditHistory(raw: unknown): ChatMessageEditHistory | null {
  if (raw == null) return null
  const e = raw as Record<string, unknown>
  const previous = e.previousEdits ?? e.PreviousEdits
  const previousList = Array.isArray(previous) ? previous : []
  return {
    id: Number(e.id ?? e.Id),
    body: String(e.body ?? e.Body ?? ''),
    recordedAt: String(e.recordedAt ?? e.RecordedAt ?? ''),
    previousEdits: previousList.map((item) => normalizeEditHistory(item)!),
  }
}

/** Normalize API/SignalR payloads (camelCase or PascalCase). */
export function normalizeChatMessage(raw: unknown): ChatMessage {
  const m = raw as Record<string, unknown>
  return {
    id: Number(m.id ?? m.Id),
    chatRoomId: Number(m.chatRoomId ?? m.ChatRoomId),
    senderUserId: Number(m.senderUserId ?? m.SenderUserId),
    senderNickname: String(m.senderNickname ?? m.SenderNickname ?? ''),
    body: String(m.body ?? m.Body ?? ''),
    sentAt: String(m.sentAt ?? m.SentAt ?? ''),
    updatedAt: String(m.updatedAt ?? m.UpdatedAt ?? m.sentAt ?? m.SentAt ?? ''),
    isDeleted: Boolean(m.isDeleted ?? m.IsDeleted ?? false),
    isEdited: Boolean(m.isEdited ?? m.IsEdited ?? false),
    canEdit: readOptionalBool(m, 'canEdit', 'CanEdit'),
    canDelete: readOptionalBool(m, 'canDelete', 'CanDelete'),
    editHistory: normalizeEditHistory(m.editHistory ?? m.EditHistory),
    media: normalizeMediaAsset(m.media ?? m.Media),
  }
}
