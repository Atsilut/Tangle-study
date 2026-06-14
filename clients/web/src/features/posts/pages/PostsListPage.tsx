import { Link } from 'react-router-dom'
import { Button, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { useAuthStore } from '@/stores/authStore'
import { usePosts } from '../hooks'
import { PostCard } from '../components/PostCard'

export function PostsListPage() {
  const { data, isLoading, isError, error, refetch } = usePosts()
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Posts</h1>
        {isAuthenticated && (
          <Link to="/posts/new">
            <Button size="sm">New post</Button>
          </Link>
        )}
      </div>
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
          <EmptyState
            title="No posts yet"
            description={isAuthenticated ? 'Be the first to post.' : undefined}
            action={
              isAuthenticated ? (
                <Link to="/posts/new">
                  <Button size="sm">New post</Button>
                </Link>
              ) : undefined
            }
          />
        )}
      </QueryBoundary>
    </div>
  )
}
