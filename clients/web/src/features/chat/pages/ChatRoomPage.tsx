import { type FormEvent, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Avatar, Button, Card, ConfirmDialog, ErrorState, Input, Modal, Spinner } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { formatDateTime } from '@/lib/format'
import { cn } from '@/lib/cn'
import { MediaIntendedContext } from '@/types/api'
import { MediaAssetView, MediaUploader, useMediaUploads } from '@/features/media'
import { useAuthStore, getCurrentUserId } from '@/stores/authStore'
import { useLeaveRoom, useRoom, useRoomMessages } from '../hooks'
import { roomLabel } from '../labels'
import type { ChatMessage, ChatMessageEditHistory } from '../api'

export function ChatRoomPage() {
  const { id } = useParams<{ id: string }>()
  const roomId = Number(id)
  const valid = Number.isFinite(roomId)
  const navigate = useNavigate()
  const storeUserId = useAuthStore((s) => s.userId)
  const accessToken = useAuthStore((s) => s.accessToken)
  const currentUserId = storeUserId ?? (accessToken ? getCurrentUserId() : null)

  const room = useRoom(valid ? roomId : null)
  const leave = useLeaveRoom()
  const [confirmLeave, setConfirmLeave] = useState(false)

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <div className="flex items-center justify-between">
        <Link to="/chat" className="text-sm text-blue-600 hover:underline">
          Back to chats
        </Link>
        <Button size="sm" variant="secondary" onClick={() => setConfirmLeave(true)}>
          Leave
        </Button>
      </div>

      <h1 className="text-xl font-bold text-gray-900">
        {room.data ? roomLabel(room.data, currentUserId) : 'Chat'}
      </h1>

      {valid && room.isError && (
        <ErrorState
          title="No access"
          message="You are not a participant in this chat room."
        />
      )}
      {valid && room.data && (
        <Conversation key={roomId} roomId={roomId} currentUserId={currentUserId} />
      )}

      <ConfirmDialog
        isOpen={confirmLeave}
        title="Leave chat"
        message="You will stop receiving messages from this conversation."
        confirmLabel="Leave"
        destructive
        isLoading={leave.isPending}
        onConfirm={() =>
          leave.mutate(roomId, { onSuccess: () => navigate('/chat', { replace: true }) })
        }
        onCancel={() => setConfirmLeave(false)}
      />
    </div>
  )
}

