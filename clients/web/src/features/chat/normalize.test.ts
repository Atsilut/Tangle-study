import { describe, expect, it } from 'vitest'
import { normalizeChatMessage } from './normalize'

describe('normalizeChatMessage', () => {
  it('normalizes camelCase payloads', () => {
    const message = normalizeChatMessage({
      id: 1,
      chatRoomId: 2,
      senderUserId: 3,
      senderNickname: 'alice',
      body: 'Hello',
      sentAt: '2026-06-13T12:00:00Z',
      updatedAt: '2026-06-13T12:00:00Z',
      isDeleted: false,
      isEdited: false,
      canEdit: true,
      canDelete: true,
      editHistory: null,
      media: null,
    })

    expect(message.id).toBe(1)
    expect(message.senderNickname).toBe('alice')
    expect(message.canEdit).toBe(true)
  })

  it('normalizes PascalCase payloads', () => {
    const message = normalizeChatMessage({
      Id: 5,
      ChatRoomId: 6,
      SenderUserId: 7,
      SenderNickname: 'bob',
      Body: 'Hi',
      SentAt: '2026-06-13T12:00:00Z',
      IsDeleted: true,
      IsEdited: false,
    })

    expect(message.id).toBe(5)
    expect(message.body).toBe('Hi')
    expect(message.isDeleted).toBe(true)
  })
})
