import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createMapPin,
  getActiveGroupLocations,
  getGroupMemberSharingStatus,
  getMapClustersInBounds,
  getMapPinsInBounds,
  getMyLocationSession,
  startLocationSession,
  stopLocationSession,
  triggerLocationSos,
  updateLocationSessionPosition,
  MIN_CLUSTER_ZOOM,
  MAX_CLUSTER_ZOOM,
  type LocationPositionUpdateRequest,
  type LocationSessionCreateRequest,
  type MapBounds,
  type MapPinCreateRequest,
} from './api'
import { reverseGeocode, searchPlaces } from './places'

export const locationKeys = {
  all: ['location'] as const,
  pins: (bounds: MapBounds | null) =>
    [...locationKeys.all, 'pins', bounds] as const,
  clusters: (bounds: MapBounds | null, zoom: number | null) =>
    [...locationKeys.all, 'clusters', bounds, zoom] as const,
  reverse: (latitude: number, longitude: number) =>
    [...locationKeys.all, 'reverse', latitude, longitude] as const,
  mySession: (groupId: number | null) =>
    [...locationKeys.all, 'sessions', 'mine', groupId] as const,
  activeGroup: (groupId: number | null) =>
    [...locationKeys.all, 'sessions', 'active', groupId] as const,
  memberStatus: (groupId: number | null) =>
    [...locationKeys.all, 'sessions', 'members', groupId] as const,
}

export function useMapClusters(bounds: MapBounds | null, zoom: number | null) {
  const clusterZoom =
    zoom != null
      ? Math.min(MAX_CLUSTER_ZOOM, Math.max(MIN_CLUSTER_ZOOM, Math.floor(zoom)))
      : null

  return useQuery({
    queryKey: locationKeys.clusters(bounds, clusterZoom),
    queryFn: () => getMapClustersInBounds(bounds as MapBounds, clusterZoom as number),
    enabled: bounds != null && clusterZoom != null,
    staleTime: 30_000,
    refetchInterval: (query) =>
      query.state.fetchStatus === 'idle' && (query.state.data?.length ?? 0) === 0 ? 2000 : false,
    placeholderData: (previous) => previous,
  })
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

export function useTriggerLocationSos() {
  return useMutation({
    mutationFn: (sessionId: number) => triggerLocationSos(sessionId),
  })
}

export function useCreateMapPin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: MapPinCreateRequest) => createMapPin(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: locationKeys.all }),
  })
}

export function useMyLocationSession(groupId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: locationKeys.mySession(groupId),
    queryFn: () => getMyLocationSession(groupId as number),
    enabled: enabled && groupId != null,
    staleTime: 10_000,
    placeholderData: (previous) => previous,
  })
}

export function useActiveGroupLocations(groupId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: locationKeys.activeGroup(groupId),
    queryFn: () => getActiveGroupLocations(groupId as number),
    enabled: enabled && groupId != null,
    staleTime: 15_000,
    refetchInterval: 30_000,
  })
}

export function useGroupMemberSharingStatus(groupId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: locationKeys.memberStatus(groupId),
    queryFn: () => getGroupMemberSharingStatus(groupId as number),
    enabled: enabled && groupId != null,
    staleTime: 15_000,
    refetchInterval: 30_000,
  })
}

export function useStartLocationSession(groupId: number | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: Omit<LocationSessionCreateRequest, 'groupId'>) =>
      startLocationSession({ ...body, groupId: groupId as number }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: locationKeys.mySession(groupId) })
      queryClient.invalidateQueries({ queryKey: locationKeys.activeGroup(groupId) })
      queryClient.invalidateQueries({ queryKey: locationKeys.memberStatus(groupId) })
    },
  })
}

export function useUpdateLocationSessionPosition(groupId: number | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      sessionId,
      body,
    }: {
      sessionId: number
      body: LocationPositionUpdateRequest
    }) => updateLocationSessionPosition(sessionId, body),
    onSuccess: (updated) => {
      queryClient.setQueryData(locationKeys.mySession(groupId), updated)
      queryClient.invalidateQueries({ queryKey: locationKeys.activeGroup(groupId) })
      queryClient.invalidateQueries({ queryKey: locationKeys.memberStatus(groupId) })
    },
  })
}

export function useStopLocationSession(groupId: number | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (sessionId: number) => stopLocationSession(sessionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: locationKeys.mySession(groupId) })
      queryClient.invalidateQueries({ queryKey: locationKeys.activeGroup(groupId) })
      queryClient.invalidateQueries({ queryKey: locationKeys.memberStatus(groupId) })
    },
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
