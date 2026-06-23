import { api, getList } from '@/lib/apiClient'
import type { MediaAsset } from '@/types/api'
import { parsePostLocation } from './postLocation'

export interface PostLocation {
  latitude: number
  longitude: number
}

export interface Post {
  id: number
  title: string
  content: string
  createdAt: string
  updatedAt: string
  authorId: number
  authorNickname: string
  media: MediaAsset[]
  location?: PostLocation | null
}

export function parsePost(raw: Post): Post {
  return {
    ...raw,
    location: parsePostLocation(raw.location),
  }
}

export function parsePosts(rows: Post[]): Post[] {
  return rows.map(parsePost)
}

export interface CreatePostRequest {
  title: string
  content: string
  mediaAssetIds?: number[]
  latitude?: number
  longitude?: number
}

export interface UpdatePostRequest {
  id: number
  title: string
  content: string
  addMediaAssetIds?: number[]
  removeMediaAssetIds?: number[]
  latitude?: number
  longitude?: number
  clearLocation?: boolean
}

// GET /api/posts -> 200 list | 204 empty
export async function getPosts(): Promise<Post[]> {
  return parsePosts(await getList<Post>('/posts'))
}

// GET /api/posts/{id} -> 200 | 404
export async function getPost(id: number): Promise<Post> {
  const res = await api.get<Post>(`/posts/${id}`)
  return parsePost(res.data)
}

// GET /api/posts/nickname/{nickname} -> 200 list | 204 empty
export async function getPostsByNickname(nickname: string): Promise<Post[]> {
  return parsePosts(
    await getList<Post>(`/posts/nickname/${encodeURIComponent(nickname)}`),
  )
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
