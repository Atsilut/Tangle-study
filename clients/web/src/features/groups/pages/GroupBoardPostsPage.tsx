import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button, EmptyState, Modal } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { PostForm, type PostFormValues } from '@/features/posts/components/PostForm'
import { PostCard } from '@/features/posts/components/PostCard'
import { useBoardPosts, useBoards, useCreateBoardPost } from '../boardsHooks'

export function GroupBoardPostsPage() {
  const { id, boardId } = useParams<{ id: string; boardId: string }>()
  const groupId = Number(id)
  const board = Number(boardId)
  const valid = Number.isFinite(groupId) && Number.isFinite(board)
  const posts = useBoardPosts(valid ? groupId : null, valid ? board : null)
  const boards = useBoards(valid ? groupId : null)
  const boardMeta = boards.data?.find((b) => b.id === board)
  const boardName = boardMeta?.name ?? 'Board'
  const canWrite = boardMeta?.canWrite ?? false
  const createPost = useCreateBoardPost(groupId, board)
  const [createOpen, setCreateOpen] = useState(false)

  const onCreate = (values: PostFormValues) => {
    createPost.mutate(values, { onSuccess: () => setCreateOpen(false) })
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}/boards`} className="text-sm text-blue-600 hover:underline">
        Back to boards
      </Link>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">{boardName}</h1>
        {canWrite && <Button onClick={() => setCreateOpen(true)}>New post</Button>}
      </div>

      <QueryBoundary
        isLoading={posts.isLoading}
        isError={posts.isError}
        error={posts.error}
        onRetry={() => posts.refetch()}
      >
        {posts.data && posts.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {posts.data.map((post) => (
              <li key={post.id}>
                <PostCard
                  post={post}
                  to={`/groups/${groupId}/boards/${board}/posts/${post.id}`}
                />
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
