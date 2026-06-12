import { api, getList } from '@/lib/apiClient'

export interface GroupInvitation {
  id: number
  groupId: number
  groupName: string
  inviterId: number
  inviteeId: number
  otherUserId: number
  otherUserNickname: string
  isPending: boolean
  isIncoming: boolean
  createdAt: string
  updatedAt: string
}

// POST /api/groups/{groupId}/invitations (JWT, owner/admin) -> 201 | 200 (reciprocal)
export async function inviteToGroup(groupId: number, inviteeId: number): Promise<void> {
  await api.post(
    `/groups/${groupId}/invitations`,
    { inviteeId },
    { treatUnauthorizedAsForbidden: true },
  )
}

// GET /api/invitations/me -> 200 list | 204 (my pending + ignored outgoing)
export function getMyInvitations(): Promise<GroupInvitation[]> {
  return getList<GroupInvitation>('/invitations/me')
}

// GET /api/invitations/ignored -> 200 list | 204 (incoming ignored)
export function getIgnoredInvitations(): Promise<GroupInvitation[]> {
  return getList<GroupInvitation>('/invitations/ignored')
}

// POST /api/invitations/{id}/accept (invitee) -> 200
export async function acceptInvitation(id: number): Promise<void> {
  await api.post(`/invitations/${id}/accept`)
}

// POST /api/invitations/{id}/ignore (invitee) -> 204
export async function ignoreInvitation(id: number): Promise<void> {
  await api.post(`/invitations/${id}/ignore`)
}

// POST /api/invitations/{id}/reject (invitee) -> 204
export async function rejectInvitation(id: number): Promise<void> {
  await api.post(`/invitations/${id}/reject`)
}

// DELETE /api/invitations/{id} (inviter or admin/owner) -> 204
export async function cancelInvitation(id: number): Promise<void> {
  await api.delete(`/invitations/${id}`, { treatUnauthorizedAsForbidden: true })
}
