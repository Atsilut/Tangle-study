import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createMapPin,
  getMapPinsInBounds,
  type MapBounds,
  type MapPinCreateRequest,
} from './api'
import { reverseGeocode, searchPlaces } from './places'

export const locationKeys = {
  all: ['location'] as const,
  pins: (bounds: MapBounds | null) =>
    [...locationKeys.all, 'pins', bounds] as const,
  reverse: (latitude: number, longitude: number) =>
    [...locationKeys.all, 'reverse', latitude, longitude] as const,
}

export function useMapPins(bounds: MapBounds | null) {
  return useQuery({
    queryKey: locationKeys.pins(bounds),
    queryFn: () => getMapPinsInBounds(bounds as MapBounds),
    enabled: bounds != null,
    staleTime: 30_000,
    placeholderData: (previous) => previous,
  })
}

export function useCreateMapPin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: MapPinCreateRequest) => createMapPin(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: locationKeys.all }),
  })
}

export function usePlaceSearchQuery(query: string) {
  const trimmed = query.trim()
  return useQuery({
    queryKey: [...locationKeys.all, 'search', trimmed] as const,
    queryFn: () => searchPlaces(trimmed),
    enabled: trimmed.length >= 2,
    staleTime: 60_000,
  })
}

export function usePlaceReverse(latitude: number | null, longitude: number | null) {
  return useQuery({
    queryKey: locationKeys.reverse(latitude ?? 0, longitude ?? 0),
    queryFn: () => reverseGeocode(latitude as number, longitude as number),
    enabled: latitude != null && longitude != null,
    staleTime: 5 * 60 * 1000,
  })
}
