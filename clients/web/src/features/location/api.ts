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

export type LocationSafetyAlertType = 'StalePosition' | 'Sos'

export interface LocationSafetyAlert {
  type: LocationSafetyAlertType
  groupId: number
  sessionId: number
  userId: number
  userNickname: string
  latitude: number | null
  longitude: number | null
  occurredAt: string
  message: string
}

export interface GroupMemberLocationStatus {
  userId: number
  userNickname: string
  isSharing: boolean
  sessionId: number | null
  latitude: number | null
  longitude: number | null
  updatedAt: string | null
}

export interface LocationSession {
  id: number
  groupId: number
  userId: number
  userNickname: string
  latitude: number
  longitude: number
  startedAt: string
  positionUpdatedAt: string
}

export interface LiveLocation {
  sessionId: number
  groupId: number
  userId: number
  userNickname: string
  latitude: number
  longitude: number
  updatedAt: string
}

export interface LocationSessionCreateRequest {
  groupId: number
  latitude: number
  longitude: number
}

export interface LocationPositionUpdateRequest {
  latitude: number
  longitude: number
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

// POST /api/location/sessions (JWT) -> 201 session
export async function startLocationSession(
  body: LocationSessionCreateRequest,
): Promise<LocationSession> {
  const res = await api.post<LocationSession>('/location/sessions', body)
  return res.data
}

// GET /api/location/sessions/mine?groupId= (JWT) -> 200 session | 204 none
export async function getMyLocationSession(groupId: number): Promise<LocationSession | null> {
  const res = await api.get<LocationSession>('/location/sessions/mine', { params: { groupId } })
  if (res.status === 204) return null
  return res.data
}

// GET /api/location/sessions/active?groupId= (JWT) -> 200 list | 204 empty
export function getActiveGroupLocations(groupId: number): Promise<LiveLocation[]> {
  return getList<LiveLocation>('/location/sessions/active', { params: { groupId } })
}

// GET /api/location/sessions/members?groupId= (JWT) -> 200 list (may be empty)
export function getGroupMemberSharingStatus(groupId: number): Promise<GroupMemberLocationStatus[]> {
  return getList<GroupMemberLocationStatus>('/location/sessions/members', { params: { groupId } })
}

// PATCH /api/location/sessions/{id}/position (JWT) -> 200 session
export async function updateLocationSessionPosition(
  sessionId: number,
  body: LocationPositionUpdateRequest,
): Promise<LocationSession> {
  const res = await api.patch<LocationSession>(`/location/sessions/${sessionId}/position`, body)
  return res.data
}

// DELETE /api/location/sessions/{id} (JWT) -> 204
export async function stopLocationSession(sessionId: number): Promise<void> {
  await api.delete(`/location/sessions/${sessionId}`)
}

// POST /api/location/sessions/{id}/sos (JWT) -> 200 alert
export async function triggerLocationSos(sessionId: number): Promise<LocationSafetyAlert> {
  const res = await api.post<LocationSafetyAlert>(`/location/sessions/${sessionId}/sos`)
  return res.data
}
