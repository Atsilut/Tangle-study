import { api, getList } from '@/lib/apiClient'

export interface Place {
  placeId: string
  displayName: string
  latitude: number
  longitude: number
}

export function buildPlaceSearchQuery(query: string, limit = 5): string {
  const params = new URLSearchParams({
    q: query,
    limit: String(limit),
  })
  return params.toString()
}

export function buildPlaceReverseQuery(latitude: number, longitude: number): string {
  const format = (value: number) => value.toFixed(6)
  const params = new URLSearchParams({
    latitude: format(latitude),
    longitude: format(longitude),
  })
  return params.toString()
}

export function parsePlace(raw: Place): Place {
  return {
    placeId: raw.placeId,
    displayName: raw.displayName,
    latitude: raw.latitude,
    longitude: raw.longitude,
  }
}

// GET /api/location/places/search?q&limit -> 200 list | 204 empty
export async function searchPlaces(query: string, limit = 5): Promise<Place[]> {
  const trimmed = query.trim()
  if (trimmed.length < 2) return []

  const results = await getList<Place>(`/location/places/search?${buildPlaceSearchQuery(trimmed, limit)}`)
  return results.map(parsePlace)
}

// GET /api/location/places/reverse?latitude&longitude -> 200 body | 204 empty
export async function reverseGeocode(latitude: number, longitude: number): Promise<string | null> {
  const res = await api.get<PlaceReverseResponse | ''>(
    `/location/places/reverse?${buildPlaceReverseQuery(latitude, longitude)}`,
  )
  if (res.status === 204 || !res.data) return null
  return res.data.displayName?.trim() || null
}

interface PlaceReverseResponse {
  displayName: string
}
