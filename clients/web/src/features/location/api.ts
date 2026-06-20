import { api, getList } from '@/lib/apiClient'

export interface MapPin {
  id: number
  latitude: number
  longitude: number
  ownerUserId: number
  ownerNickname: string
  postId: number | null
  createdAt: string
  updatedAt: string
}

export interface MapBounds {
  minLatitude: number
  maxLatitude: number
  minLongitude: number
  maxLongitude: number
}

export interface MapPinCreateRequest {
  latitude: number
  longitude: number
  postId?: number | null
}

export interface MapCluster {
  latitude: number
  longitude: number
  pinCount: number
  samplePinId: number | null
}

/** Individual pins load at city/regional zoom and closer. */
export const MIN_PIN_FETCH_ZOOM = 5

/** Clusters only at continent/country zoom (below pin fetch). */
export const MIN_CLUSTER_ZOOM = 2
export const MAX_CLUSTER_ZOOM = 4

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

export function sanitizeBoundsForQuery(bounds: MapBounds): MapBounds | null {
  const minLatitude = clamp(bounds.minLatitude, -90, 90)
  const maxLatitude = clamp(bounds.maxLatitude, -90, 90)
  const minLongitude = clamp(bounds.minLongitude, -180, 180)
  const maxLongitude = clamp(bounds.maxLongitude, -180, 180)

  if (minLatitude >= maxLatitude || minLongitude >= maxLongitude) return null

  return { minLatitude, maxLatitude, minLongitude, maxLongitude }
}

export function buildMapPinBoundsQuery(bounds: MapBounds): string {
  const format = (value: number) => value.toFixed(6)
  const params = new URLSearchParams({
    minLatitude: format(bounds.minLatitude),
    maxLatitude: format(bounds.maxLatitude),
    minLongitude: format(bounds.minLongitude),
    maxLongitude: format(bounds.maxLongitude),
  })
  return params.toString()
}

// GET /api/location/pins?minLatitude&maxLatitude&minLongitude&maxLongitude -> 200 list | 204 empty
export function getMapPinsInBounds(bounds: MapBounds): Promise<MapPin[]> {
  return getList<MapPin>(`/location/pins?${buildMapPinBoundsQuery(bounds)}`)
}

function buildMapClusterBoundsQuery(bounds: MapBounds, zoom: number): string {
  const format = (value: number) => value.toFixed(6)
  const params = new URLSearchParams({
    minLatitude: format(bounds.minLatitude),
    maxLatitude: format(bounds.maxLatitude),
    minLongitude: format(bounds.minLongitude),
    maxLongitude: format(bounds.maxLongitude),
    zoom: String(zoom),
  })
  return params.toString()
}

// GET /api/location/clusters?bbox&zoom -> 200 list | 204 empty (worker may still be computing)
export function getMapClustersInBounds(bounds: MapBounds, zoom: number): Promise<MapCluster[]> {
  return getList<MapCluster>(`/location/clusters?${buildMapClusterBoundsQuery(bounds, zoom)}`)
}

// POST /api/location/pins (JWT) -> 201 with pin body
export async function createMapPin(body: MapPinCreateRequest): Promise<MapPin> {
  const res = await api.post<MapPin>('/location/pins', body)
  return res.data
}
