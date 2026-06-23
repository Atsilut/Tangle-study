import { Card, CardBody, CardHeader, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { usePostsByNickname } from '../hooks'
import { PostCard } from './PostCard'

interface UserPostsListProps {
  nickname: string
}

export function UserPostsList({ nickname }: UserPostsListProps) {
  const { data, isLoading, isError, error, refetch } = usePostsByNickname(nickname)

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Posts</h2>
      </CardHeader>
      <CardBody>
        <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
          {data && data.length > 0 ? (
            <ul className="flex flex-col gap-2">
              {data.map((post) => (
                <li key={post.id}>
                  <PostCard post={post} />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState title="No posts yet" description={`${nickname} has not posted yet.`} />
          )}
        </QueryBoundary>
      </CardBody>
    </Card>
  )
}
