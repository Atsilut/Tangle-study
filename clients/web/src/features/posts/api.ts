import { api, getList } from '@/lib/apiClient'
import type { MediaAsset } from '@/types/api'

export interface Post {
  id: number
  title: string
  content: string
  createdAt: string
  updatedAt: string
  authorId: number
  authorNickname: string
  media: MediaAsset[]
}

export interface CreatePostRequest {
  title: string
  content: string
  mediaAssetIds?: number[]
}

export interface UpdatePostRequest {
  id: number
  title: string
  content: string
  addMediaAssetIds?: number[]
  removeMediaAssetIds?: number[]
}

// GET /api/posts -> 200 list | 204 empty
export function getPosts(): Promise<Post[]> {
  return getList<Post>('/posts')
}

// GET /api/posts/{id} -> 200 | 404
export async function getPost(id: number): Promise<Post> {
  const res = await api.get<Post>(`/posts/${id}`)
  return res.data
}

// GET /api/posts/nickname/{nickname} -> 200 list | 204 empty
export function getPostsByNickname(nickname: string): Promise<Post[]> {
  return getList<Post>(`/posts/nickname/${encodeURIComponent(nickname)}`)
}

// POST /api/posts (JWT) -> 201 (empty body)
export async function createPost(body: CreatePostRequest): Promise<void> {
  await api.post('/posts', body)
}

// PATCH /api/posts (JWT, author) -> 200
export async function updatePost(body: UpdatePostRequest): Promise<void> {
  await api.patch('/posts', body)
}

// DELETE /api/posts/{id} (JWT, author) -> 204
export async function deletePost(id: number): Promise<void> {
  await api.delete(`/posts/${id}`)
}
