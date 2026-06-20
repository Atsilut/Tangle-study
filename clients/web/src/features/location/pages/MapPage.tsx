import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuthStore } from '@/stores/authStore'
import { MemoryMap } from '../components/MemoryMap'
import { LiveSharingControls } from '../components/LiveSharingControls'
import { GroupLocationSelector } from '../components/GroupLocationSelector'
import { MapSearchBox } from '../components/MapSearchBox'
import { GroupSharingStatusList } from '../components/GroupSharingStatusList'
import { SafetyAlertList } from '../components/SafetyAlertList'
import { useGroupSafetyAlerts } from '../useGroupSafetyAlerts'
import type { Place } from '../places'

export function MapPage() {
  const [searchParams] = useSearchParams()
  const [searchFlyToPlace, setSearchFlyToPlace] = useState<Place | null>(null)
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null)
  const [dismissedKeys, setDismissedKeys] = useState<Set<string>>(() => new Set())
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const incomingAlerts = useGroupSafetyAlerts(
    selectedGroupId,
    isAuthenticated && selectedGroupId != null,
  )
  const visibleAlerts = useMemo(
    () =>
      incomingAlerts.filter((alert) => !dismissedKeys.has(`${alert.type}:${alert.sessionId}`)),
    [incomingAlerts, dismissedKeys],
  )

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
            <GroupSharingStatusList
              groupId={selectedGroupId}
              enabled={isAuthenticated && selectedGroupId != null}
            />
            <SafetyAlertList
              alerts={visibleAlerts}
              onDismiss={(sessionId, type) =>
                setDismissedKeys((prev) => new Set(prev).add(`${type}:${sessionId}`))
              }
            />
          </>
        )}
        {isAuthenticated ? (
          <p className="text-sm text-gray-600">
            Magenta &ldquo;You&rdquo; is your position. Green badges show group members sharing live
            location. The list below shows who is and isn&rsquo;t sharing in the selected group.
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
