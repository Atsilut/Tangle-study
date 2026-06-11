import { Link } from 'react-router-dom'
import { Card, CardBody, CardHeader, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { useAuthStore } from '@/stores/authStore'
import { useCommentsByPost, useCreateComment } from '../hooks'
import { CommentForm } from './CommentForm'
import { CommentItem } from './CommentItem'

export interface CommentSectionProps {
  postId: number
}

// Mounted under a post: top-level add form plus the threaded comment tree.
export function CommentSection({ postId }: CommentSectionProps) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const { data, isLoading, isError, refetch } = useCommentsByPost(postId)
  const createComment = useCreateComment(postId)

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Comments</h2>
      </CardHeader>
      <CardBody className="flex flex-col gap-4">
        {isAuthenticated ? (
          <CommentForm
            submitLabel="Comment"
            isPending={createComment.isPending}
            error={createComment.error}
            onSubmit={(content) => createComment.mutate({ content, postId })}
          />
        ) : (
          <p className="text-sm text-gray-500">
            <Link to="/login" className="text-blue-600 hover:underline">
              Log in
            </Link>{' '}
            to comment.
          </p>
        )}

        <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
          {data && data.length > 0 ? (
            <div className="flex flex-col gap-4">
              {data.map((comment) => (
                <CommentItem key={comment.id} comment={comment} postId={postId} />
              ))}
            </div>
          ) : (
            <EmptyState title="No comments yet" />
          )}
        </QueryBoundary>
      </CardBody>
    </Card>
  )
}
