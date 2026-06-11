import { useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { Button, EmptyState, Tabs } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { GroupRole } from '@/types/api'
import {
  useApproveApplication,
  useGroupApplications,
  useGroupIgnoredApplications,
  useIgnoreApplication,
  useRejectApplication,
} from '../applicationsHooks'
import { useMyGroupRole } from '../membersHooks'
import type { GroupApplication } from '../applicationsApi'

type TabId = 'pending' | 'ignored'

export function GroupApplicationsPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const [tab, setTab] = useState<TabId>('pending')
  const { role, isLoading: roleLoading } = useMyGroupRole(
    Number.isFinite(groupId) ? groupId : null,
  )
  const pending = useGroupApplications(Number.isFinite(groupId) ? groupId : null)
  const ignored = useGroupIgnoredApplications(Number.isFinite(groupId) ? groupId : null)

  if (roleLoading) return null
  if (role !== GroupRole.Owner && role !== GroupRole.Admin) {
    return <Navigate to={`/groups/${groupId}`} replace />
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}`} className="text-sm text-blue-600 hover:underline">
        Back to group
      </Link>
      <h1 className="text-2xl font-bold text-gray-900">Applications</h1>
      <Tabs
        activeId={tab}
        onChange={(t) => setTab(t as TabId)}
        tabs={[
          { id: 'pending', label: 'Pending', count: pending.data?.length },
          { id: 'ignored', label: 'Ignored', count: ignored.data?.length },
        ]}
      />

      {tab === 'pending' && (
        <QueryBoundary
          isLoading={pending.isLoading}
          isError={pending.isError}
          onRetry={() => pending.refetch()}
        >
          {pending.data && pending.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {pending.data.map((app) => (
                <li key={app.id}>
                  <ApplicantRow application={app} showApprove />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No pending applications" />
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
              {ignored.data.map((app) => (
                <li key={app.id}>
                  <ApplicantRow application={app} showApprove />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No ignored applications" />
          )}
        </QueryBoundary>
      )}
    </div>
  )
}

function ApplicantRow({
  application,
  showApprove,
}: {
  application: GroupApplication
  showApprove?: boolean
}) {
  const approve = useApproveApplication()
  const ignore = useIgnoreApplication()
  const reject = useRejectApplication()
  const busy = approve.isPending || ignore.isPending || reject.isPending
  return (
    <UserRow
      userId={application.applicantId}
      nickname={application.applicantNickname}
      subtitle="Wants to join"
      actions={
        <>
          {showApprove && (
            <Button size="sm" disabled={busy} isLoading={approve.isPending} onClick={() => approve.mutate(application.id)}>
              Approve
            </Button>
          )}
          <Button size="sm" variant="secondary" disabled={busy} onClick={() => ignore.mutate(application.id)}>
            Ignore
          </Button>
          <Button size="sm" variant="danger" disabled={busy} onClick={() => reject.mutate(application.id)}>
            Reject
          </Button>
        </>
      }
    />
  )
}
