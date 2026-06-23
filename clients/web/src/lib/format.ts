function toDate(iso: string): Date | null {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? null : d
}

export function isSameCalendarDay(a: string, b: string): boolean {
  const da = toDate(a)
  const db = toDate(b)
  if (!da || !db) return false
  return (
    da.getFullYear() === db.getFullYear() &&
    da.getMonth() === db.getMonth() &&
    da.getDate() === db.getDate()
  )
}

// Shared date formatting for list/detail timestamps (ISO strings from the API).
export function formatDateTime(iso: string): string {
  const d = toDate(iso)
  if (!d) return ''
  return d.toLocaleString()
}

export function formatDate(iso: string): string {
  const d = toDate(iso)
  if (!d) return ''
  return d.toLocaleDateString()
}

export function formatTime(iso: string): string {
  const d = toDate(iso)
  if (!d) return ''
  return d.toLocaleTimeString()
}

/** Chat inbox rows: time if today, otherwise the calendar date. */
export function formatChatListTimestamp(iso: string): string {
  const d = toDate(iso)
  if (!d) return ''
  const now = new Date()
  const sameDay =
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  return sameDay ? formatTime(iso) : formatDate(iso)
}

export function formatUpdatedAt(
  createdAt: string,
  updatedAt: string,
  variant: 'date' | 'datetime' = 'datetime',
): string {
  if (isSameCalendarDay(createdAt, updatedAt)) return formatTime(updatedAt)
  return variant === 'date' ? formatDate(updatedAt) : formatDateTime(updatedAt)
}

export function isEdited(createdAt: string, updatedAt: string): boolean {
  const created = new Date(createdAt).getTime()
  const updated = new Date(updatedAt).getTime()
  if (Number.isNaN(created) || Number.isNaN(updated)) return false
  return updated > created
}
