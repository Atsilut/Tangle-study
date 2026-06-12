import { api, getList } from '@/lib/apiClient'
import type { GroupRole } from '@/types/api'

export interface GroupMember {
  userId: number
  nickname: string
  role: GroupRole
  createdAt: string
  updatedAt: string
}

// GET /api/groups/{groupId}/members (JWT) -> 200 list | 204
export function getGroupMembers(groupId: number): Promise<GroupMember[]> {
  return getList<GroupMember>(`/groups/${groupId}/members`, {
    treatUnauthorizedAsForbidden: true,
  })
}

// PATCH /api/groups/{groupId}/members/{userId} (JWT, owner) -> 200
export async function updateMemberRole(
  groupId: number,
  userId: number,
  role: GroupRole,
): Promise<GroupMember> {
  const res = await api.patch<GroupMember>(
    `/groups/${groupId}/members/${userId}`,
    { role },
    { treatUnauthorizedAsForbidden: true },
  )
  return res.data
}

// DELETE /api/groups/{groupId}/members/{userId} (JWT) -> 204
// Leave (self), kick (admin/owner), or remove an admin (owner only).
export async function removeMember(groupId: number, userId: number): Promise<void> {
  await api.delete(`/groups/${groupId}/members/${userId}`, {
    treatUnauthorizedAsForbidden: true,
  })
}
