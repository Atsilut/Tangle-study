import { Link } from 'react-router-dom'
import { Avatar, Card, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { useUsers } from '../hooks'

export function UsersListPage() {
  const { data, isLoading, isError, refetch } = useUsers()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Users</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {data && data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {data.map((user) => (
              <li key={user.id}>
                <Link to={`/users/${user.id}`} className="block">
                  <Card className="flex items-center gap-3 px-4 py-3 hover:bg-gray-50">
                    <Avatar name={user.nickname} />
                    <div>
                      <p className="text-sm font-medium text-gray-900">{user.nickname}</p>
                      <p className="text-xs text-gray-500">{user.email}</p>
                    </div>
                  </Card>
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No users yet" />
        )}
      </QueryBoundary>
    </div>
  )
}
