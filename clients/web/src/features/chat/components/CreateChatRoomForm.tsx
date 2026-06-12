import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, Input } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'

export interface ChatRoomParticipantOption {
  userId: number
  nickname: string
}

export interface CreateChatRoomFormProps {
  participants: ChatRoomParticipantOption[]
  submitLabel?: string
  isPending: boolean
  error?: unknown
  onSubmit: (userIds: number[], title: string) => void
}

// Shared participant-picker form for multi-user and group-scoped chat rooms.
export function CreateChatRoomForm({
  participants,
  submitLabel = 'Create room',
  isPending,
  error,
  onSubmit,
}: CreateChatRoomFormProps) {
  const [title, setTitle] = useState('')
  const [selected, setSelected] = useState<Set<number>>(new Set())

  const toggle = (userId: number) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(userId)) next.delete(userId)
      else next.add(userId)
      return next
    })
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    const userIds = [...selected]
    if (userIds.length === 0) return
    onSubmit(userIds, title)
  }

  return (
    <form className="flex flex-col gap-3" onSubmit={handleSubmit}>
      <FormField label="Title (optional)">
        {({ id }) => (
          <Input
            id={id}
            value={title}
            maxLength={200}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="e.g. Weekend plans"
          />
        )}
      </FormField>
      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-medium text-gray-700">Participants</legend>
        {participants.length === 0 ? (
          <p className="text-sm text-gray-500">No one available to add.</p>
        ) : (
          participants.map((participant) => (
            <label
              key={participant.userId}
              className="flex items-center gap-2 text-sm text-gray-800"
            >
              <input
                type="checkbox"
                checked={selected.has(participant.userId)}
                onChange={() => toggle(participant.userId)}
                className="h-4 w-4 rounded border-gray-300"
              />
              {participant.nickname}
            </label>
          ))
        )}
      </fieldset>
      <div>
        <Button type="submit" isLoading={isPending} disabled={selected.size === 0}>
          {submitLabel}
        </Button>
      </div>
      {error != null && (
        <ErrorState title="Could not create room" message={getErrorMessage(error)} />
      )}
    </form>
  )
}
