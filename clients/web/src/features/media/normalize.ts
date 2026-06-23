import {
  MediaIntendedContext,
  MediaKind,
  MediaProcessingStatus,
  type MediaAsset,
} from '@/types/api'

/** Coerce API / SignalR media payloads (camelCase or PascalCase) into MediaAsset. */
export function normalizeMediaAsset(raw: unknown): MediaAsset | null {
  if (!raw || typeof raw !== 'object') return null
  const m = raw as Record<string, unknown>
  const id = Number(m.id ?? m.Id)
  if (!Number.isFinite(id)) return null

  return {
    id,
    kind: Number(m.kind ?? m.Kind ?? MediaKind.Image) as MediaKind,
    intendedContext: Number(m.intendedContext ?? m.IntendedContext ?? 0) as MediaIntendedContext,
    processingStatus: Number(
      m.processingStatus ?? m.ProcessingStatus ?? MediaProcessingStatus.PendingUpload,
    ) as MediaProcessingStatus,
    mimeType: String(m.mimeType ?? m.MimeType ?? ''),
    originalFileName: String(m.originalFileName ?? m.OriginalFileName ?? ''),
    originalSizeBytes: Number(m.originalSizeBytes ?? m.OriginalSizeBytes ?? 0),
    storedSizeBytes:
      m.storedSizeBytes != null || m.StoredSizeBytes != null
        ? Number(m.storedSizeBytes ?? m.StoredSizeBytes)
        : undefined,
    failureReason:
      m.failureReason != null || m.FailureReason != null
        ? String(m.failureReason ?? m.FailureReason)
        : undefined,
    postId:
      m.postId != null || m.PostId != null ? Number(m.postId ?? m.PostId) : undefined,
    commentId:
      m.commentId != null || m.CommentId != null ? Number(m.commentId ?? m.CommentId) : undefined,
    chatMessageId:
      m.chatMessageId != null || m.ChatMessageId != null
        ? Number(m.chatMessageId ?? m.ChatMessageId)
        : undefined,
    createdAt: String(m.createdAt ?? m.CreatedAt ?? ''),
    updatedAt: String(m.updatedAt ?? m.UpdatedAt ?? ''),
  }
}

export function isMediaReady(asset: MediaAsset): boolean {
  return Number(asset.processingStatus) === MediaProcessingStatus.Ready
}

export function mediaContentUrl(id: number): string {
  return `/api/media/${id}/content`
}
