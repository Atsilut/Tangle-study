import { api, getList } from '@/lib/apiClient'
import type { BoardVisibility, BoardWriteability } from '@/types/api'
import { parsePost, parsePosts, type Post } from '@/features/posts/api'

const asForbidden = { treatUnauthorizedAsForbidden: true }

export interface GroupBoard {
  id: number
  groupId: number
  name: string
  description: string | null
  visibility: BoardVisibility
  writeability: BoardWriteability
  canWrite: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateBoardRequest {
  name: string
  description?: string | null
  // Omit to let the backend default by group visibility.
  visibility?: BoardVisibility
  writeability?: BoardWriteability
}

export interface UpdateBoardRequest {
  name: string
  description?: string | null
  visibility: BoardVisibility
  writeability: BoardWriteability
}

export interface CreateBoardPostRequest {
  title: string
  content: string
  mediaAssetIds?: number[]
  latitude?: number
  longitude?: number
}

// GET /api/groups/{groupId}/boards -> 200 list | 204 empty
export function getBoards(groupId: number): Promise<GroupBoard[]> {
  return getList<GroupBoard>(`/groups/${groupId}/boards`, asForbidden)
}

// POST /api/groups/{groupId}/boards (admin/owner) -> 201
export async function createBoard(
  groupId: number,
  body: CreateBoardRequest,
): Promise<GroupBoard> {
  const res = await api.post<GroupBoard>(`/groups/${groupId}/boards`, body, asForbidden)
  return res.data
}

// PATCH /api/groups/{groupId}/boards/{boardId} (admin/owner) -> 200
export async function updateBoard(
  groupId: number,
  boardId: number,
  body: UpdateBoardRequest,
): Promise<GroupBoard> {
  const res = await api.patch<GroupBoard>(
    `/groups/${groupId}/boards/${boardId}`,
    body,
    asForbidden,
  )
  return res.data
}

// DELETE /api/groups/{groupId}/boards/{boardId} (admin/owner) -> 204
export async function deleteBoard(groupId: number, boardId: number): Promise<void> {
  await api.delete(`/groups/${groupId}/boards/${boardId}`, asForbidden)
}

// GET /api/groups/{groupId}/boards/{boardId}/posts -> 200 list | 204 empty
export async function getBoardPosts(groupId: number, boardId: number): Promise<Post[]> {
  return parsePosts(
    await getList<Post>(`/groups/${groupId}/boards/${boardId}/posts`, asForbidden),
  )
}

// GET /api/groups/{groupId}/boards/{boardId}/posts/{postId} -> 200 | 404
export async function getBoardPost(
  groupId: number,
  boardId: number,
  postId: number,
): Promise<Post> {
  const res = await api.get<Post>(
    `/groups/${groupId}/boards/${boardId}/posts/${postId}`,
    asForbidden,
  )
  return parsePost(res.data)
}

// POST /api/groups/{groupId}/boards/{boardId}/posts -> 201 (empty body)
export async function createBoardPost(
  groupId: number,
  boardId: number,
  body: CreateBoardPostRequest,
): Promise<void> {
  await api.post(`/groups/${groupId}/boards/${boardId}/posts`, body, asForbidden)
}
