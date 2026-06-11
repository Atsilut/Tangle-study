import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  Avatar,
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
} from '@/components/ui'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { CommentSection } from '@/features/comments'
import { useAuthStore } from '@/stores/authStore'
import { useDeletePost, usePost } from '../hooks'

export function PostDetailPage() {
  const { id } = useParams<{ id: string }>()
  const postId = Number(id)
  const navigate = useNavigate()
  const currentUserId = useAuthStore((s) => s.userId)
  const { data: post, isLoading, isError, refetch } = usePost(
    Number.isFinite(postId) ? postId : null,
  )
  const deletePost = useDeletePost()
  const [confirmOpen, setConfirmOpen] = useState(false)

  const isAuthor = post != null && currentUserId === post.authorId

  const onDelete = () => {
    deletePost.mutate(postId, { onSuccess: () => navigate('/posts', { replace: true }) })
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to="/posts" className="text-sm text-blue-600 hover:underline">
        Back to posts
      </Link>
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {post && (
          <Card>
            <CardHeader className="flex items-center gap-3">
              <Avatar name={post.authorNickname} />
              <div>
                <Link
                  to={`/users/${post.authorId}`}
                  className="text-sm font-medium text-gray-900 hover:underline"
                >
                  {post.authorNickname}
                </Link>
                <EditedTimestamp createdAt={post.createdAt} updatedAt={post.updatedAt} />
              </div>
              {isAuthor && (
                <div className="ml-auto flex gap-2">
                  <Link to={`/posts/${post.id}/edit`}>
                    <Button size="sm" variant="secondary">
                      Edit
                    </Button>
                  </Link>
                  <Button size="sm" variant="danger" onClick={() => setConfirmOpen(true)}>
                    Delete
                  </Button>
                </div>
              )}
            </CardHeader>
            <CardBody className="flex flex-col gap-3">
              <h1 className="text-xl font-bold text-gray-900">{post.title}</h1>
              <p className="whitespace-pre-wrap text-sm text-gray-700">{post.content}</p>
              {post.media.length > 0 && (
                <Badge color="blue">{post.media.length} media attached</Badge>
              )}
            </CardBody>
          </Card>
        )}
      </QueryBoundary>

      {post && <CommentSection postId={post.id} />}

      <ConfirmDialog
        isOpen={confirmOpen}
        title="Delete post"
        message="This permanently deletes the post."
        confirmLabel="Delete"
        destructive
        isLoading={deletePost.isPending}
        onConfirm={onDelete}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  )
}
