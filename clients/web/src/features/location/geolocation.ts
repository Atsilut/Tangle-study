export interface GeoCoordinates {
  latitude: number
  longitude: number
  accuracy: number | null
}

export interface ReadCurrentPositionOptions {
  /** Prefer a fresh GPS fix (start sharing). Default false for periodic heartbeats. */
  highAccuracy?: boolean
  /** Accept a cached fix up to this age in ms. Heartbeats use a longer cache to avoid timeouts. */
  maximumAge?: number
  timeout?: number
}

export function readCurrentPosition(
  options: ReadCurrentPositionOptions = {},
): Promise<GeoCoordinates> {
  const highAccuracy = options.highAccuracy ?? false
  const maximumAge = options.maximumAge ?? (highAccuracy ? 10_000 : 120_000)
  const timeout = options.timeout ?? (highAccuracy ? 15_000 : 10_000)

  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject(new Error('Geolocation is not supported in this browser.'))
      return
    }

    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracy: position.coords.accuracy,
        }),
      (error) => reject(new Error(error.message || 'Could not read your location.')),
      {
        enableHighAccuracy: highAccuracy,
        maximumAge,
        timeout,
      },
    )
  })
}

export function watchCurrentPosition(
  onUpdate: (coords: GeoCoordinates) => void,
  onError: (message: string) => void,
): () => void {
  if (!navigator.geolocation) {
    onError('Geolocation is not supported in this browser.')
    return () => {}
  }

  const watchId = navigator.geolocation.watchPosition(
    (position) =>
      onUpdate({
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        accuracy: position.coords.accuracy,
      }),
    (error) => onError(error.message || 'Could not read your location.'),
    {
      enableHighAccuracy: true,
      maximumAge: 15_000,
      timeout: 20_000,
    },
  )

  return () => navigator.geolocation.clearWatch(watchId)
}
