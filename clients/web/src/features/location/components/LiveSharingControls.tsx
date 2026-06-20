import { useCallback, useEffect, useRef, useState } from 'react'
import { Button } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { readCurrentPosition, watchCurrentPosition, type GeoCoordinates } from '../geolocation'
import {
  useMyLocationSession,
  useStartLocationSession,
  useStopLocationSession,
  useTriggerLocationSos,
  useUpdateLocationSessionPosition,
} from '../hooks'

const POSITION_REFRESH_MS = 30_000

export interface LiveSharingControlsProps {
  groupId: number | null
}

export function LiveSharingControls({ groupId }: LiveSharingControlsProps) {
  const enabled = groupId != null
  const { data: mySession, isLoading } = useMyLocationSession(groupId, enabled)
  const startSession = useStartLocationSession(groupId)
  const stopSession = useStopLocationSession(groupId)
  const updatePosition = useUpdateLocationSessionPosition(groupId)
  const triggerSos = useTriggerLocationSos()
  const [error, setError] = useState<string | null>(null)
  const sessionIdRef = useRef<number | null>(null)
  const lastPositionRef = useRef<Pick<GeoCoordinates, 'latitude' | 'longitude'> | null>(null)

  useEffect(() => {
    sessionIdRef.current = mySession?.id ?? null
  }, [mySession?.id])

  const rememberPosition = useCallback((position: Pick<GeoCoordinates, 'latitude' | 'longitude'>) => {
    lastPositionRef.current = position
  }, [])

  const pushCurrentPosition = useCallback(async () => {
    const sessionId = sessionIdRef.current
    if (sessionId == null) return

    let latitude: number
    let longitude: number
    try {
      const position = await readCurrentPosition()
      latitude = position.latitude
      longitude = position.longitude
      rememberPosition(position)
    } catch (readError: unknown) {
      const last = lastPositionRef.current
      if (last == null) throw readError
      // Heartbeat: re-send last known coords so the server refreshes UpdatedAt even when
      // getCurrentPosition times out (common on desktop) or the device has not moved.
      latitude = last.latitude
      longitude = last.longitude
    }

    await updatePosition.mutateAsync({
      sessionId,
      body: { latitude, longitude },
    })
    setError(null)
  }, [rememberPosition, updatePosition])

  useEffect(() => {
    const sessionId = mySession?.id
    if (sessionId == null) return

    void pushCurrentPosition().catch((err: unknown) => {
      setError(getErrorMessage(err, 'Could not refresh live location.'))
    })

    const intervalId = window.setInterval(() => {
      void pushCurrentPosition().catch((err: unknown) => {
        setError(getErrorMessage(err, 'Could not refresh live location.'))
      })
    }, POSITION_REFRESH_MS)

    return () => window.clearInterval(intervalId)
  }, [mySession?.id, pushCurrentPosition])

  useEffect(() => {
    if (mySession == null) return

    return watchCurrentPosition(
      (position) => rememberPosition(position),
      () => {},
    )
  }, [mySession, rememberPosition])

  const handleStart = async () => {
    if (groupId == null) return
    setError(null)
    try {
      const position = await readCurrentPosition({ highAccuracy: true })
      rememberPosition(position)
      await startSession.mutateAsync({
        latitude: position.latitude,
        longitude: position.longitude,
      })
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Could not start live sharing.'))
    }
  }

  const handleStop = async () => {
    if (!mySession) return
    setError(null)
    try {
      await stopSession.mutateAsync(mySession.id)
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Could not stop live sharing.'))
    }
  }

  const handleSos = async () => {
    if (!mySession) return
    setError(null)
    try {
      await triggerSos.mutateAsync(mySession.id)
    } catch (err: unknown) {
      setError(getErrorMessage(err, 'Could not send SOS alert.'))
    }
  }

  const isSharing = mySession != null
  const isUserActionBusy =
    startSession.isPending || stopSession.isPending || triggerSos.isPending

  if (groupId == null) {
    return (
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-3 text-sm text-gray-600">
        Join a group to share live location with its members.
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-gray-200 bg-gray-50 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-sm font-medium text-gray-900">Live location</p>
          <p className="text-xs text-gray-600">
            {mySession
              ? `Sharing with group members since ${new Date(mySession.startedAt).toLocaleTimeString()}`
              : 'Share your current position with members of the selected group.'}
          </p>
        </div>
        {isSharing ? (
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              variant="secondary"
              disabled={isUserActionBusy}
              onClick={handleSos}
            >
              SOS
            </Button>
            <Button size="sm" variant="secondary" disabled={isUserActionBusy} onClick={handleStop}>
              Stop sharing
            </Button>
          </div>
        ) : (
          <Button size="sm" disabled={isUserActionBusy || isLoading} onClick={handleStart}>
            Start sharing
          </Button>
        )}
      </div>
      {error && <p className="text-sm text-red-700">{error}</p>}
    </div>
  )
}
