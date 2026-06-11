import { useState } from 'react'
import { Link } from 'react-router-dom'
import { EditedTimestamp } from '@/components/common/EditedTimestamp'
import { Avatar, ConfirmDialog } from '@/components/ui'
import { useAuthStore } from '@/stores/authStore'
import { MediaAssetView } from '@/features/media'
import { useCreateComment, useDeleteComment, useUpdateComment } from '../hooks'
import { CommentForm } from './CommentForm'
import type { Comment } from '../api'

export interface CommentItemProps {
  comment: Comment
  postId: number
}

// One node in the comment tree; recurses into replies. Reply/edit/delete are
// inline. Edit and delete show only for the live author (comment.userId).
export function CommentItem({ comment, postId }: CommentItemProps) {
  const currentUserId = useAuthStore((s) => s.userId)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const isAuthor = comment.userId != null && comment.userId === currentUserId

  const [mode, setMode] = useState<'view' | 'reply' | 'edit'>('view')
  const [confirmOpen, setConfirmOpen] = useState(false)

  const createReply = useCreateComment(postId)
  const updateComment = useUpdateComment(postId)
  const deleteComment = useDeleteComment(postId)

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-start gap-2">
        <Avatar name={comment.authorNickname} size="sm" />
        <div className="flex-1">
          <div className="flex items-center gap-2">
            {comment.userId != null ? (
              <Link
                to={`/users/${comment.userId}`}
                className="text-sm font-medium text-gray-900 hover:underline"
              >
                {comment.authorNickname}
              </Link>
            ) : (
              <span className="text-sm font-medium text-gray-500">{comment.authorNickname}</span>
            )}
            <EditedTimestamp createdAt={comment.createdAt} updatedAt={comment.updatedAt} />
          </div>

          {mode === 'edit' ? (
            <div className="mt-1">
              <CommentForm
                initial={comment.content}
                submitLabel="Save"
                isPending={updateComment.isPending}
                error={updateComment.error}
                autoFocus
                onSubmit={(content) =>
                  updateComment.mutate(
                    { id: comment.id, content },
                    { onSuccess: () => setMode('view') },
                  )
                }
                onCancel={() => setMode('view')}
              />
            </div>
          ) : (
            <p className="mt-0.5 whitespace-pre-wrap text-sm text-gray-700">{comment.content}</p>
          )}

          {comment.media && (
            <div className="mt-2">
              <MediaAssetView asset={comment.media} />
            </div>
          )}

          {mode === 'view' && (
            <div className="mt-1 flex items-center gap-3 text-xs">
              {isAuthenticated && (
                <button
                  className="font-medium text-gray-500 hover:text-gray-700"
                  onClick={() => setMode('reply')}
                >
                  Reply
                </button>
              )}
              {isAuthor && (
                <>
                  <button
                    className="font-medium text-gray-500 hover:text-gray-700"
                    onClick={() => setMode('edit')}
                  >
                    Edit
                  </button>
                  <button
                    className="font-medium text-red-500 hover:text-red-700"
                    onClick={() => setConfirmOpen(true)}
                  >
                    Delete
                  </button>
                </>
              )}
            </div>
          )}

          {mode === 'reply' && (
            <div className="mt-2">
              <CommentForm
                submitLabel="Reply"
                placeholder="Write a reply…"
                isPending={createReply.isPending}
                error={createReply.error}
                autoFocus
                enableMedia
                onSubmit={(content, mediaAssetId) =>
                  createReply.mutate(
                    { content, postId, parentId: comment.id, mediaAssetId },
                    { onSuccess: () => setMode('view') },
                  )
                }
                onCancel={() => setMode('view')}
              />
            </div>
          )}
        </div>
      </div>

      {comment.replies.length > 0 && (
        <div className="ml-5 flex flex-col gap-3 border-l border-gray-100 pl-4">
          {comment.replies.map((reply) => (
            <CommentItem key={reply.id} comment={reply} postId={postId} />
          ))}
        </div>
      )}

      <ConfirmDialog
        isOpen={confirmOpen}
        title="Delete comment"
        message="This permanently deletes the comment."
        confirmLabel="Delete"
        destructive
        isLoading={deleteComment.isPending}
        onConfirm={() =>
          deleteComment.mutate(comment.id, { onSuccess: () => setConfirmOpen(false) })
        }
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  )
}
