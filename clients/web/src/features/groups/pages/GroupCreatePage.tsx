import { useNavigate } from 'react-router-dom'
import { Card, CardBody, CardHeader } from '@/components/ui'
import { useCreateGroup } from '../hooks'
import { GroupForm } from '../components/GroupForm'

export function GroupCreatePage() {
  const navigate = useNavigate()
  const createGroup = useCreateGroup()

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">New group</h1>
      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-gray-900">Create a group</h2>
        </CardHeader>
        <CardBody>
          <GroupForm
            submitLabel="Create"
            isPending={createGroup.isPending}
            error={createGroup.error}
            onSubmit={(values) =>
              createGroup.mutate(values, {
                onSuccess: (group) => navigate(`/groups/${group.id}`),
              })
            }
            onCancel={() => navigate('/groups')}
          />
        </CardBody>
      </Card>
    </div>
  )
}
