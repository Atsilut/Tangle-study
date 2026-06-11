import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, Input, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'

export interface PostFormValues {
  title: string
  content: string
}

export interface PostFormProps {
  initial?: PostFormValues
  submitLabel: string
  isPending: boolean
  error?: unknown
  onSubmit: (values: PostFormValues) => void
  onCancel?: () => void
}

// Reused for both create and edit. Title/content match PostCreateRequestDto and
// PostPatchRequestDto (both capped at 100 chars by the backend).
export function PostForm({
  initial,
  submitLabel,
  isPending,
  error,
  onSubmit,
  onCancel,
}: PostFormProps) {
  const [title, setTitle] = useState(initial?.title ?? '')
  const [content, setContent] = useState(initial?.content ?? '')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({ title: title.trim(), content: content.trim() })
  }

  const canSubmit = title.trim() !== '' && content.trim() !== ''

  return (
    <form className="flex flex-col gap-4" onSubmit={submit}>
      {error != null && <ErrorState title="Could not save" message={getErrorMessage(error)} />}
      <FormField label="Title" required>
        {({ id }) => (
          <Input
            id={id}
            value={title}
            maxLength={100}
            onChange={(e) => setTitle(e.target.value)}
            required
          />
        )}
      </FormField>
      <FormField label="Content" required>
        {({ id }) => (
          <TextArea
            id={id}
            value={content}
            maxLength={100}
            rows={6}
            onChange={(e) => setContent(e.target.value)}
            required
          />
        )}
      </FormField>
      <div className="flex items-center gap-2">
        <Button type="submit" isLoading={isPending} disabled={!canSubmit}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
    </form>
  )
}
