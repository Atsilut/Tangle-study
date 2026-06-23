import maplibregl from 'maplibre-gl'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-csp-worker?url'

// MapLibre needs an explicit worker URL when bundled by Vite; without this, the map
// canvas renders markers but tile layers stay blank (solid style background only).
maplibregl.setWorkerUrl(workerUrl)

export { maplibregl }
