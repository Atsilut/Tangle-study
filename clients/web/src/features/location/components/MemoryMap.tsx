import type { Map as MapLibreMap } from 'maplibre-gl'
import type { FeatureCollection, Point } from 'geojson'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import Map, {
  Layer,
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
import type { MapBounds, MapPin } from '../api'
import { MIN_PIN_FETCH_ZOOM, sanitizeBoundsForQuery } from '../api'
import { useCreateMapPin, useMapPins, usePlaceReverse } from '../hooks'
import { OSM_RASTER_STYLE, OSM_TILE_MAX_ZOOM } from '../mapStyle'
import type { Place } from '../places'
import 'maplibre-gl/dist/maplibre-gl.css'

const INITIAL_VIEW = {
  longitude: 126.978,
  latitude: 37.5665,
  zoom: 11,
} as const

const FLY_TO_ZOOM = 14
const BOUNDS_DEBOUNCE_MS = 300
const PINS_LAYER_ID = 'memory-map-pins'

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
      properties: { id: pin.id },
      geometry: {
        type: 'Point',
        coordinates: [pin.longitude, pin.latitude],
      },
    })),
  }
}

export interface MemoryMapProps {
  flyToPlace?: Place | null
}

export function MemoryMap({ flyToPlace = null }: MemoryMapProps) {
  const mapRef = useRef<MapRef>(null)
  const boundsDebounceRef = useRef<number | null>(null)
  const [bounds, setBounds] = useState<MapBounds | null>(null)
  const [selectedPin, setSelectedPin] = useState<MapPin | null>(null)
  const [mapError, setMapError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const { data: pins = [], isFetching, isError, error, refetch } = useMapPins(bounds)
  const visiblePins = bounds == null ? [] : pins
  const pinsGeoJson = useMemo(() => pinsToGeoJson(visiblePins), [visiblePins])
  const createPin = useCreateMapPin()
  const { data: selectedPlaceName } = usePlaceReverse(
    selectedPin?.latitude ?? null,
    selectedPin?.longitude ?? null,
  )

  const syncBounds = useCallback(() => {
    const map = mapRef.current?.getMap()
    if (!map) return
    if (map.getZoom() < MIN_PIN_FETCH_ZOOM) {
      setBounds(null)
      return
    }
    const next = sanitizeBoundsForQuery(quantizeBounds(boundsFromMap(map)))
    if (next == null) {
      setBounds(null)
      return
    }
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

  const handleMapClick = useCallback(
    (event: MapLayerMouseEvent) => {
      const feature = event.features?.[0]
      if (feature?.properties?.id != null) {
        const pinId = Number(feature.properties.id)
        setSelectedPin(visiblePins.find((pin) => pin.id === pinId) ?? null)
        return
      }
      setSelectedPin(null)
    },
    [visiblePins],
  )

  useEffect(() => {
    if (!flyToPlace) return
    mapRef.current?.flyTo({
      center: [flyToPlace.longitude, flyToPlace.latitude],
      zoom: FLY_TO_ZOOM,
      duration: 1200,
    })
    setSelectedPin(null)
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
            interactiveLayerIds={[PINS_LAYER_ID]}
            style={{ width: '100%', height: '100%' }}
            onLoad={syncBounds}
            onMoveEnd={scheduleBoundsSync}
            onClick={handleMapClick}
            onDblClick={(event) => {
              handleDropPin(event.lngLat.lat, event.lngLat.lng)
            }}
            onMouseEnter={(event) => {
              if (event.features?.some((feature) => feature.layer.id === PINS_LAYER_ID)) {
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
          </Map>
        </div>
        {isFetching && bounds != null && (
          <p className="pointer-events-none absolute bottom-3 left-3 z-10 rounded-md bg-white/90 px-2 py-1 text-xs text-gray-600 shadow">
            Loading pins…
          </p>
        )}
        {createPin.isPending && (
          <p className="pointer-events-none absolute bottom-10 left-3 z-10 rounded-md bg-white/90 px-2 py-1 text-xs text-gray-600 shadow">
            Saving pin…
          </p>
        )}
      </div>
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
      {!isFetching && !isError && bounds != null && visiblePins.length === 0 && (
        <p className="text-sm text-gray-600">
          No pins in this area yet.
          {isAuthenticated ? ' Double-click the map to add one.' : ' Sign in to add pins.'}
        </p>
      )}
    </div>
  )
}
