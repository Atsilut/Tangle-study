import { useNavigate, useParams } from 'react-router-dom'
import { Card, CardBody, CardHeader } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { PostForm } from '@/features/posts/components/PostForm'
import { useAuthStore } from '@/stores/authStore'
import type { Post } from '@/features/posts/api'
import { useBoardPost, useUpdateBoardPost } from '../boardsHooks'

export function GroupBoardPostEditPage() {
  const { id, boardId, postId } = useParams<{
    id: string
    boardId: string
    postId: string
  }>()
  const groupId = Number(id)
  const board = Number(boardId)
  const post = Number(postId)
  const valid = Number.isFinite(groupId) && Number.isFinite(board) && Number.isFinite(post)
  const { data, isLoading, isError, error, refetch } = useBoardPost(
    valid ? groupId : null,
    valid ? board : null,
    valid ? post : null,
  )

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Edit post</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
        {data && valid && <EditForm groupId={groupId} boardId={board} post={data} />}
      </QueryBoundary>
    </div>
  )
}

function EditForm({
  groupId,
  boardId,
  post,
}: {
  groupId: number
  boardId: number
  post: Post
}) {
  const navigate = useNavigate()
  const currentUserId = useAuthStore((s) => s.userId)
  const updatePost = useUpdateBoardPost(groupId, boardId)

  if (currentUserId !== post.authorId) {
    return <p className="text-sm text-gray-600">You can only edit your own posts.</p>
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Update your post</h2>
      </CardHeader>
      <CardBody>
        <PostForm
          initial={{ title: post.title, content: post.content }}
          existingMedia={post.media}
          enableMedia
          submitLabel="Save changes"
          isPending={updatePost.isPending}
          error={updatePost.error}
          onSubmit={(values) =>
            updatePost.mutate(
              {
                id: post.id,
                title: values.title,
                content: values.content,
                addMediaAssetIds: values.addMediaAssetIds,
                removeMediaAssetIds: values.removeMediaAssetIds,
              },
              {
                onSuccess: () =>
                  navigate(`/groups/${groupId}/boards/${boardId}/posts/${post.id}`),
              },
            )
          }
          onCancel={() => navigate(`/groups/${groupId}/boards/${boardId}/posts/${post.id}`)}
        />
      </CardBody>
    </Card>
  )
}
