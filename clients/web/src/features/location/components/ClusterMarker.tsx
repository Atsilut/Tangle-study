/** Radius grows with pin count (sqrt so large clusters do not dominate). */
export function clusterMarkerSizePx(pinCount: number): number {
  const count = Math.max(1, pinCount)
  return Math.round(Math.min(64, 22 + Math.sqrt(count) * 6))
}

export function ClusterMarker({ pinCount }: { pinCount: number }) {
  const size = clusterMarkerSizePx(pinCount)
  const fontSize = size >= 40 ? 14 : size >= 30 ? 12 : 11
  const label = pinCount > 999 ? `${Math.floor(pinCount / 1000)}k` : String(pinCount)

  return (
    <div
      role="img"
      aria-label={`${pinCount} pins`}
      className="flex items-center justify-center rounded-full border-2 border-white bg-blue-700 font-semibold text-white shadow-md"
      style={{
        width: size,
        height: size,
        fontSize,
        backgroundColor:
          pinCount >= 50 ? '#1e3a8a' : pinCount >= 10 ? '#1d4ed8' : pinCount >= 3 ? '#2563eb' : '#3b82f6',
      }}
    >
      {label}
    </div>
  )
}
