import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ConfirmDialog } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { CommentSection } from '@/features/comments'
import { PostDetailCard } from '@/features/posts/components/PostDetailCard'
import { useAuthStore } from '@/stores/authStore'
import { useBoardPost, useDeleteBoardPost } from '../boardsHooks'

export function GroupBoardPostDetailPage() {
  const { id, boardId, postId } = useParams<{
    id: string
    boardId: string
    postId: string
  }>()
  const groupId = Number(id)
  const board = Number(boardId)
  const post = Number(postId)
  const valid = Number.isFinite(groupId) && Number.isFinite(board) && Number.isFinite(post)
  const navigate = useNavigate()
  const currentUserId = useAuthStore((s) => s.userId)
  const { data, isLoading, isError, error, refetch } = useBoardPost(
    valid ? groupId : null,
    valid ? board : null,
    valid ? post : null,
  )
  const deletePost = useDeleteBoardPost(groupId, board)
  const [confirmOpen, setConfirmOpen] = useState(false)

  const isAuthor = data != null && currentUserId === data.authorId

  const onDelete = () => {
    deletePost.mutate(post, {
      onSuccess: () => navigate(`/groups/${groupId}/boards/${board}`, { replace: true }),
    })
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link
        to={`/groups/${groupId}/boards/${board}`}
        className="text-sm text-blue-600 hover:underline"
      >
        Back to board
      </Link>
      <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
        {data && (
          <PostDetailCard
            post={data}
            isAuthor={isAuthor}
            editUrl={`/groups/${groupId}/boards/${board}/posts/${data.id}/edit`}
            onDelete={() => setConfirmOpen(true)}
          />
        )}
      </QueryBoundary>

      {data && <CommentSection postId={data.id} />}

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
