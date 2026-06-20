import type { StyleSpecification } from 'maplibre-gl'

// OSM Standard tiles are only published through zoom 19.
// https://operations.osmfoundation.org/policies/tiles/
export const OSM_TILE_MAX_ZOOM = 19

// OpenStreetMap raster tiles rendered via MapLibre (real-world basemap).
export const OSM_RASTER_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    osm: {
      type: 'raster',
      tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
      tileSize: 256,
      // Native tiles stop at z19; MapLibre overzooms them if the map zooms further.
      maxzoom: OSM_TILE_MAX_ZOOM,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    },
  },
  layers: [
    {
      id: 'osm-raster',
      type: 'raster',
      source: 'osm',
      minzoom: 0,
      // Do not set maxzoom here — layer maxzoom hides the basemap at z >= maxzoom.
    },
  ],
}
