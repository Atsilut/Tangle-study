import Map, { Marker, NavigationControl } from 'react-map-gl/maplibre'
import { Link } from 'react-router-dom'
import { maplibregl } from '@/lib/maplibreSetup'
import { usePlaceReverse } from '@/features/location/hooks'
import { OSM_RASTER_STYLE, OSM_TILE_MAX_ZOOM } from '@/features/location/mapStyle'
import type { PostLocation } from '../api'
import { buildMapUrl } from '../postLocation'
import 'maplibre-gl/dist/maplibre-gl.css'

const PREVIEW_ZOOM = 13

export interface PostLocationPreviewProps {
  location: PostLocation
}

export function PostLocationPreview({ location }: PostLocationPreviewProps) {
  const { data: placeName } = usePlaceReverse(location.latitude, location.longitude)

  return (
    <section
      className="flex flex-col gap-2 rounded-md border border-gray-200 bg-gray-50 p-3"
      aria-label="Post location"
    >
      <div className="flex flex-col gap-0.5">
        <p className="text-sm font-medium text-gray-900">
          {placeName ?? 'Pinned location'}
        </p>
        <p className="text-xs text-gray-500">
          {location.latitude.toFixed(5)}, {location.longitude.toFixed(5)}
        </p>
      </div>
      <div className="h-44 overflow-hidden rounded-md border border-gray-200">
        <Map
          initialViewState={{
            longitude: location.longitude,
            latitude: location.latitude,
            zoom: PREVIEW_ZOOM,
          }}
          mapStyle={OSM_RASTER_STYLE}
          mapLib={maplibregl}
          maxZoom={OSM_TILE_MAX_ZOOM}
          style={{ width: '100%', height: '100%' }}
          scrollZoom={false}
          dragPan={false}
          dragRotate={false}
          doubleClickZoom={false}
          touchZoomRotate={false}
          keyboard={false}
          attributionControl={false}
        >
          <NavigationControl position="top-right" showCompass={false} />
          <Marker
            longitude={location.longitude}
            latitude={location.latitude}
            anchor="bottom"
            color="#2563eb"
          />
        </Map>
      </div>
      <Link
        to={buildMapUrl(location.latitude, location.longitude)}
        className="text-sm font-medium text-blue-700 hover:underline"
      >
        Open in Memory Map
      </Link>
    </section>
  )
}
