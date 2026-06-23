import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ConfirmDialog } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { CommentSection } from '@/features/comments'
import { useAuthStore } from '@/stores/authStore'
import { PostDetailCard } from '../components/PostDetailCard'
import { useDeletePost, usePost } from '../hooks'

export function PostDetailPage() {
  const { id } = useParams<{ id: string }>()
  const postId = Number(id)
  const navigate = useNavigate()
  const currentUserId = useAuthStore((s) => s.userId)
  const { data: post, isLoading, isError, error, refetch } = usePost(
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
      <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
        {post && (
          <PostDetailCard
            post={post}
            isAuthor={isAuthor}
            editUrl={`/posts/${post.id}/edit`}
            onDelete={() => setConfirmOpen(true)}
          />
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
