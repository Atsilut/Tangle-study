import type { Map as MapLibreMap } from 'maplibre-gl'
import type { FeatureCollection, Point } from 'geojson'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import Map, {
  Layer,
  Marker,
  NavigationControl,
  Popup,
  Source,
  type MapLayerMouseEvent,
  type MapRef,
} from 'react-map-gl/maplibre'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { maplibregl } from '@/lib/maplibreSetup'
import { useAuthStore } from '@/stores/authStore'
import type { MapBounds, MapCluster, MapPin } from '../api'
import { MIN_PIN_FETCH_ZOOM, sanitizeBoundsForQuery } from '../api'
import { useCreateMapPin, useMapClusters, useMapPins, usePlaceReverse } from '../hooks'
import { useLiveGroupLocations } from '../useLiveGroupLocations'
import { useMyGeolocation } from '../useMyGeolocation'
import { MyLocationMarker } from './MyLocationMarker'
import { LiveGroupLocationMarker } from './LiveGroupLocationMarker'
import { OSM_RASTER_STYLE, OSM_TILE_MAX_ZOOM } from '../mapStyle'
import type { Place } from '../places'
import type { LiveLocation } from '../api'
import 'maplibre-gl/dist/maplibre-gl.css'

const INITIAL_VIEW = {
  longitude: 126.978,
  latitude: 37.5665,
  zoom: 11,
} as const

const FLY_TO_ZOOM = 14
const BOUNDS_DEBOUNCE_MS = 300
const PINS_LAYER_ID = 'memory-map-pins'
const CLUSTERS_LAYER_ID = 'memory-map-clusters'
const CLUSTER_CLICK_ZOOM_STEP = 2
const LOCATE_ZOOM = 14

function boundsFromMap(map: MapLibreMap): MapBounds {
  const box = map.getBounds()
  return {
    minLatitude: box.getSouth(),
    maxLatitude: box.getNorth(),
    minLongitude: box.getWest(),
    maxLongitude: box.getEast(),
  }
}

function quantizeBounds(bounds: MapBounds): MapBounds {
  const q = (value: number) => Number(value.toFixed(4))
  return {
    minLatitude: q(bounds.minLatitude),
    maxLatitude: q(bounds.maxLatitude),
    minLongitude: q(bounds.minLongitude),
    maxLongitude: q(bounds.maxLongitude),
  }
}

function pinsToGeoJson(pins: MapPin[]): FeatureCollection<Point> {
  return {
    type: 'FeatureCollection',
    features: pins.map((pin) => ({
      type: 'Feature',
      id: pin.id,
      properties: { id: pin.id, kind: 'pin' },
      geometry: {
        type: 'Point',
        coordinates: [pin.longitude, pin.latitude],
      },
    })),
  }
}

function clustersToGeoJson(clusters: MapCluster[]): FeatureCollection<Point> {
  return {
    type: 'FeatureCollection',
    features: clusters.map((cluster, index) => ({
      type: 'Feature',
      id: `cluster-${index}`,
      properties: {
        clusterId: index,
        pinCount: cluster.pinCount,
        kind: 'cluster',
      },
      geometry: {
        type: 'Point',
        coordinates: [cluster.longitude, cluster.latitude],
      },
    })),
  }
}

export interface MemoryMapProps {
  flyToPlace?: Place | null
  groupId?: number | null
}

