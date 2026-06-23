import { useCallback, useEffect, useRef, useState } from 'react'
import { MediaIntendedContext, MediaProcessingStatus } from '@/types/api'
import { getErrorMessage } from '@/lib/apiError'
import {
  completeUpload,
  deleteUnlinkedMedia,
  getMediaAsset,
  initUpload,
  uploadToStorage,
} from './api'

export type UploadStatus = 'uploading' | 'processing' | 'ready' | 'failed'

export interface UploadItem {
  localId: string
  fileName: string
  status: UploadStatus
  mediaAssetId?: number
  error?: string
}

const POLL_INTERVAL_MS = 1500
const POLL_TIMEOUT_MS = 60_000

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// Manages the full direct-to-storage upload flow for one or more files:
// init -> PUT to storage -> complete -> poll until Ready/Failed. Exposes the
// ready media asset ids so a form can attach them on submit.
export function useMediaUploads(context: MediaIntendedContext) {
  const [items, setItems] = useState<UploadItem[]>([])
  const mountedRef = useRef(true)
  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
    }
  }, [])

  const patch = useCallback((localId: string, changes: Partial<UploadItem>) => {
    if (!mountedRef.current) return
    setItems((prev) =>
      prev.map((item) => (item.localId === localId ? { ...item, ...changes } : item)),
    )
  }, [])

  const runUpload = useCallback(
    async (localId: string, file: File) => {
      try {
        const init = await initUpload({
          intendedContext: context,
          mimeType: file.type,
          fileName: file.name,
          sizeBytes: file.size,
        })
        patch(localId, { mediaAssetId: init.mediaAssetId })

        await uploadToStorage(init.uploadUrl, file)
        await completeUpload(init.mediaAssetId)
        patch(localId, { status: 'processing' })

        const deadline = Date.now() + POLL_TIMEOUT_MS
        while (Date.now() < deadline) {
          await sleep(POLL_INTERVAL_MS)
          if (!mountedRef.current) return
          const asset = await getMediaAsset(init.mediaAssetId)
          if (asset.processingStatus === MediaProcessingStatus.Ready) {
            patch(localId, { status: 'ready' })
            return
          }
          if (asset.processingStatus === MediaProcessingStatus.Failed) {
            patch(localId, { status: 'failed', error: asset.failureReason ?? 'Processing failed' })
            return
          }
        }
        patch(localId, { status: 'failed', error: 'Timed out while processing' })
      } catch (error) {
        patch(localId, { status: 'failed', error: getErrorMessage(error) })
      }
    },
    [context, patch],
  )

  const addFiles = useCallback(
    (files: FileList | File[]) => {
      const list = Array.from(files)
      for (const file of list) {
        const localId = `${Date.now()}-${Math.random().toString(36).slice(2)}`
        setItems((prev) => [
          ...prev,
          { localId, fileName: file.name, status: 'uploading' },
        ])
        void runUpload(localId, file)
      }
    },
    [runUpload],
  )

  const removeItem = useCallback((localId: string) => {
    setItems((prev) => {
      const target = prev.find((item) => item.localId === localId)
      // Best-effort cleanup of an uploaded-but-unattached asset.
      if (target?.mediaAssetId != null && target.status !== 'processing') {
        void deleteUnlinkedMedia(target.mediaAssetId).catch(() => {})
      }
      return prev.filter((item) => item.localId !== localId)
    })
  }, [])

  const reset = useCallback(() => setItems([]), [])

  const readyIds = items
    .filter((item) => item.status === 'ready' && item.mediaAssetId != null)
    .map((item) => item.mediaAssetId as number)

  const isUploading = items.some(
    (item) => item.status === 'uploading' || item.status === 'processing',
  )

  return { items, addFiles, removeItem, reset, readyIds, isUploading }
}