// Keyed by roomId so message state resets cleanly when switching rooms.
function Conversation({
  roomId,
  currentUserId,
}: {
  roomId: number
  currentUserId: number | null
}) {
  const {
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
  } = useRoomMessages(roomId)
  const [draft, setDraft] = useState('')
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null)
  const [historyMessage, setHistoryMessage] = useState<ChatMessage | null>(null)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editDraft, setEditDraft] = useState('')
  const media = useMediaUploads(MediaIntendedContext.ChatMessage)

  const bottomRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages])

  const mediaId = media.readyIds[0]
  const canSend = !isSending && !media.isUploading && (draft.trim() !== '' || mediaId != null)

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (!canSend) return
    const body = draft
    setDraft('')
    void send(body, mediaId)
      .then(() => media.reset())
      .catch(() => {})
  }

  return (
    <Card className="flex h-[60vh] flex-col">
      <div className="flex-1 overflow-y-auto px-4 py-3">
        {hasMore && (
          <div className="mb-3 flex justify-center">
            <Button size="sm" variant="secondary" isLoading={isLoadingMore} onClick={loadOlder}>
              Load older
            </Button>
          </div>
        )}
        <QueryBoundary isLoading={isLoading} isError={isError}>
          {messages.length === 0 ? (
            <p className="py-8 text-center text-sm text-gray-500">No messages yet.</p>
          ) : (
            <ul className="flex flex-col gap-3">
              {messages.map((message) => {
                const isOwn =
                  currentUserId != null && message.senderUserId === currentUserId
                const showEdit =
                  isOwn && !message.isDeleted && message.canEdit !== false
                const showDelete =
                  isOwn && !message.isDeleted && message.canDelete !== false
                return (
                <li key={message.id}>
                  <MessageBubble
                    message={message}
                    isOwn={isOwn}
                    isEditing={editingId === message.id}
                    editDraft={editingId === message.id ? editDraft : ''}
                    onEditDraftChange={setEditDraft}
                    onStartEdit={
                      showEdit
                        ? () => {
                            setEditingId(message.id)
                            setEditDraft(message.body)
                          }
                        : undefined
                    }
                    onCancelEdit={() => {
                      setEditingId(null)
                      setEditDraft('')
                    }}
                    onSaveEdit={
                      editingId === message.id
                        ? () => {
                            const trimmed = editDraft.trim()
                            if (trimmed === '') return
                            void editMessage(message.id, trimmed).then(() => {
                              setEditingId(null)
                              setEditDraft('')
                            })
                          }
                        : undefined
                    }
                    isSavingEdit={isEditing}
                    onDelete={
                      showDelete ? () => setConfirmDeleteId(message.id) : undefined
                    }
                    onShowHistory={
                      message.isEdited && !message.isDeleted && message.editHistory
                        ? () => setHistoryMessage(message)
                        : undefined
                    }
                  />
                </li>
                )
              })}
            </ul>
          )}
          <div ref={bottomRef} />
        </QueryBoundary>
      </div>

      <div className="flex flex-col gap-2 border-t border-gray-100 p-3">
        {sendError && (
          <p className="text-sm text-red-600" role="alert">
            {sendError}
            <button
              type="button"
              className="ml-2 underline"
              onClick={clearSendError}
            >
              Dismiss
            </button>
          </p>
        )}
        <MediaUploader
          items={media.items}
          onAddFiles={media.addFiles}
          onRemove={media.removeItem}
        />
        <form className="flex items-center gap-2" onSubmit={onSubmit}>
          <Input
            aria-label="Message"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="Type a message"
            maxLength={2000}
          />
          <Button type="submit" disabled={!canSend}>
            {isSending ? <Spinner size="sm" /> : 'Send'}
          </Button>
        </form>
      </div>

      <ConfirmDialog
        isOpen={confirmDeleteId != null}
        title="Delete message"
        message="This removes your message for everyone in the chat."
        confirmLabel="Delete"
        destructive
        isLoading={isDeleting}
        onConfirm={() => {
          if (confirmDeleteId == null) return
          void deleteMessage(confirmDeleteId).then(() => setConfirmDeleteId(null))
        }}
        onCancel={() => setConfirmDeleteId(null)}
      />

      <EditHistoryModal
        message={historyMessage}
        isOpen={historyMessage != null}
        onClose={() => setHistoryMessage(null)}
      />
    </Card>
  )
}

