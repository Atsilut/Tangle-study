import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Avatar, Button, Card, CardBody, CardHeader, ConfirmDialog } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { CommentSection } from '@/features/comments'
import { MediaGallery } from '@/features/media'
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
  const { data, isLoading, isError, refetch } = useBoardPost(
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
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {data && (
          <Card>
            <CardHeader className="flex items-center gap-3">
              <Avatar name={data.authorNickname} />
              <div>
                <Link
                  to={`/users/${data.authorId}`}
                  className="text-sm font-medium text-gray-900 hover:underline"
                >
                  {data.authorNickname}
                </Link>
                <EditedTimestamp createdAt={data.createdAt} updatedAt={data.updatedAt} />
              </div>
              {isAuthor && (
                <div className="ml-auto flex gap-2">
                  <Link to={`/groups/${groupId}/boards/${board}/posts/${data.id}/edit`}>
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
              <h1 className="text-xl font-bold text-gray-900">{data.title}</h1>
              <p className="whitespace-pre-wrap text-sm text-gray-700">{data.content}</p>
              <MediaGallery assets={data.media} />
            </CardBody>
          </Card>
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
