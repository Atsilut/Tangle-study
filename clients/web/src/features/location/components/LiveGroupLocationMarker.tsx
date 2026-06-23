function formatNickname(nickname: string): string {
  const trimmed = nickname.trim() || 'Member'
  if (trimmed.length <= 14) return trimmed
  return `${trimmed.slice(0, 13)}…`
}

export interface LiveGroupLocationMarkerProps {
  nickname: string
  selected?: boolean
  animationDelayMs?: number
}

export function LiveGroupLocationMarker({
  nickname,
  selected = false,
  animationDelayMs = 0,
}: LiveGroupLocationMarkerProps) {
  const label = formatNickname(nickname)

  return (
    <div
      className={`live-friend-marker${selected ? ' live-friend-marker--selected' : ''}`}
      role="img"
      aria-label={`${label} live location`}
      title={`${label} — live location`}
      style={{ ['--live-friend-pulse-delay' as string]: `${animationDelayMs}ms` }}
    >
      <span className="live-friend-marker__pulse" aria-hidden="true" />
      <span className="live-friend-marker__ring" aria-hidden="true" />
      <span className="live-friend-marker__dot" aria-hidden="true" />
      <span className="live-friend-marker__label" aria-hidden="true">
        {label}
      </span>
    </div>
  )
}
