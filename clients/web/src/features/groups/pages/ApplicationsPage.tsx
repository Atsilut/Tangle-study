import { Link } from 'react-router-dom'
import { Badge, Button, Card, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { useCancelApplication, useMyApplications } from '../applicationsHooks'
import type { GroupApplication } from '../applicationsApi'

export function ApplicationsPage() {
  const mine = useMyApplications()

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">My applications</h1>
      <QueryBoundary isLoading={mine.isLoading} isError={mine.isError} onRetry={() => mine.refetch()}>
        {mine.data && mine.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {mine.data.map((app) => (
              <li key={app.id}>
                <ApplicationRow application={app} />
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No applications" />
        )}
      </QueryBoundary>
    </div>
  )
}

function ApplicationRow({ application }: { application: GroupApplication }) {
  const cancel = useCancelApplication()
  return (
    <Card className="flex items-center gap-3 px-4 py-3">
      <div className="min-w-0 flex-1">
        <Link
          to={`/groups/${application.groupId}`}
          className="block truncate text-sm font-medium text-gray-900 hover:underline"
        >
          Group #{application.groupId}
        </Link>
        <div className="mt-0.5">
          {application.isPending ? (
            <Badge color="yellow">Pending</Badge>
          ) : (
            <Badge>Ignored</Badge>
          )}
        </div>
      </div>
      <Button
        size="sm"
        variant="secondary"
        isLoading={cancel.isPending}
        onClick={() => cancel.mutate(application.id)}
      >
        Cancel
      </Button>
    </Card>
  )
}
