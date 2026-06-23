import { Button } from '@/components/ui'
import type { LocationSafetyAlert } from '../api'

export interface SafetyAlertListProps {
  alerts: LocationSafetyAlert[]
  onDismiss: (sessionId: number, type: LocationSafetyAlert['type']) => void
}

function alertStyles(type: LocationSafetyAlert['type']): string {
  if (type === 'Sos') {
    return 'border-red-300 bg-red-50 text-red-900'
  }
  return 'border-amber-300 bg-amber-50 text-amber-950'
}

function alertTitle(type: LocationSafetyAlert['type']): string {
  return type === 'Sos' ? 'SOS alert' : 'Stale location'
}

export function SafetyAlertList({ alerts, onDismiss }: SafetyAlertListProps) {
  if (alerts.length === 0) return null

  return (
    <div className="flex flex-col gap-2" aria-live="polite">
      {alerts.map((alert) => (
        <div
          key={`${alert.type}-${alert.sessionId}-${alert.occurredAt}`}
          className={`flex items-start justify-between gap-3 rounded-lg border p-3 text-sm ${alertStyles(alert.type)}`}
          role="alert"
        >
          <div className="min-w-0">
            <p className="font-semibold">{alertTitle(alert.type)}</p>
            <p className="mt-1">{alert.message}</p>
            <p className="mt-1 text-xs opacity-80">
              {new Date(alert.occurredAt).toLocaleTimeString()}
              {alert.latitude != null && alert.longitude != null
                ? ` · ${alert.latitude.toFixed(4)}, ${alert.longitude.toFixed(4)}`
                : ''}
            </p>
          </div>
          <Button
            size="sm"
            variant="secondary"
            onClick={() => onDismiss(alert.sessionId, alert.type)}
          >
            Dismiss
          </Button>
        </div>
      ))}
    </div>
  )
}
