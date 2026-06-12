import type { MediaAsset } from '@/types/api'
import { isMediaReady } from '../normalize'
import { MediaAssetView } from './MediaAssetView'

export interface MediaGalleryProps {
  assets: MediaAsset[]
  authenticated?: boolean
}

export function MediaGallery({ assets, authenticated = false }: MediaGalleryProps) {
  const ready = assets.filter(isMediaReady)
  if (ready.length === 0) return null

  return (
    <ul className="flex flex-wrap gap-2">
      {ready.map((asset) => (
        <li key={asset.id}>
          <MediaAssetView asset={asset} authenticated={authenticated} />
        </li>
      ))}
    </ul>
  )
}
