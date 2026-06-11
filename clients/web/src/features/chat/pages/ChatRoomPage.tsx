import { type FormEvent, useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Avatar, Badge, Button, Card, ConfirmDialog, ErrorState, Input, Spinner } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { formatDateTime } from '@/lib/format'
import { cn } from '@/lib/cn'
import { MediaIntendedContext } from '@/types/api'
import { MediaUploader, useMediaUploads } from '@/features/media'
import { useAuthStore } from '@/stores/authStore'
import { useLeaveRoom, useRoom, useRoomMessages } from '../hooks'
import { roomLabel } from '../labels'
import type { ChatMessage } from '../api'

export function ChatRoomPage() {
  const { id } = useParams<{ id: string }>()
  const roomId = Number(id)
  const valid = Number.isFinite(roomId)
  const navigate = useNavigate()
  const currentUserId = useAuthStore((s) => s.userId)

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
  } = useRoomMessages(roomId)
  const [draft, setDraft] = useState('')
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
              {messages.map((message) => (
                <li key={message.id}>
                  <MessageBubble
                    message={message}
                    isOwn={message.senderUserId === currentUserId}
                  />
                </li>
              ))}
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
    </Card>
  )
}

function MessageBubble({ message, isOwn }: { message: ChatMessage; isOwn: boolean }) {
  return (
    <div className={cn('flex gap-2', isOwn && 'flex-row-reverse')}>
      <Avatar name={message.senderNickname} size="sm" />
      <div className={cn('max-w-[75%]', isOwn && 'text-right')}>
        <div className="flex items-center gap-2 text-xs text-gray-500">
          <span className="font-medium text-gray-700">{message.senderNickname}</span>
          <span>{formatDateTime(message.sentAt)}</span>
        </div>
        {message.body.length > 0 && (
          <p
            className={cn(
              'mt-1 inline-block whitespace-pre-wrap rounded-lg px-3 py-2 text-sm',
              isOwn ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-900',
            )}
          >
            {message.body}
          </p>
        )}
        {message.media && (
          <div className={cn('mt-1', isOwn && 'flex justify-end')}>
            <Badge color="blue">Attachment</Badge>
          </div>
        )}
      </div>
    </div>
  )
}
