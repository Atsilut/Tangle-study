import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '@/stores/authStore'
import { MemoryMap } from '../components/MemoryMap'
import { MapSearchBox } from '../components/MapSearchBox'
import type { Place } from '../places'

export function MapPage() {
  const [flyToPlace, setFlyToPlace] = useState<Place | null>(null)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Memory Map</h1>
        <p className="text-sm text-gray-600">
          Real-world map with OpenStreetMap tiles. Pan, search, and explore community pins.
        </p>
      </div>

      <section className="flex flex-col gap-3" aria-label="Map controls">
        <MapSearchBox onSelectPlace={setFlyToPlace} />
        {isAuthenticated ? (
          <p className="text-sm text-gray-600">
            Search for a place above, then double-click the map to drop a pin.
          </p>
        ) : (
          <p className="text-sm text-gray-600">
            <Link to="/login" className="font-medium text-blue-700 hover:underline">
              Sign in
            </Link>{' '}
            to drop pins.
          </p>
        )}
        <MemoryMap flyToPlace={flyToPlace} />
      </section>
    </div>
  )
}
