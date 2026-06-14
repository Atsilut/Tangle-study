import { type ReactNode, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button, EmptyState } from '@/components/ui'
import { TabbedRequestLayout } from '@/components/common/TabbedRequestLayout'
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
      <TabbedRequestLayout
        activeId={tab}
        onTabChange={(id) => setTab(id as TabId)}
        tabs={[
          { id: 'incoming', label: 'Received', count: incoming.length },
          { id: 'outgoing', label: 'Sent', count: outgoing.length },
          { id: 'ignored', label: 'Ignored', count: ignored.data?.length },
        ]}
        panels={[
          {
            id: 'incoming',
            isLoading: mine.isLoading,
            isError: mine.isError,
            error: mine.error,
            onRetry: () => mine.refetch(),
            children:
              incoming.length > 0 ? (
                <InvitationList>
                  {incoming.map((inv) => (
                    <IncomingRow key={inv.id} invitation={inv} />
                  ))}
                </InvitationList>
              ) : (
                <EmptyState title="No invitations" />
              ),
          },
          {
            id: 'outgoing',
            isLoading: mine.isLoading,
            isError: mine.isError,
            error: mine.error,
            onRetry: () => mine.refetch(),
            children:
              outgoing.length > 0 ? (
                <InvitationList>
                  {outgoing.map((inv) => (
                    <OutgoingRow key={inv.id} invitation={inv} />
                  ))}
                </InvitationList>
              ) : (
                <EmptyState title="No sent invitations" />
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
                <InvitationList>
                  {ignored.data.map((inv) => (
                    <IncomingRow key={inv.id} invitation={inv} />
                  ))}
                </InvitationList>
              ) : (
                <EmptyState title="No ignored invitations" />
              ),
          },
        ]}
      />
    </div>
  )
}

function InvitationList({ children }: { children: ReactNode }) {
  return <ul className="flex flex-col gap-2">{children}</ul>
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
    <li>
      <div className="flex items-center gap-3 rounded-lg border border-gray-200 bg-white px-4 py-3">
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
      </div>
    </li>
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
