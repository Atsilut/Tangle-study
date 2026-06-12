import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, Input, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { MediaIntendedContext, type MediaAsset } from '@/types/api'
import {
  ExistingMediaAttachments,
  isMediaReady,
  MediaUploader,
  useMediaUploads,
} from '@/features/media'

export interface PostFormValues {
  title: string
  content: string
  mediaAssetIds?: number[]
  addMediaAssetIds?: number[]
  removeMediaAssetIds?: number[]
}

export interface PostFormProps {
  initial?: Pick<PostFormValues, 'title' | 'content'>
  existingMedia?: MediaAsset[]
  submitLabel: string
  isPending: boolean
  error?: unknown
  enableMedia?: boolean
  onSubmit: (values: PostFormValues) => void
  onCancel?: () => void
}

// Reused for both create and edit. Title/content match PostCreateRequestDto and
// PostPatchRequestDto (both capped at 100 chars by the backend).
export function PostForm({
  initial,
  existingMedia,
  submitLabel,
  isPending,
  error,
  enableMedia = false,
  onSubmit,
  onCancel,
}: PostFormProps) {
  const [title, setTitle] = useState(initial?.title ?? '')
  const [content, setContent] = useState(initial?.content ?? '')
  const [removedExistingIds, setRemovedExistingIds] = useState<ReadonlySet<number>>(() => new Set())
  const media = useMediaUploads(MediaIntendedContext.Post)
  const isEdit = existingMedia != null

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const trimmed = { title: title.trim(), content: content.trim() }

    if (isEdit) {
      const removeMediaAssetIds = existingMedia
        .filter((asset) => isMediaReady(asset) && removedExistingIds.has(asset.id))
        .map((asset) => asset.id)
      const addMediaAssetIds =
        enableMedia && media.readyIds.length > 0 ? media.readyIds : undefined

      onSubmit({
        ...trimmed,
        ...(addMediaAssetIds != null ? { addMediaAssetIds } : {}),
        ...(removeMediaAssetIds.length > 0 ? { removeMediaAssetIds } : {}),
      })
      return
    }

    onSubmit({
      ...trimmed,
      mediaAssetIds:
        enableMedia && media.readyIds.length > 0 ? media.readyIds : undefined,
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
            <>
              {isEdit && existingMedia.length > 0 && (
                <ExistingMediaAttachments
                  assets={existingMedia}
                  removedIds={removedExistingIds}
                  onRemove={(id) =>
                    setRemovedExistingIds((prev) => new Set([...prev, id]))
                  }
                />
              )}
              <MediaUploader
                items={media.items}
                onAddFiles={media.addFiles}
                onRemove={media.removeItem}
                multiple
              />
            </>
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
