import { useRef } from 'react'
import Map, { Marker, NavigationControl, type MapRef } from 'react-map-gl/maplibre'
import { Button } from '@/components/ui'
import { maplibregl } from '@/lib/maplibreSetup'
import { OSM_RASTER_STYLE, OSM_TILE_MAX_ZOOM } from '../mapStyle'
import type { Place } from '../places'
import { MapSearchBox } from './MapSearchBox'
import 'maplibre-gl/dist/maplibre-gl.css'

export interface PostLocation {
  latitude: number
  longitude: number
}

const DEFAULT_VIEW = {
  longitude: 126.978,
  latitude: 37.5665,
  zoom: 11,
} as const

const PICKER_ZOOM = 13

export interface LocationPickerProps {
  value: PostLocation | null
  onChange: (value: PostLocation | null) => void
}

export function LocationPicker({ value, onChange }: LocationPickerProps) {
  const mapRef = useRef<MapRef>(null)

  const handleSelectPlace = (place: Place) => {
    onChange({ latitude: place.latitude, longitude: place.longitude })
    mapRef.current?.getMap()?.flyTo({
      center: [place.longitude, place.latitude],
      zoom: PICKER_ZOOM,
      duration: 800,
    })
  }

  return (
    <div className="flex flex-col gap-2">
      <MapSearchBox onSelectPlace={handleSelectPlace} />
      <div className="h-56 overflow-hidden rounded-md border border-gray-200">
        <Map
          ref={mapRef}
          initialViewState={
            value
              ? { longitude: value.longitude, latitude: value.latitude, zoom: PICKER_ZOOM }
              : DEFAULT_VIEW
          }
          onClick={(event) =>
            onChange({ latitude: event.lngLat.lat, longitude: event.lngLat.lng })
          }
          mapStyle={OSM_RASTER_STYLE}
          mapLib={maplibregl}
          maxZoom={OSM_TILE_MAX_ZOOM}
          style={{ width: '100%', height: '100%' }}
          cursor="crosshair"
        >
          <NavigationControl position="top-right" showCompass={false} />
          {value && (
            <Marker longitude={value.longitude} latitude={value.latitude} anchor="bottom" color="#2563eb" />
          )}
        </Map>
      </div>
      {value ? (
        <div className="flex items-center justify-between gap-2 text-sm text-gray-600">
          <span>
            {value.latitude.toFixed(5)}, {value.longitude.toFixed(5)}
          </span>
          <Button type="button" variant="secondary" size="sm" onClick={() => onChange(null)}>
            Clear location
          </Button>
        </div>
      ) : (
        <p className="text-xs text-gray-500">Optional. Search or click the map to attach a place.</p>
      )}
    </div>
  )
}
