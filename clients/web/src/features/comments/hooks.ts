import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createComment,
  deleteComment,
  getCommentsByPost,
  updateComment,
  type CreateCommentRequest,
  type UpdateCommentRequest,
} from './api'

export const commentKeys = {
  all: ['comments'] as const,
  byPost: (postId: number) => [...commentKeys.all, 'post', postId] as const,
}

export function useCommentsByPost(postId: number | null) {
  return useQuery({
    queryKey: commentKeys.byPost(postId ?? -1),
    queryFn: () => getCommentsByPost(postId as number),
    enabled: postId != null,
  })
}

// Mutations close over the post id so they can invalidate that post's tree.
export function useCreateComment(postId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateCommentRequest) => createComment(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentKeys.byPost(postId) }),
  })
}

export function useUpdateComment(postId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateCommentRequest) => updateComment(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentKeys.byPost(postId) }),
  })
}

export function useDeleteComment(postId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteComment(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentKeys.byPost(postId) }),
  })
}
