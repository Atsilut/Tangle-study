import { type ReactNode, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button, Card, EmptyState, Tabs } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import {
  useAcceptInvitation,
  useCancelInvitation,
  useIgnoreInvitation,
  useIgnoredInvitations,
  useMyInvitations,
  useRejectInvitation,
} from '../invitationsHooks'
import type { GroupInvitation } from '../invitationsApi'

type TabId = 'incoming' | 'outgoing' | 'ignored'

export function InvitationsPage() {
  const [tab, setTab] = useState<TabId>('incoming')
  const mine = useMyInvitations()
  const ignored = useIgnoredInvitations()

  const incoming = (mine.data ?? []).filter((i) => i.isIncoming)
  const outgoing = (mine.data ?? []).filter((i) => !i.isIncoming)

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Invitations</h1>
      <Tabs
        activeId={tab}
        onChange={(id) => setTab(id as TabId)}
        tabs={[
          { id: 'incoming', label: 'Received', count: incoming.length },
          { id: 'outgoing', label: 'Sent', count: outgoing.length },
          { id: 'ignored', label: 'Ignored', count: ignored.data?.length },
        ]}
      />

      {tab === 'incoming' && (
        <QueryBoundary
          isLoading={mine.isLoading}
          isError={mine.isError}
          onRetry={() => mine.refetch()}
        >
          {incoming.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {incoming.map((inv) => (
                <li key={inv.id}>
                  <IncomingRow invitation={inv} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No invitations" />
          )}
        </QueryBoundary>
      )}

      {tab === 'outgoing' && (
        <QueryBoundary
          isLoading={mine.isLoading}
          isError={mine.isError}
          onRetry={() => mine.refetch()}
        >
          {outgoing.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {outgoing.map((inv) => (
                <li key={inv.id}>
                  <OutgoingRow invitation={inv} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No sent invitations" />
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
              {ignored.data.map((inv) => (
                <li key={inv.id}>
                  <IncomingRow invitation={inv} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No ignored invitations" />
          )}
        </QueryBoundary>
      )}
    </div>
  )
}

function InvitationRow({
  invitation,
  personPrefix,
  actions,
}: {
  invitation: GroupInvitation
  personPrefix: string
  actions: ReactNode
}) {
  return (
    <Card className="flex items-center gap-3 px-4 py-3">
      <div className="min-w-0 flex-1">
        <Link
          to={`/groups/${invitation.groupId}`}
          className="block truncate text-sm font-medium text-gray-900 hover:underline"
        >
          {invitation.groupName}
        </Link>
        <p className="mt-0.5 truncate text-xs text-gray-500">
          {personPrefix}{' '}
          <Link
            to={`/users/${invitation.otherUserId}`}
            className="text-gray-600 hover:underline"
          >
            {invitation.otherUserNickname}
          </Link>
        </p>
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </Card>
  )
}

function IncomingRow({ invitation }: { invitation: GroupInvitation }) {
  const accept = useAcceptInvitation()
  const ignore = useIgnoreInvitation()
  const reject = useRejectInvitation()
  const busy = accept.isPending || ignore.isPending || reject.isPending
  return (
    <InvitationRow
      invitation={invitation}
      personPrefix="Invited by"
      actions={
        <>
          <Button size="sm" disabled={busy} isLoading={accept.isPending} onClick={() => accept.mutate(invitation.id)}>
            Accept
          </Button>
          <Button size="sm" variant="secondary" disabled={busy} onClick={() => ignore.mutate(invitation.id)}>
            Ignore
          </Button>
          <Button size="sm" variant="danger" disabled={busy} onClick={() => reject.mutate(invitation.id)}>
            Reject
          </Button>
        </>
      }
    />
  )
}

function OutgoingRow({ invitation }: { invitation: GroupInvitation }) {
  const cancel = useCancelInvitation()
  return (
    <InvitationRow
      invitation={invitation}
      personPrefix="Invited"
      actions={
        <Button size="sm" variant="secondary" isLoading={cancel.isPending} onClick={() => cancel.mutate(invitation.id)}>
          Cancel
        </Button>
      }
    />
  )
}
