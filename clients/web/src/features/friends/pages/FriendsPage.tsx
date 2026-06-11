import { useState } from 'react'
import { Button, EmptyState, Tabs } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
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
      <Tabs
        activeId={tab}
        onChange={(id) => setTab(id as TabId)}
        tabs={[
          { id: 'friends', label: 'Friends', count: friends.data?.length },
          { id: 'incoming', label: 'Requests', count: incoming.length },
          { id: 'outgoing', label: 'Sent', count: outgoing.length },
          { id: 'ignored', label: 'Ignored', count: ignored.data?.length },
        ]}
      />

      {tab === 'friends' && (
        <QueryBoundary
          isLoading={friends.isLoading}
          isError={friends.isError}
          onRetry={() => friends.refetch()}
        >
          {friends.data && friends.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {friends.data.map((f) => (
                <li key={f.id}>
                  <FriendRow id={f.id} userId={f.otherUserId} nickname={f.otherUserNickname} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No friends yet" />
          )}
        </QueryBoundary>
      )}

      {tab === 'incoming' && (
        <QueryBoundary
          isLoading={pending.isLoading}
          isError={pending.isError}
          onRetry={() => pending.refetch()}
        >
          {incoming.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {incoming.map((r) => (
                <li key={r.id}>
                  <IncomingRow id={r.id} userId={r.otherUserId} nickname={r.otherUserNickname} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No incoming requests" />
          )}
        </QueryBoundary>
      )}

      {tab === 'outgoing' && (
        <QueryBoundary
          isLoading={pending.isLoading}
          isError={pending.isError}
          onRetry={() => pending.refetch()}
        >
          {outgoing.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {outgoing.map((r) => (
                <li key={r.id}>
                  <OutgoingRow id={r.id} userId={r.otherUserId} nickname={r.otherUserNickname} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No sent requests" />
          )}
        </QueryBoundary>
      )}

      {tab === 'ignored' && (
        <QueryBoundary
          isLoading={ignored.isLoading}
          isError={ignored.isError}
          onRetry={() => ignored.refetch()}
        >
          {ignored.data && ignored.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {ignored.data.map((r) => (
                <li key={r.id}>
                  <IncomingRow id={r.id} userId={r.otherUserId} nickname={r.otherUserNickname} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No ignored requests" />
          )}
        </QueryBoundary>
      )}
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
  return (
    <UserRow
      userId={userId}
      nickname={nickname}
      actions={
        <Button size="sm" variant="secondary" isLoading={remove.isPending} onClick={() => remove.mutate(id)}>
          Remove
        </Button>
      }
    />
  )
}

function IncomingRow({ id, userId, nickname }: RowProps) {
  const accept = useAcceptRequest()
  const ignore = useIgnoreRequest()
  const reject = useRejectRequest()
  const busy = accept.isPending || ignore.isPending || reject.isPending
  return (
    <UserRow
      userId={userId}
      nickname={nickname}
      subtitle="Wants to be your friend"
      actions={
        <>
          <Button size="sm" disabled={busy} isLoading={accept.isPending} onClick={() => accept.mutate(id)}>
            Accept
          </Button>
          <Button size="sm" variant="secondary" disabled={busy} onClick={() => ignore.mutate(id)}>
            Ignore
          </Button>
          <Button size="sm" variant="danger" disabled={busy} onClick={() => reject.mutate(id)}>
            Reject
          </Button>
        </>
      }
    />
  )
}

function OutgoingRow({ id, userId, nickname }: RowProps) {
  const cancel = useCancelRequest()
  return (
    <UserRow
      userId={userId}
      nickname={nickname}
      subtitle="Request sent"
      actions={
        <Button size="sm" variant="secondary" isLoading={cancel.isPending} onClick={() => cancel.mutate(id)}>
          Cancel
        </Button>
      }
    />
  )
}
