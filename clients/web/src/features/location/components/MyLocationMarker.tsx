export function MyLocationMarker() {
  return (
    <div
      className="my-location-marker"
      role="img"
      aria-label="Your location"
      title="You are here"
    >
      <span className="my-location-marker__pulse" aria-hidden="true" />
      <span className="my-location-marker__ring" aria-hidden="true" />
      <span className="my-location-marker__dot" aria-hidden="true" />
      <span className="my-location-marker__label" aria-hidden="true">
        You
      </span>
    </div>
  )
}
