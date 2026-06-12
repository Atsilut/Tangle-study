import { api, getList } from '@/lib/apiClient'
import type { MediaAsset } from '@/types/api'

export interface Comment {
  id: number
  content: string
  postId?: number
  deletedPostId?: number
  authorId: number
  authorNickname: string
  // Live author id; null when the author was deleted (use for ownership).
  userId?: number
  deletedUserId?: number
  parentId?: number
  deletedParentId?: number
  createdAt: string
  updatedAt: string
  replies: Comment[]
  media?: MediaAsset
}

export interface CreateCommentRequest {
  content: string
  postId: number
  parentId?: number
  mediaAssetId?: number
}

export interface UpdateCommentRequest {
  id: number
  content: string
}

// GET /api/comments/post/{postId} -> 200 forest | 204 empty
export function getCommentsByPost(postId: number): Promise<Comment[]> {
  return getList<Comment>(`/comments/post/${postId}`)
}

// POST /api/comments (JWT) -> 201 (empty body)
export async function createComment(body: CreateCommentRequest): Promise<void> {
  await api.post('/comments', body)
}

// PATCH /api/comments (JWT, author) -> 200
export async function updateComment(body: UpdateCommentRequest): Promise<void> {
  await api.patch('/comments', body)
}

// DELETE /api/comments/{id} (JWT, author) -> 204
export async function deleteComment(id: number): Promise<void> {
  await api.delete(`/comments/${id}`)
}
