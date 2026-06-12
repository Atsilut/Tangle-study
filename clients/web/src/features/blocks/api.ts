import { api, getList } from '@/lib/apiClient'

export interface UserBlock {
  id: number
  blockedUserId: number
  blockedUserNickname: string
  createdAt: string
  updatedAt: string
}

// POST /api/users/blocks (JWT) -> 200
export async function blockUser(blockedUserId: number): Promise<void> {
  await api.post('/users/blocks', { blockedUserId })
}

// GET /api/users/blocks/me (JWT) -> 200 list | 204
export function getMyBlocks(): Promise<UserBlock[]> {
  return getList<UserBlock>('/users/blocks/me')
}

// DELETE /api/users/blocks/{id} (JWT) -> 204
export async function unblockUser(id: number): Promise<void> {
  await api.delete(`/users/blocks/${id}`)
}