function MessageBubble({
  message,
  isOwn,
  isEditing,
  editDraft,
  onEditDraftChange,
  onStartEdit,
  onCancelEdit,
  onSaveEdit,
  isSavingEdit,
  onDelete,
  onShowHistory,
}: {
  message: ChatMessage
  isOwn: boolean
  isEditing?: boolean
  editDraft?: string
  onEditDraftChange?: (value: string) => void
  onStartEdit?: () => void
  onCancelEdit?: () => void
  onSaveEdit?: () => void
  isSavingEdit?: boolean
  onDelete?: () => void
  onShowHistory?: () => void
}) {
  return (
    <div className={cn('flex gap-2', isOwn && 'flex-row-reverse')}>
      <Avatar name={message.senderNickname} size="sm" />
      <div className={cn('max-w-[75%]', isOwn && 'text-right')}>
        <div
          className={cn(
            'flex items-center gap-2 text-xs text-gray-500',
            isOwn && 'flex-row-reverse',
          )}
        >
          <span className="font-medium text-gray-700">{message.senderNickname}</span>
          <span>{formatDateTime(message.sentAt)}</span>
          {onShowHistory && (
            <button
              type="button"
              className="italic text-gray-400 hover:text-blue-600 hover:underline"
              aria-label="View edit history"
              onClick={onShowHistory}
            >
              edited
            </button>
          )}
          {onStartEdit && (
            <button
              type="button"
              className="text-blue-600 hover:underline"
              aria-label="Edit message"
              onClick={onStartEdit}
            >
              Edit
            </button>
          )}
          {onDelete && (
            <button
              type="button"
              className="text-red-600 hover:underline"
              aria-label="Delete message"
              onClick={onDelete}
            >
              Delete
            </button>
          )}
        </div>
        {message.isDeleted ? (
          <p className="mt-1 text-sm italic text-gray-400">Message deleted</p>
        ) : isEditing ? (
          <div className={cn('mt-1 flex flex-col gap-2', isOwn && 'items-end')}>
            <Input
              aria-label="Edit message"
              value={editDraft ?? ''}
              onChange={(e) => onEditDraftChange?.(e.target.value)}
              maxLength={1000}
            />
            <div className="flex gap-2">
              <Button size="sm" variant="secondary" onClick={onCancelEdit}>
                Cancel
              </Button>
              <Button
                size="sm"
                disabled={!editDraft?.trim() || isSavingEdit}
                onClick={onSaveEdit}
              >
                {isSavingEdit ? <Spinner size="sm" /> : 'Save'}
              </Button>
            </div>
          </div>
        ) : (
          message.body.length > 0 && (
            <p
              className={cn(
                'mt-1 inline-block whitespace-pre-wrap rounded-lg px-3 py-2 text-sm',
                isOwn ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-900',
              )}
            >
              {message.body}
            </p>
          )
        )}
        {!message.isDeleted && message.media && (
          <div className={cn('mt-1', isOwn && 'flex justify-end')}>
            <MediaAssetView asset={message.media} authenticated />
          </div>
        )}
      </div>
    </div>
  )
}

function EditHistoryModal({
  message,
  isOpen,
  onClose,
}: {
  message: ChatMessage | null
  isOpen: boolean
  onClose: () => void
}) {
  if (!message?.editHistory) return null

  const versions = collectEditHistoryVersions(message.body, message.updatedAt, message.editHistory)

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Edit history"
      className="max-w-lg"
      footer={
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      }
    >
      <p className="mb-4 text-sm text-gray-500">
        {message.senderNickname} · sent {formatDateTime(message.sentAt)}
      </p>
      <ol className="relative flex max-h-[min(60vh,24rem)] flex-col gap-0 overflow-y-auto pr-1">
        {versions.map((version, index) => (
          <li
            key={version.key}
            className={cn(
              'relative flex gap-3 pb-6 last:pb-0',
              index < versions.length - 1 &&
                'before:absolute before:top-3 before:left-[0.4375rem] before:h-[calc(100%-0.75rem)] before:w-px before:bg-gray-200',
            )}
          >
            <span
              className={cn(
                'relative z-10 mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full ring-2 ring-white',
                version.isCurrent ? 'bg-blue-600' : 'bg-gray-300',
              )}
              aria-hidden
            />
            <div className="min-w-0 flex-1">
              <time className="text-xs text-gray-400">{formatDateTime(version.recordedAt)}</time>
              <p
                className={cn(
                  'mt-1 whitespace-pre-wrap rounded-lg px-3 py-2 text-sm',
                  version.isCurrent
                    ? 'bg-blue-50 text-blue-950 ring-1 ring-blue-100'
                    : 'bg-gray-50 text-gray-800 ring-1 ring-gray-100',
                )}
              >
                {version.body}
              </p>
            </div>
          </li>
        ))}
      </ol>
    </Modal>
  )
}

interface EditHistoryVersion {
  key: string
  body: string
  recordedAt: string
  isCurrent: boolean
}

function collectEditHistoryVersions(
  currentBody: string,
  updatedAt: string,
  history: ChatMessageEditHistory,
): EditHistoryVersion[] {
  const versions: EditHistoryVersion[] = [
    {
      key: 'current',
      body: currentBody,
      recordedAt: updatedAt,
      isCurrent: true,
    },
  ]

  let node: ChatMessageEditHistory | null = history
  while (node) {
    versions.push({
      key: String(node.id),
      body: node.body,
      recordedAt: node.recordedAt,
      isCurrent: false,
    })
    node = node.previousEdits[0] ?? null
  }

  return versions
}
