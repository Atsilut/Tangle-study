import { useEffect, useMemo, useState } from 'react'
import { subscribeToLocationSession } from './signalr'
import { useActiveGroupLocations } from './hooks'
import type { LiveLocation } from './api'

export function useLiveGroupLocations(groupId: number | null, enabled: boolean): LiveLocation[] {
  const { data: initial = [] } = useActiveGroupLocations(groupId, enabled)
  const [realtimeOverrides, setRealtimeOverrides] = useState<Map<number, LiveLocation>>(
    () => new Map(),
  )

  const sessionIds = useMemo(
    () => initial.map((location) => location.sessionId).sort((a, b) => a - b).join(','),
    [initial],
  )

  useEffect(() => {
    if (!enabled || groupId == null || sessionIds.length === 0) return

    let cancelled = false
    const cleanups: Array<() => void> = []

    void (async () => {
      for (const location of initial) {
        const unsubscribe = await subscribeToLocationSession(location.sessionId, (update) => {
          if (update.groupId !== groupId) return
          setRealtimeOverrides((prev) => {
            const next = new Map(prev)
            next.set(update.sessionId, update)
            return next
          })
        })
        if (cancelled) {
          unsubscribe()
          return
        }
        cleanups.push(unsubscribe)
      }
    })()

    return () => {
      cancelled = true
      cleanups.forEach((cleanup) => cleanup())
    }
  }, [enabled, groupId, initial, sessionIds])

  return useMemo(() => {
    const allowedSessionIds = new Set(initial.map((location) => location.sessionId))
    const merged = new Map(initial.map((location) => [location.sessionId, location]))
    for (const [sessionId, location] of realtimeOverrides) {
      if (location.groupId !== groupId) continue
      if (!allowedSessionIds.has(sessionId)) continue
      merged.set(sessionId, location)
    }
    return Array.from(merged.values())
  }, [groupId, initial, realtimeOverrides])
}
