export interface GeoCoordinates {
  latitude: number
  longitude: number
  accuracy: number | null
}

export function readCurrentPosition(): Promise<GeoCoordinates> {
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
        enableHighAccuracy: true,
        maximumAge: 10_000,
        timeout: 15_000,
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