export function MemoryMap({ flyToPlace = null, groupId = null }: MemoryMapProps) {
  const mapRef = useRef<MapRef>(null)
  const hasCenteredOnUser = useRef(false)
  const boundsDebounceRef = useRef<number | null>(null)
  const [bounds, setBounds] = useState<MapBounds | null>(null)
  const [clusterBounds, setClusterBounds] = useState<MapBounds | null>(null)
  const [mapZoom, setMapZoom] = useState<number>(INITIAL_VIEW.zoom)
  const [selectedPin, setSelectedPin] = useState<MapPin | null>(null)
  const [selectedLive, setSelectedLive] = useState<LiveLocation | null>(null)
  const [showMyLocationPopup, setShowMyLocationPopup] = useState(false)
  const [mapError, setMapError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const { data: pins = [], isFetching, isError, error, refetch } = useMapPins(bounds)
  const {
    data: clusters = [],
    isFetching: isFetchingClusters,
    isError: isClusterError,
    error: clusterError,
    refetch: refetchClusters,
  } = useMapClusters(clusterBounds, mapZoom)
  const visiblePins = useMemo(() => (bounds == null ? [] : pins), [bounds, pins])
  const visibleClusters = useMemo(
    () => (clusterBounds == null ? [] : clusters),
    [clusterBounds, clusters],
  )
  const pinsGeoJson = useMemo(() => pinsToGeoJson(visiblePins), [visiblePins])
  const clustersGeoJson = useMemo(() => clustersToGeoJson(visibleClusters), [visibleClusters])
  const showPinLayer = bounds != null
  const showClusterLayer = clusterBounds != null
  const createPin = useCreateMapPin()
  const liveLocations = useLiveGroupLocations(groupId, isAuthenticated && groupId != null)
  const { position: myPosition, error: myLocationError, refresh: refreshMyLocation } =
    useMyGeolocation(true)
  const { data: selectedPlaceName } = usePlaceReverse(
    isAuthenticated ? selectedPin?.latitude ?? null : null,
    isAuthenticated ? selectedPin?.longitude ?? null : null,
  )

  const syncBounds = useCallback(() => {
    const map = mapRef.current?.getMap()
    if (!map) return

    const zoom = map.getZoom()
    setMapZoom(zoom)

    const next = sanitizeBoundsForQuery(quantizeBounds(boundsFromMap(map)))
    if (next == null) {
      setBounds(null)
      setClusterBounds(null)
      return
    }

    if (zoom < MIN_PIN_FETCH_ZOOM) {
      setBounds(null)
      setClusterBounds((prev) =>
        prev &&
        prev.minLatitude === next.minLatitude &&
        prev.maxLatitude === next.maxLatitude &&
        prev.minLongitude === next.minLongitude &&
        prev.maxLongitude === next.maxLongitude
          ? prev
          : next,
      )
      return
    }

    setClusterBounds(null)
    setBounds((prev) =>
      prev &&
      prev.minLatitude === next.minLatitude &&
      prev.maxLatitude === next.maxLatitude &&
      prev.minLongitude === next.minLongitude &&
      prev.maxLongitude === next.maxLongitude
        ? prev
        : next,
    )
  }, [])

  const scheduleBoundsSync = useCallback(() => {
    if (boundsDebounceRef.current != null) {
      window.clearTimeout(boundsDebounceRef.current)
    }
    boundsDebounceRef.current = window.setTimeout(() => {
      boundsDebounceRef.current = null
      syncBounds()
    }, BOUNDS_DEBOUNCE_MS)
  }, [syncBounds])

  useEffect(
    () => () => {
      if (boundsDebounceRef.current != null) {
        window.clearTimeout(boundsDebounceRef.current)
      }
    },
    [],
  )

  useEffect(() => {
    if (!myPosition || flyToPlace || hasCenteredOnUser.current) return
    hasCenteredOnUser.current = true
    mapRef.current?.flyTo({
      center: [myPosition.longitude, myPosition.latitude],
      zoom: LOCATE_ZOOM,
      duration: 1200,
    })
  }, [myPosition, flyToPlace])

  const handleLocateMe = useCallback(async () => {
    setShowMyLocationPopup(false)
    try {
      const coords = myPosition ?? (await refreshMyLocation())
      mapRef.current?.flyTo({
        center: [coords.longitude, coords.latitude],
        zoom: LOCATE_ZOOM,
        duration: 800,
      })
      setShowMyLocationPopup(true)
    } catch {
      // Error state is surfaced via myLocationError.
    }
  }, [myPosition, refreshMyLocation])

  const handleMapClick = useCallback(
    (event: MapLayerMouseEvent) => {
      const feature = event.features?.[0]
      if (feature?.properties?.kind === 'cluster') {
        const clusterId = Number(feature.properties.clusterId)
        const cluster = visibleClusters[clusterId]
        if (cluster) {
          setSelectedPin(null)
          setSelectedLive(null)
          setShowMyLocationPopup(false)
          mapRef.current?.flyTo({
            center: [cluster.longitude, cluster.latitude],
            zoom: Math.min(mapZoom + CLUSTER_CLICK_ZOOM_STEP, MIN_PIN_FETCH_ZOOM),
            duration: 800,
          })
        }
        return
      }

      if (feature?.properties?.id != null) {
        const pinId = Number(feature.properties.id)
        setSelectedLive(null)
        setShowMyLocationPopup(false)
        setSelectedPin(visiblePins.find((pin) => pin.id === pinId) ?? null)
        return
      }
      setSelectedPin(null)
      setSelectedLive(null)
      setShowMyLocationPopup(false)
    },
    [mapZoom, visibleClusters, visiblePins],
  )

  useEffect(() => {
    if (!flyToPlace) return
    const map = mapRef.current
    if (!map) return

    const clearSelection = () => {
      setSelectedPin(null)
      setSelectedLive(null)
      setShowMyLocationPopup(false)
    }
    map.once('moveend', clearSelection)
    map.flyTo({
      center: [flyToPlace.longitude, flyToPlace.latitude],
      zoom: FLY_TO_ZOOM,
      duration: 1200,
    })

    return () => {
      map.off('moveend', clearSelection)
    }
  }, [flyToPlace])

  const handleDropPin = useCallback(
    (latitude: number, longitude: number) => {
      if (!isAuthenticated) return
      setActionError(null)
      createPin.mutate(
        { latitude, longitude },
        {
          onError: (err) => setActionError(getErrorMessage(err, 'Could not create pin.')),
        },
      )
    },
    [createPin, isAuthenticated],
  )

  return (
    <div className="flex flex-col gap-2">
      <div className="relative h-[min(70vh,560px)] min-h-[360px] overflow-hidden rounded-lg border border-gray-200">
        <div className="absolute inset-0 [&_.maplibregl-ctrl-attrib]:hidden [&_.maplibregl-ctrl-logo]:hidden [&_.maplibregl-canvas]:outline-none">
          <Map
            ref={mapRef}
            mapLib={maplibregl}
            initialViewState={INITIAL_VIEW}
            mapStyle={OSM_RASTER_STYLE}
            minZoom={2}
            maxZoom={OSM_TILE_MAX_ZOOM}
            attributionControl={false}
            interactiveLayerIds={[CLUSTERS_LAYER_ID, PINS_LAYER_ID]}
            style={{ width: '100%', height: '100%' }}
            onLoad={syncBounds}
            onMoveEnd={scheduleBoundsSync}
            onClick={handleMapClick}
            onDblClick={(event) => {
              handleDropPin(event.lngLat.lat, event.lngLat.lng)
            }}
            onMouseEnter={(event) => {
              if (
                event.features?.some(
                  (feature) =>
                    feature.layer.id === PINS_LAYER_ID ||
                    feature.layer.id === CLUSTERS_LAYER_ID,
                )
              ) {
                event.target.getCanvas().style.cursor = 'pointer'
              }
            }}
            onMouseLeave={(event) => {
              event.target.getCanvas().style.cursor = ''
            }}
            onError={(event) => {
              const message = event.error?.message ?? ''
              // OSM tiles stop at z19; ignore stale requests if the viewport already corrected.
              if (message.includes('tile.openstreetmap.org')) return
              setMapError(message || 'Map failed to load tiles.')
            }}
          >
            <NavigationControl position="top-right" />
            {showClusterLayer && (
              <Source id="memory-map-clusters" type="geojson" data={clustersGeoJson}>
                <Layer
                  id={CLUSTERS_LAYER_ID}
                  type="circle"
                  paint={{
                    'circle-radius': [
                      'step',
                      ['get', 'pinCount'],
                      14,
                      10,
                      18,
                      50,
                      24,
                    ],
                    'circle-color': '#1d4ed8',
                    'circle-stroke-width': 2,
                    'circle-stroke-color': '#ffffff',
                  }}
                />
              </Source>
            )}
            {showPinLayer && (
              <Source id="memory-map-pins" type="geojson" data={pinsGeoJson}>
                <Layer
                  id={PINS_LAYER_ID}
                  type="circle"
                  paint={{
                    'circle-radius': 8,
                    'circle-color': '#2563eb',
                    'circle-stroke-width': 2,
                    'circle-stroke-color': '#ffffff',
                  }}
                />
              </Source>
            )}
            {isAuthenticated &&
              groupId != null &&
              liveLocations.map((location) => (
                <Marker
                  key={location.sessionId}
                  longitude={location.longitude}
                  latitude={location.latitude}
                  anchor="center"
                  onClick={(event) => {
                    event.originalEvent.stopPropagation()
                    setSelectedPin(null)
                    setShowMyLocationPopup(false)
                    setSelectedLive(location)
                  }}
                >
                  <LiveGroupLocationMarker
                    nickname={location.userNickname}
                    selected={selectedLive?.sessionId === location.sessionId}
                    animationDelayMs={(location.userId % 5) * 400}
                  />
                </Marker>
              ))}
            {myPosition && (
              <Marker
                longitude={myPosition.longitude}
                latitude={myPosition.latitude}
                anchor="center"
                onClick={(event) => {
                  event.originalEvent.stopPropagation()
                  setSelectedPin(null)
                  setSelectedLive(null)
                  setShowMyLocationPopup(true)
                }}
              >
                <MyLocationMarker />
              </Marker>
            )}
            {selectedPin && (
              <Popup
                longitude={selectedPin.longitude}
                latitude={selectedPin.latitude}
                anchor="top"
                closeButton={false}
                closeOnClick={false}
                onClose={() => setSelectedPin(null)}
              >
                <div className="flex flex-col gap-1 text-sm">
                  <p className="font-medium text-gray-900">{selectedPin.ownerNickname}</p>
                  {selectedPlaceName && <p className="text-gray-600">{selectedPlaceName}</p>}
                  <p className="text-gray-500">
                    {selectedPin.latitude.toFixed(4)}, {selectedPin.longitude.toFixed(4)}
                  </p>
                  {selectedPin.postId != null ? (
                    <Link
                      to={`/posts/${selectedPin.postId}`}
                      className="font-medium text-blue-700 hover:underline"
                    >
                      View post
                    </Link>
                  ) : (
                    <p className="text-gray-500">Standalone pin</p>
                  )}
                </div>
              </Popup>
            )}
            {selectedLive && (
              <Popup
                longitude={selectedLive.longitude}
                latitude={selectedLive.latitude}
                anchor="top"
                closeButton={false}
                closeOnClick={false}
                onClose={() => setSelectedLive(null)}
              >
                <div className="flex flex-col gap-1 text-sm">
                  <p className="font-medium text-gray-900">{selectedLive.userNickname}</p>
                  <p className="text-gray-500">Live location</p>
                  <p className="text-gray-500">
                    {selectedLive.latitude.toFixed(4)}, {selectedLive.longitude.toFixed(4)}
                  </p>
                </div>
              </Popup>
            )}
            {showMyLocationPopup && myPosition && (
              <Popup
                longitude={myPosition.longitude}
                latitude={myPosition.latitude}
                anchor="top"
                closeButton={false}
                closeOnClick={false}
                onClose={() => setShowMyLocationPopup(false)}
              >
                <div className="flex flex-col gap-1 text-sm">
                  <p className="font-medium text-gray-900">You are here</p>
                  <p className="text-gray-500">
                    {myPosition.latitude.toFixed(4)}, {myPosition.longitude.toFixed(4)}
                  </p>
                </div>
              </Popup>
            )}
          </Map>
        </div>
        <button
          type="button"
          aria-label="Center map on my location"
          title="My location"
          onClick={() => void handleLocateMe()}
          className="absolute top-[6.5rem] right-2.5 z-10 flex h-[29px] w-[29px] items-center justify-center rounded border border-black/20 bg-white text-sky-600 shadow-sm hover:bg-gray-50"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4 w-4 fill-current">
            <path d="M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8Zm8.94 3h-2.02a6.98 6.98 0 0 0-1.27-3.01l1.43-1.43a1 1 0 0 0-1.41-1.41l-1.43 1.43A6.98 6.98 0 0 0 15 4.08V2.06a1 1 0 1 0-2 0v2.02a6.98 6.98 0 0 0-3.01 1.27L8.56 4.92A1 1 0 0 0 7.15 6.33l1.43 1.43A6.98 6.98 0 0 0 7.31 11H5.29a1 1 0 1 0 0 2h2.02c.28 1.12.78 2.15 1.44 3.01l-1.43 1.43a1 1 0 1 0 1.41 1.41l1.43-1.43A6.98 6.98 0 0 0 11 18.92v2.02a1 1 0 1 0 2 0v-2.02a6.98 6.98 0 0 0 3.01-1.27l1.43 1.43a1 1 0 0 0 1.41-1.41l-1.43-1.43A6.98 6.98 0 0 0 18.69 13h2.02a1 1 0 1 0 0-2Z" />
          </svg>
        </button>
        {isFetching && bounds != null && (
          <p className="pointer-events-none absolute bottom-3 left-3 z-10 rounded-md bg-white/90 px-2 py-1 text-xs text-gray-600 shadow">
            Loading pins…
          </p>
        )}
        {isFetchingClusters && clusterBounds != null && (
          <p className="pointer-events-none absolute bottom-3 left-3 z-10 rounded-md bg-white/90 px-2 py-1 text-xs text-gray-600 shadow">
            Loading clusters…
          </p>
        )}
        {createPin.isPending && (
          <p className="pointer-events-none absolute bottom-10 left-3 z-10 rounded-md bg-white/90 px-2 py-1 text-xs text-gray-600 shadow">
            Saving pin…
          </p>
        )}
      </div>
      {myLocationError && (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          Location unavailable: {myLocationError}. Allow location access in your browser, then use
          the target button on the map.
        </div>
      )}
      {mapError && (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          {mapError}
        </div>
      )}
      {actionError && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
          <span>{actionError}</span>
          <Button size="sm" variant="secondary" onClick={() => setActionError(null)}>
            Dismiss
          </Button>
        </div>
      )}
      {isError && bounds != null && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
          <span>{getErrorMessage(error)}</span>
          <Button size="sm" variant="secondary" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      )}
      {isClusterError && clusterBounds != null && (
        <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
          <span>{getErrorMessage(clusterError)}</span>
          <Button size="sm" variant="secondary" onClick={() => refetchClusters()}>
            Retry
          </Button>
        </div>
      )}
      {!isFetching &&
        !isError &&
        bounds != null &&
        visiblePins.length === 0 && (
        <p className="text-sm text-gray-600">
          No pins in this area yet.
          {isAuthenticated ? ' Double-click the map to add one.' : ' Sign in to add pins.'}
        </p>
      )}
      {!isFetchingClusters &&
        !isClusterError &&
        clusterBounds != null &&
        visibleClusters.length === 0 && (
        <p className="text-sm text-gray-600">
          Clustering pins for this view… zoom in to see individual pins.
        </p>
      )}
    </div>
  )
}
