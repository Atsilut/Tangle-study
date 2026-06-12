import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, Input, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { MediaIntendedContext } from '@/types/api'
import { MediaUploader, useMediaUploads } from '@/features/media'

export interface PostFormValues {
  title: string
  content: string
  mediaAssetIds?: number[]
}

export interface PostFormProps {
  initial?: PostFormValues
  submitLabel: string
  isPending: boolean
  error?: unknown
  // Media can only be attached on create (the patch endpoint takes no media).
  enableMedia?: boolean
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
  enableMedia = false,
  onSubmit,
  onCancel,
}: PostFormProps) {
  const [title, setTitle] = useState(initial?.title ?? '')
  const [content, setContent] = useState(initial?.content ?? '')
  const media = useMediaUploads(MediaIntendedContext.Post)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({
      title: title.trim(),
      content: content.trim(),
      mediaAssetIds: enableMedia && media.readyIds.length > 0 ? media.readyIds : undefined,
    })
  }

  const canSubmit =
    title.trim() !== '' && content.trim() !== '' && !(enableMedia && media.isUploading)

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
      {enableMedia && (
        <FormField label="Media">
          {() => (
            <MediaUploader
              items={media.items}
              onAddFiles={media.addFiles}
              onRemove={media.removeItem}
              multiple
            />
          )}
        </FormField>
      )}
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
