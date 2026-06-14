import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { MediaIntendedContext } from '@/types/api'
import { MediaUploader, useMediaUploads } from '@/features/media'

export interface CommentFormProps {
  initial?: string
  submitLabel: string
  placeholder?: string
  isPending: boolean
  error?: unknown
  autoFocus?: boolean
  enableMedia?: boolean
  onSubmit: (content: string, mediaAssetId?: number) => void
  onCancel?: () => void
}

// Reused for new comment, reply, and edit (content max 1000 per backend).
// Media can only be attached on create/reply (patch takes no media).
export function CommentForm({
  initial = '',
  submitLabel,
  placeholder = 'Write a comment…',
  isPending,
  error,
  autoFocus,
  enableMedia = false,
  onSubmit,
  onCancel,
}: CommentFormProps) {
  const [content, setContent] = useState(initial)
  const media = useMediaUploads(MediaIntendedContext.Comment)
  const mediaId = enableMedia ? media.readyIds[0] : undefined
  const hasContent = content.trim() !== ''
  const canSubmit =
    !(enableMedia && media.isUploading) && (hasContent || (enableMedia && mediaId != null))

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!canSubmit) return
    onSubmit(content.trim(), mediaId)
    if (enableMedia) media.reset()
  }

  return (
    <form className="flex flex-col gap-2" onSubmit={submit}>
      {error != null && <ErrorState title="Could not save" message={getErrorMessage(error)} />}
      <FormField label="Comment">
        {({ id }) => (
          <TextArea
            id={id}
            value={content}
            maxLength={1000}
            rows={3}
            autoFocus={autoFocus}
            placeholder={placeholder}
            onChange={(e) => setContent(e.target.value)}
          />
        )}
      </FormField>
      {enableMedia && (
        <MediaUploader
          items={media.items}
          onAddFiles={media.addFiles}
          onRemove={media.removeItem}
        />
      )}
      <div className="flex items-center gap-2">
        <Button type="submit" size="sm" isLoading={isPending} disabled={!canSubmit}>
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
