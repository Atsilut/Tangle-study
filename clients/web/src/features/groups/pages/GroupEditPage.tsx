import { Navigate, useNavigate, useParams } from 'react-router-dom'
import { Card, CardBody, CardHeader } from '@/components/ui'
import { CenteredSpinner } from '@/components/common/CenteredSpinner'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { GroupRole } from '@/types/api'
import { useGroup, useUpdateGroup } from '../hooks'
import { useMyGroupRole } from '../membersHooks'
import { GroupForm } from '../components/GroupForm'
import type { Group } from '../api'

export function GroupEditPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const { data: group, isLoading, isError, refetch } = useGroup(
    Number.isFinite(groupId) ? groupId : null,
  )

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Edit group</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {group && <EditForm group={group} />}
      </QueryBoundary>
    </div>
  )
}

function EditForm({ group }: { group: Group }) {
  const navigate = useNavigate()
  const updateGroup = useUpdateGroup()
  const { role, isLoading } = useMyGroupRole(group.id)

  if (isLoading) return <CenteredSpinner />
  if (role !== GroupRole.Owner && role !== GroupRole.Admin) {
    return <Navigate to={`/groups/${group.id}`} replace />
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Group settings</h2>
      </CardHeader>
      <CardBody>
        <GroupForm
          initial={{
            name: group.name,
            description: group.description,
            visibility: group.visibility,
            joinPolicy: group.joinPolicy,
          }}
          submitLabel="Save changes"
          isPending={updateGroup.isPending}
          error={updateGroup.error}
          onSubmit={(values) =>
            updateGroup.mutate(
              { id: group.id, ...values },
              { onSuccess: () => navigate(`/groups/${group.id}`) },
            )
          }
          onCancel={() => navigate(`/groups/${group.id}`)}
        />
      </CardBody>
    </Card>
  )
}
