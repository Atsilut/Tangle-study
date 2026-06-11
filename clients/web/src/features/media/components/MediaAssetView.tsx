import { useEffect, useState } from 'react'
import { Spinner } from '@/components/ui'
import { api } from '@/lib/apiClient'
import { cn } from '@/lib/cn'
import { MediaKind, type MediaAsset } from '@/types/api'
import { isMediaReady } from '../normalize'

export function mediaContentUrl(id: number): string {
  return `/api/media/${id}/content`
}

export interface MediaAssetViewProps {
  asset: MediaAsset
  /** Chat attachments require JWT; posts and comments are public. */
  authenticated?: boolean
  className?: string
}

function PublicMediaView({ asset, className }: { asset: MediaAsset; className?: string }) {
  const src = mediaContentUrl(asset.id)
  if (asset.kind === MediaKind.Video) {
    return (
      <video
        src={src}
        controls
        className={cn('max-h-80 max-w-full rounded-md', className)}
        aria-label={asset.originalFileName}
      />
    )
  }
  return (
    <img
      src={src}
      alt={asset.originalFileName}
      className={cn('max-h-80 max-w-full rounded-md object-contain', className)}
    />
  )
}

function AuthenticatedMediaView({ asset, className }: { asset: MediaAsset; className?: string }) {
  const [src, setSrc] = useState<string>()
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let active = true
    let objectUrl: string | undefined
    setFailed(false)
    setSrc(undefined)

    api
      .get<Blob>(`/media/${asset.id}/content`, { responseType: 'blob', treatUnauthorizedAsForbidden: true })
      .then((res) => {
        if (!active) return
        objectUrl = URL.createObjectURL(res.data)
        setSrc(objectUrl)
      })
      .catch(() => {
        if (active) setFailed(true)
      })

    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [asset.id])

  if (failed) {
    return <p className="text-xs text-red-600">Could not load attachment</p>
  }
  if (!src) {
    return (
      <div className="flex h-20 w-20 items-center justify-center rounded-md bg-gray-100">
        <Spinner size="sm" />
      </div>
    )
  }

  if (asset.kind === MediaKind.Video) {
    return (
      <video
        src={src}
        controls
        className={cn('max-h-80 max-w-full rounded-md', className)}
        aria-label={asset.originalFileName}
      />
    )
  }
  return (
    <img
      src={src}
      alt={asset.originalFileName}
      className={cn('max-h-80 max-w-full rounded-md object-contain', className)}
    />
  )
}

export function MediaAssetView({ asset, authenticated = false, className }: MediaAssetViewProps) {
  if (!isMediaReady(asset)) return null

  if (authenticated) return <AuthenticatedMediaView asset={asset} className={className} />
  return <PublicMediaView asset={asset} className={className} />
}
