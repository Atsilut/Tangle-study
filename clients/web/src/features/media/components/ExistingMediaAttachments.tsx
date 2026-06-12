import { Button } from '@/components/ui'
import type { MediaAsset } from '@/types/api'
import { isMediaReady } from '../normalize'
import { MediaAssetView } from './MediaAssetView'

export interface ExistingMediaAttachmentsProps {
  assets: MediaAsset[]
  removedIds: ReadonlySet<number>
  onRemove: (id: number) => void
}

export function ExistingMediaAttachments({
  assets,
  removedIds,
  onRemove,
}: ExistingMediaAttachmentsProps) {
  const visible = assets.filter((asset) => isMediaReady(asset) && !removedIds.has(asset.id))
  if (visible.length === 0) return null

  return (
    <ul className="mb-2 flex flex-col gap-1">
      {visible.map((asset) => (
        <li
          key={asset.id}
          className="flex items-center gap-2 rounded-md border border-gray-200 px-2 py-1 text-sm"
        >
          <div className="h-12 w-12 shrink-0 overflow-hidden rounded bg-gray-100">
            <MediaAssetView asset={asset} className="h-12 w-12 object-cover" />
          </div>
          <span className="min-w-0 flex-1 truncate text-gray-800">{asset.originalFileName}</span>
          <Button type="button" variant="secondary" size="sm" onClick={() => onRemove(asset.id)}>
            Remove
          </Button>
        </li>
      ))}
    </ul>
  )
}
