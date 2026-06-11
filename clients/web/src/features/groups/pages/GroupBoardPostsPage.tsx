import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button, Card, EmptyState, Modal } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { Avatar, Badge } from '@/components/ui'
import { PostForm, type PostFormValues } from '@/features/posts/components/PostForm'
import { useBoardPosts, useBoards, useCreateBoardPost } from '../boardsHooks'
import type { Post } from '@/features/posts/api'

export function GroupBoardPostsPage() {
  const { id, boardId } = useParams<{ id: string; boardId: string }>()
  const groupId = Number(id)
  const board = Number(boardId)
  const valid = Number.isFinite(groupId) && Number.isFinite(board)
  const posts = useBoardPosts(valid ? groupId : null, valid ? board : null)
  const boards = useBoards(valid ? groupId : null)
  const boardName = boards.data?.find((b) => b.id === board)?.name ?? 'Board'
  const createPost = useCreateBoardPost(groupId, board)
  const [createOpen, setCreateOpen] = useState(false)

  const onCreate = (values: PostFormValues) => {
    createPost.mutate(
      { title: values.title, content: values.content },
      { onSuccess: () => setCreateOpen(false) },
    )
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}/boards`} className="text-sm text-blue-600 hover:underline">
        Back to boards
      </Link>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">{boardName}</h1>
        <Button onClick={() => setCreateOpen(true)}>New post</Button>
      </div>

      <QueryBoundary
        isLoading={posts.isLoading}
        isError={posts.isError}
        onRetry={() => posts.refetch()}
      >
        {posts.data && posts.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {posts.data.map((post) => (
              <li key={post.id}>
                <BoardPostRow groupId={groupId} boardId={board} post={post} />
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No posts yet" />
        )}
      </QueryBoundary>

      <Modal isOpen={createOpen} title="New post" onClose={() => setCreateOpen(false)}>
        <PostForm
          submitLabel="Post"
          isPending={createPost.isPending}
          error={createPost.error}
          enableMedia
          onSubmit={onCreate}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>
    </div>
  )
}

function BoardPostRow({
  groupId,
  boardId,
  post,
}: {
  groupId: number
  boardId: number
  post: Post
}) {
  return (
    <Link to={`/groups/${groupId}/boards/${boardId}/posts/${post.id}`} className="block">
      <Card className="px-4 py-3 hover:bg-gray-50">
        <div className="flex items-center gap-2">
          <Avatar name={post.authorNickname} size="sm" />
          <span className="text-sm font-medium text-gray-900">{post.authorNickname}</span>
          <EditedTimestamp createdAt={post.createdAt} updatedAt={post.updatedAt} variant="date" />
          {post.media.length > 0 && <Badge color="blue">{post.media.length} media</Badge>}
        </div>
        <h3 className="mt-2 text-base font-semibold text-gray-900">{post.title}</h3>
        <p className="mt-1 line-clamp-2 text-sm text-gray-600">{post.content}</p>
      </Card>
    </Link>
  )
}
