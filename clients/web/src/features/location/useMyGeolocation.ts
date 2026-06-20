import { useCallback, useEffect, useState } from 'react'
import { readCurrentPosition, watchCurrentPosition, type GeoCoordinates } from './geolocation'

export function useMyGeolocation(enabled: boolean) {
  const [position, setPosition] = useState<GeoCoordinates | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!enabled) return

    return watchCurrentPosition(
      (coords) => {
        setPosition(coords)
        setError(null)
      },
      (message) => setError(message),
    )
  }, [enabled])

  const refresh = useCallback(async () => {
    setError(null)
    try {
      const coords = await readCurrentPosition()
      setPosition(coords)
      return coords
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Could not read your location.'
      setError(message)
      throw err
    }
  }, [])

  return { position, error, isWatching: enabled, refresh }
}
