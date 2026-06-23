import { describe, expect, it } from 'vitest'
import { MediaKind, MediaProcessingStatus } from '@/types/api'
import { normalizeMediaAsset } from './normalize'

describe('normalizeMediaAsset', () => {
  it('returns null for non-objects', () => {
    expect(normalizeMediaAsset(null)).toBeNull()
    expect(normalizeMediaAsset('bad')).toBeNull()
  })

  it('normalizes camelCase media', () => {
    const asset = normalizeMediaAsset({
      id: 10,
      kind: MediaKind.Image,
      intendedContext: 1,
      processingStatus: MediaProcessingStatus.Ready,
      mimeType: 'image/png',
      originalFileName: 'photo.png',
      originalSizeBytes: 100,
      createdAt: '2026-06-13T12:00:00Z',
      updatedAt: '2026-06-13T12:00:00Z',
    })

    expect(asset?.id).toBe(10)
    expect(asset?.mimeType).toBe('image/png')
  })

  it('normalizes PascalCase media', () => {
    const asset = normalizeMediaAsset({
      Id: 11,
      Kind: MediaKind.Video,
      MimeType: 'video/mp4',
      OriginalFileName: 'clip.mp4',
      OriginalSizeBytes: 200,
      ProcessingStatus: MediaProcessingStatus.Ready,
      CreatedAt: '2026-06-13T12:00:00Z',
      UpdatedAt: '2026-06-13T12:00:00Z',
    })

    expect(asset?.id).toBe(11)
    expect(asset?.kind).toBe(MediaKind.Video)
  })
})
