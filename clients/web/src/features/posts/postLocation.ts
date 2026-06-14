import type { PostLocation } from './api'

export function buildMapUrl(latitude: number, longitude: number): string {
  return `/map?lat=${latitude}&lng=${longitude}`
}

export function parsePostLocation(raw: unknown): PostLocation | null {
  if (raw == null || typeof raw !== 'object') return null

  const record = raw as Record<string, unknown>
  const lat = record.latitude ?? record.Latitude
  const lng = record.longitude ?? record.Longitude
  const latitude = typeof lat === 'number' ? lat : typeof lat === 'string' ? Number(lat) : Number.NaN
  const longitude =
    typeof lng === 'number' ? lng : typeof lng === 'string' ? Number(lng) : Number.NaN

  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null
  return { latitude, longitude }
}
