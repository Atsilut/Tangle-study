import { api, getList } from '@/lib/apiClient'

const asForbidden = { treatUnauthorizedAsForbidden: true }

export interface GroupBlacklistEntry {
  id: number
  groupId: number
  userId: number
  userNickname: string
  createdAt: string
  updatedAt: string
}

// GET /api/groups/{groupId}/blacklist (owner) -> 200 list
export function getBlacklist(groupId: number): Promise<GroupBlacklistEntry[]> {
  return getList<GroupBlacklistEntry>(`/groups/${groupId}/blacklist`, asForbidden)
}

// POST /api/groups/{groupId}/blacklist (owner) -> 201
export async function addToBlacklist(
  groupId: number,
  userId: number,
): Promise<GroupBlacklistEntry> {
  const res = await api.post<GroupBlacklistEntry>(
    `/groups/${groupId}/blacklist`,
    { userId },
    asForbidden,
  )
  return res.data
}

// DELETE /api/groups/{groupId}/blacklist/{userId} (owner) -> 204
export async function removeFromBlacklist(groupId: number, userId: number): Promise<void> {
  await api.delete(`/groups/${groupId}/blacklist/${userId}`, asForbidden)
}
