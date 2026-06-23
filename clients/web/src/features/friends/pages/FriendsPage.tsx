import { useState } from 'react'
import { Button, EmptyState } from '@/components/ui'
import { TabbedRequestLayout } from '@/components/common/TabbedRequestLayout'
import { UserRow } from '@/components/common/UserRow'
import { getErrorMessage } from '@/lib/apiError'
import {
  useAcceptRequest,
  useCancelRequest,
  useIgnoreRequest,
  useIgnoredRequests,
  useMyFriends,
  usePendingRequests,
  useRejectRequest,
  useRemoveFriend,
} from '../hooks'

type TabId = 'friends' | 'incoming' | 'outgoing' | 'ignored'

export function FriendsPage() {
  const [tab, setTab] = useState<TabId>('friends')

  const friends = useMyFriends()
  const pending = usePendingRequests()
  const ignored = useIgnoredRequests()

  const incoming = (pending.data ?? []).filter((r) => r.isIncoming)
  const outgoing = (pending.data ?? []).filter((r) => !r.isIncoming)

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Friends</h1>
      <TabbedRequestLayout
        activeId={tab}
        onTabChange={(id) => setTab(id as TabId)}
        tabs={[
          { id: 'friends', label: 'Friends', count: friends.data?.length },
          { id: 'incoming', label: 'Requests', count: incoming.length },
          { id: 'outgoing', label: 'Sent', count: outgoing.length },
          { id: 'ignored', label: 'Ignored', count: ignored.data?.length },
        ]}
        panels={[
          {
            id: 'friends',
            isLoading: friends.isLoading,
            isError: friends.isError,
            error: friends.error,
            onRetry: () => friends.refetch(),
            children:
              friends.data && friends.data.length > 0 ? (
                <ul className="flex flex-col gap-2">
                  {friends.data.map((f) => (
                    <li key={f.id}>
                      <FriendRow id={f.id} userId={f.otherUserId} nickname={f.otherUserNickname} />
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="No friends yet" />
              ),
          },
          {
            id: 'incoming',
            isLoading: pending.isLoading,
            isError: pending.isError,
            error: pending.error,
            onRetry: () => pending.refetch(),
            children:
              incoming.length > 0 ? (
                <ul className="flex flex-col gap-2">
                  {incoming.map((r) => (
                    <li key={r.id}>
                      <IncomingRow id={r.id} userId={r.otherUserId} nickname={r.otherUserNickname} />
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="No incoming requests" />
              ),
          },
          {
            id: 'outgoing',
            isLoading: pending.isLoading,
            isError: pending.isError,
            error: pending.error,
            onRetry: () => pending.refetch(),
            children:
              outgoing.length > 0 ? (
                <ul className="flex flex-col gap-2">
                  {outgoing.map((r) => (
                    <li key={r.id}>
                      <OutgoingRow id={r.id} userId={r.otherUserId} nickname={r.otherUserNickname} />
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="No sent requests" />
              ),
          },
          {
            id: 'ignored',
            isLoading: ignored.isLoading,
            isError: ignored.isError,
            error: ignored.error,
            onRetry: () => ignored.refetch(),
            children:
              ignored.data && ignored.data.length > 0 ? (
                <ul className="flex flex-col gap-2">
                  {ignored.data.map((r) => (
                    <li key={r.id}>
                      <IncomingRow id={r.id} userId={r.otherUserId} nickname={r.otherUserNickname} />
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState title="No ignored requests" />
              ),
          },
        ]}
      />
    </div>
  )
}

interface RowProps {
  id: number
  userId: number
  nickname: string
}

function FriendRow({ id, userId, nickname }: RowProps) {
  const remove = useRemoveFriend()
  const [actionError, setActionError] = useState<string | null>(null)
  return (
    <>
      <UserRow
        userId={userId}
        nickname={nickname}
        actions={
          <Button
            size="sm"
            variant="secondary"
            isLoading={remove.isPending}
            onClick={() =>
              remove.mutate(id, {
                onError: (error) => setActionError(getErrorMessage(error)),
                onSuccess: () => setActionError(null),
              })
            }
          >
            Remove
          </Button>
        }
      />
      {actionError && (
        <p className="text-sm text-red-600" role="alert">
          {actionError}
        </p>
      )}
    </>
  )
}

function IncomingRow({ id, userId, nickname }: RowProps) {
  const accept = useAcceptRequest()
  const ignore = useIgnoreRequest()
  const reject = useRejectRequest()
  const [actionError, setActionError] = useState<string | null>(null)
  const busy = accept.isPending || ignore.isPending || reject.isPending
  const onError = (error: unknown) => setActionError(getErrorMessage(error))
  return (
    <>
      <UserRow
        userId={userId}
        nickname={nickname}
        subtitle="Wants to be your friend"
        actions={
          <>
            <Button
              size="sm"
              disabled={busy}
              isLoading={accept.isPending}
              onClick={() => accept.mutate(id, { onError, onSuccess: () => setActionError(null) })}
            >
              Accept
            </Button>
            <Button
              size="sm"
              variant="secondary"
              disabled={busy}
              onClick={() => ignore.mutate(id, { onError, onSuccess: () => setActionError(null) })}
            >
              Ignore
            </Button>
            <Button
              size="sm"
              variant="danger"
              disabled={busy}
              onClick={() => reject.mutate(id, { onError, onSuccess: () => setActionError(null) })}
            >
              Reject
            </Button>
          </>
        }
      />
      {actionError && (
        <p className="text-sm text-red-600" role="alert">
          {actionError}
        </p>
      )}
    </>
  )
}

function OutgoingRow({ id, userId, nickname }: RowProps) {
  const cancel = useCancelRequest()
  const [actionError, setActionError] = useState<string | null>(null)
  return (
    <>
      <UserRow
        userId={userId}
        nickname={nickname}
        subtitle="Request sent"
        actions={
          <Button
            size="sm"
            variant="secondary"
            isLoading={cancel.isPending}
            onClick={() =>
              cancel.mutate(id, {
                onError: (error) => setActionError(getErrorMessage(error)),
                onSuccess: () => setActionError(null),
              })
            }
          >
            Cancel
          </Button>
        }
      />
      {actionError && (
        <p className="text-sm text-red-600" role="alert">
          {actionError}
        </p>
      )}
    </>
  )
}
