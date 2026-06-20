import { useCallback, useEffect, useState } from 'react'
import { Button } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { readCurrentPosition } from '../geolocation'
import {
  useMyLocationSession,
  useStartLocationSession,
  useStopLocationSession,
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
  const [error, setError] = useState<string | null>(null)

  const pushCurrentPosition = useCallback(async () => {
    if (!mySession) return
    const position = await readCurrentPosition()
    await updatePosition.mutateAsync({
      sessionId: mySession.id,
      body: {
        latitude: position.latitude,
        longitude: position.longitude,
      },
    })
  }, [mySession, updatePosition])

  useEffect(() => {
    if (!mySession) return

    void pushCurrentPosition().catch((err: unknown) => {
      setError(getErrorMessage(err, 'Could not refresh live location.'))
    })

    const intervalId = window.setInterval(() => {
      void pushCurrentPosition().catch((err: unknown) => {
        setError(getErrorMessage(err, 'Could not refresh live location.'))
      })
    }, POSITION_REFRESH_MS)

    return () => window.clearInterval(intervalId)
  }, [mySession, pushCurrentPosition])

  const handleStart = async () => {
    if (groupId == null) return
    setError(null)
    try {
      const position = await readCurrentPosition()
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

  const isBusy = startSession.isPending || stopSession.isPending || updatePosition.isPending

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
        {mySession ? (
          <Button size="sm" variant="secondary" disabled={isBusy || isLoading} onClick={handleStop}>
            Stop sharing
          </Button>
        ) : (
          <Button size="sm" disabled={isBusy || isLoading} onClick={handleStart}>
            Start sharing
          </Button>
        )}
      </div>
      {error && <p className="text-sm text-red-700">{error}</p>}
    </div>
  )
}
