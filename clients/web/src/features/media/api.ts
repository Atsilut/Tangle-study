import axios from 'axios'
import { api } from '@/lib/apiClient'
import type { MediaAsset, MediaIntendedContext } from '@/types/api'

export interface MediaUploadInit {
  mediaAssetId: number
  uploadUrl: string
  objectKey: string
  expiresAt: string
  ingressLimitBytes: number
  storageLimitBytes: number
}

export interface MediaUploadInitRequest {
  intendedContext: MediaIntendedContext
  mimeType: string
  fileName: string
  sizeBytes: number
}

// POST /api/media/upload-init -> presigned direct-to-storage upload
export async function initUpload(body: MediaUploadInitRequest): Promise<MediaUploadInit> {
  const res = await api.post<MediaUploadInit>('/media/upload-init', body, {
    treatUnauthorizedAsForbidden: true,
  })
  return res.data
}

// Azurite has no CORS rules; local presigned URLs target :10000 on the host.
// Rewrite them to same-origin so Vite/Nginx can proxy to Azurite without a
// browser preflight failure. Production Azure Blob URLs are left unchanged.
function normalizeAzuritePath(pathname: string): string {
  // PublicBlobEndpoint used to include /devstoreaccount1, duplicating the SAS path.
  return pathname.replace(/\/devstoreaccount1\/devstoreaccount1\//, '/devstoreaccount1/')
}

export function resolveStorageUploadUrl(uploadUrl: string): string {
  try {
    const parsed = new URL(uploadUrl)
    if (parsed.port === '10000' && (parsed.hostname === '127.0.0.1' || parsed.hostname === 'localhost')) {
      return `${window.location.origin}${normalizeAzuritePath(parsed.pathname)}${parsed.search}`
    }
  } catch {
    // Use the original URL when parsing fails.
  }
  return uploadUrl
}

// Direct PUT of the raw bytes to the presigned URL (Azure Blob SAS). This goes
// straight to storage, not through /api, and must not carry our JWT.
export async function uploadToStorage(uploadUrl: string, file: File): Promise<void> {
  await axios.put(resolveStorageUploadUrl(uploadUrl), file, {
    headers: {
      'x-ms-blob-type': 'BlockBlob',
      'Content-Type': file.type,
    },
  })
}

// POST /api/media/{id}/complete -> confirms upload and enqueues processing
export async function completeUpload(id: number): Promise<MediaAsset> {
  const res = await api.post<MediaAsset>(`/media/${id}/complete`, undefined, {
    treatUnauthorizedAsForbidden: true,
  })
  return res.data
}

// GET /api/media/{id} -> metadata + processing status (poll until ready/failed)
export async function getMediaAsset(id: number): Promise<MediaAsset> {
  const res = await api.get<MediaAsset>(`/media/${id}`, {
    treatUnauthorizedAsForbidden: true,
  })
  return res.data
}

// DELETE /api/media/{id} -> remove an unlinked asset owned by the caller
export async function deleteUnlinkedMedia(id: number): Promise<void> {
  await api.delete(`/media/${id}`, { treatUnauthorizedAsForbidden: true })
}
