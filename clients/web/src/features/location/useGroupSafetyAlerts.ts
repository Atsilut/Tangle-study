import { useEffect, useMemo, useState } from 'react'
import { subscribeToGroupSafetyAlerts } from './signalr'
import type { LocationSafetyAlert } from './api'

const MAX_ALERTS = 5

export function useGroupSafetyAlerts(groupId: number | null, enabled: boolean): LocationSafetyAlert[] {
  const [alerts, setAlerts] = useState<LocationSafetyAlert[]>([])

  useEffect(() => {
    if (!enabled || groupId == null) return

    let cancelled = false
    let unsubscribe: (() => void) | undefined

    void (async () => {
      unsubscribe = await subscribeToGroupSafetyAlerts(groupId, (alert) => {
        if (cancelled || alert.groupId !== groupId) return
        setAlerts((prev) => [alert, ...prev].slice(0, MAX_ALERTS))
      })
    })()

    return () => {
      cancelled = true
      unsubscribe?.()
    }
  }, [enabled, groupId])

  return useMemo(() => {
    if (!enabled || groupId == null) return []
    return alerts.filter((alert) => alert.groupId === groupId)
  }, [alerts, enabled, groupId])
}
