import { beforeEach, describe, expect, it, vi } from 'vitest'

const { stopLocationSessionMock, invalidateQueriesMock } = vi.hoisted(() => ({
  stopLocationSessionMock: vi.fn(),
  invalidateQueriesMock: vi.fn(),
}))

vi.mock('./api', () => ({
  stopLocationSession: stopLocationSessionMock,
}))

vi.mock('@/lib/queryClient', () => ({
  queryClient: {
    invalidateQueries: invalidateQueriesMock,
  },
}))

vi.mock('./hooks', () => ({
  locationKeys: {
    mySession: (groupId: number | null) => ['location', 'sessions', 'mine', groupId],
    activeGroup: (groupId: number | null) => ['location', 'sessions', 'active', groupId],
    memberStatus: (groupId: number | null) => ['location', 'sessions', 'members', groupId],
  },
}))

import {
  clearActiveLocationSession,
  getActiveLocationSession,
  registerActiveLocationSession,
  stopActiveLocationSession,
} from './liveSharingRegistry'

describe('liveSharingRegistry', () => {
  beforeEach(() => {
    clearActiveLocationSession()
    stopLocationSessionMock.mockReset()
    invalidateQueriesMock.mockReset()
    stopLocationSessionMock.mockResolvedValue(undefined)
    invalidateQueriesMock.mockResolvedValue(undefined)
  })

  it('tracks the active session', () => {
    registerActiveLocationSession(42, 7)
    expect(getActiveLocationSession()).toEqual({ sessionId: 42, groupId: 7 })
  })

  it('stops and clears the registered session', async () => {
    registerActiveLocationSession(42, 7)

    await stopActiveLocationSession()

    expect(stopLocationSessionMock).toHaveBeenCalledWith(42)
    expect(getActiveLocationSession()).toBeNull()
    expect(invalidateQueriesMock).toHaveBeenCalledTimes(3)
  })

  it('is a no-op when nothing is registered', async () => {
    await stopActiveLocationSession()
    expect(stopLocationSessionMock).not.toHaveBeenCalled()
  })

  it('clears the registry even when stop fails', async () => {
    registerActiveLocationSession(42, 7)
    stopLocationSessionMock.mockRejectedValue(new Error('401'))

    await stopActiveLocationSession()

    expect(getActiveLocationSession()).toBeNull()
  })
})
