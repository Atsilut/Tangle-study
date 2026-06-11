import { useRef } from 'react'
import { Badge, Button, Spinner } from '@/components/ui'
import type { UploadItem, UploadStatus } from '../hooks'

export interface MediaUploaderProps {
  items: UploadItem[]
  onAddFiles: (files: FileList) => void
  onRemove: (localId: string) => void
  multiple?: boolean
  accept?: string
  label?: string
}

const statusBadge: Record<UploadStatus, { label: string; color?: 'green' | 'red' | 'yellow' }> = {
  uploading: { label: 'Uploading', color: 'yellow' },
  processing: { label: 'Processing', color: 'yellow' },
  ready: { label: 'Ready', color: 'green' },
  failed: { label: 'Failed', color: 'red' },
}

// Presentational uploader; the parent owns useMediaUploads so it can read the
// ready asset ids when submitting. Reused across posts, comments, and chat.
export function MediaUploader({
  items,
  onAddFiles,
  onRemove,
  multiple = false,
  accept = 'image/*,video/*',
  label = 'Attach media',
}: MediaUploaderProps) {
  const inputRef = useRef<HTMLInputElement>(null)

  return (
    <div className="flex flex-col gap-2">
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        multiple={multiple}
        className="sr-only"
        onChange={(e) => {
          if (e.target.files && e.target.files.length > 0) onAddFiles(e.target.files)
          e.target.value = ''
        }}
      />
      <div>
        <Button type="button" variant="secondary" size="sm" onClick={() => inputRef.current?.click()}>
          {label}
        </Button>
      </div>
      {items.length > 0 && (
        <ul className="flex flex-col gap-1">
          {items.map((item) => {
            const badge = statusBadge[item.status]
            return (
              <li
                key={item.localId}
                className="flex items-center gap-2 rounded-md border border-gray-200 px-2 py-1 text-sm"
              >
                {(item.status === 'uploading' || item.status === 'processing') && (
                  <Spinner size="sm" />
                )}
                <span className="min-w-0 flex-1 truncate text-gray-800">{item.fileName}</span>
                <Badge color={badge.color}>{badge.label}</Badge>
                {item.status === 'failed' && item.error && (
                  <span className="text-xs text-red-600">{item.error}</span>
                )}
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  onClick={() => onRemove(item.localId)}
                >
                  Remove
                </Button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
