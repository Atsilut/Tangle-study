import { type FormEvent, useState } from 'react'
import { Button, ErrorState, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'

export interface CommentFormProps {
  initial?: string
  submitLabel: string
  placeholder?: string
  isPending: boolean
  error?: unknown
  autoFocus?: boolean
  onSubmit: (content: string) => void
  onCancel?: () => void
}

// Reused for new comment, reply, and edit (content max 1000 per backend).
export function CommentForm({
  initial = '',
  submitLabel,
  placeholder = 'Write a comment…',
  isPending,
  error,
  autoFocus,
  onSubmit,
  onCancel,
}: CommentFormProps) {
  const [content, setContent] = useState(initial)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const trimmed = content.trim()
    if (trimmed === '') return
    onSubmit(trimmed)
  }

  return (
    <form className="flex flex-col gap-2" onSubmit={submit}>
      {error != null && <ErrorState title="Could not save" message={getErrorMessage(error)} />}
      <TextArea
        value={content}
        maxLength={1000}
        rows={3}
        autoFocus={autoFocus}
        placeholder={placeholder}
        onChange={(e) => setContent(e.target.value)}
      />
      <div className="flex items-center gap-2">
        <Button type="submit" size="sm" isLoading={isPending} disabled={content.trim() === ''}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
    </form>
  )
}
