import { Link } from 'react-router-dom'
import { Badge, Button, Card, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import type { Group } from '../api'
import { useDiscoverableGroups, useMyGroups } from '../hooks'
import { groupVisibilityLabels, joinPolicyLabels } from '../labels'

function GroupListCard({ group }: { group: Group }) {
  return (
    <Link to={`/groups/${group.id}`} className="block">
      <Card className="px-4 py-3 hover:bg-gray-50">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 flex-1">
            <h3 className="truncate text-sm font-medium text-gray-900">{group.name}</h3>
            <p className="mt-1 line-clamp-2 text-xs text-gray-600">{group.description}</p>
          </div>
          <div className="flex shrink-0 flex-col items-end gap-1">
            <Badge>{group.memberCount} members</Badge>
            <Badge color="blue">{joinPolicyLabels[group.joinPolicy]}</Badge>
          </div>
        </div>
        <div className="mt-2">
          <Badge color={group.visibility ? 'green' : 'gray'}>
            {groupVisibilityLabels[group.visibility]}
          </Badge>
        </div>
      </Card>
    </Link>
  )
}

export function GroupsPage() {
  const myGroups = useMyGroups()
  const discover = useDiscoverableGroups()

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Groups</h1>
        <Link to="/groups/new">
          <Button size="sm">New group</Button>
        </Link>
      </div>

      <section className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-gray-900">My groups</h2>
        <QueryBoundary
          isLoading={myGroups.isLoading}
          isError={myGroups.isError}
          onRetry={() => myGroups.refetch()}
        >
          {myGroups.data && myGroups.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {myGroups.data.map((group) => (
                <li key={group.id}>
                  <GroupListCard group={group} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              title="No groups yet"
              description="Create a group or join a public one below."
            />
          )}
        </QueryBoundary>
      </section>

      <section className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-gray-900">Discover public groups</h2>
        <QueryBoundary
          isLoading={discover.isLoading}
          isError={discover.isError}
          onRetry={() => discover.refetch()}
        >
          {discover.data && discover.data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {discover.data.map((group) => (
                <li key={group.id}>
                  <GroupListCard group={group} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No public groups" description="Public groups will appear here." />
          )}
        </QueryBoundary>
      </section>

      <div className="flex gap-4 text-sm">
        <Link to="/invitations" className="text-blue-600 hover:underline">
          Invitations
        </Link>
        <Link to="/applications" className="text-blue-600 hover:underline">
          My applications
        </Link>
      </div>
    </div>
  )
}
