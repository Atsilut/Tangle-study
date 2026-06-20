import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuthStore } from '@/stores/authStore'
import { MemoryMap } from '../components/MemoryMap'
import { LiveSharingControls } from '../components/LiveSharingControls'
import { GroupLocationSelector } from '../components/GroupLocationSelector'
import { MapSearchBox } from '../components/MapSearchBox'
import type { Place } from '../places'

export function MapPage() {
  const [searchParams] = useSearchParams()
  const [searchFlyToPlace, setSearchFlyToPlace] = useState<Place | null>(null)
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  const urlFlyToPlace = useMemo((): Place | null => {
    const lat = searchParams.get('lat')
    const lng = searchParams.get('lng')
    if (!lat || !lng) return null

    const latitude = Number(lat)
    const longitude = Number(lng)
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null

    return {
      placeId: `coords:${latitude},${longitude}`,
      displayName: 'Post location',
      latitude,
      longitude,
    }
  }, [searchParams])

  const flyToPlace = searchFlyToPlace ?? urlFlyToPlace

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Memory Map</h1>
        <p className="text-sm text-gray-600">
          Real-world map with OpenStreetMap tiles. Pan, search, and explore community pins.
        </p>
      </div>

      <section className="flex flex-col gap-3" aria-label="Map controls">
        <MapSearchBox onSelectPlace={setSearchFlyToPlace} />
        {isAuthenticated && (
          <>
            <GroupLocationSelector value={selectedGroupId} onChange={setSelectedGroupId} />
            <LiveSharingControls groupId={selectedGroupId} />
          </>
        )}
        {isAuthenticated ? (
          <p className="text-sm text-gray-600">
            Magenta &ldquo;You&rdquo; is your position. Green badges show group members sharing live
            location in the selected group. Use the target button to recenter. Double-click to drop a
            pin.
          </p>
        ) : (
          <p className="text-sm text-gray-600">
            Allow location access to see the pulsing magenta &ldquo;You&rdquo; marker.{' '}
            <Link to="/login" className="font-medium text-blue-700 hover:underline">
              Sign in
            </Link>{' '}
            to drop pins or share live location with a group.
          </p>
        )}
        <MemoryMap flyToPlace={flyToPlace} groupId={selectedGroupId} />
      </section>
    </div>
  )
}
