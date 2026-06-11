import { api } from '@/lib/apiClient'
import type { GroupJoinPolicy, GroupVisibility } from '@/types/api'

export interface Group {
  id: number
  name: string
  description: string
  visibility: GroupVisibility
  joinPolicy: GroupJoinPolicy
  memberCount: number
  createdAt: string
  updatedAt: string
}

export interface CreateGroupRequest {
  name: string
  description: string
  visibility: GroupVisibility
  joinPolicy?: GroupJoinPolicy
}

export interface UpdateGroupRequest {
  id: number
  name: string
  description: string
  visibility: GroupVisibility
  joinPolicy: GroupJoinPolicy
}

// POST /api/groups (JWT) -> 201 GroupResponseDto (caller becomes owner)
export async function createGroup(body: CreateGroupRequest): Promise<Group> {
  const res = await api.post<Group>('/groups', body)
  return res.data
}

// GET /api/groups/{id} (JWT) -> 200 (private: members only)
export async function getGroup(id: number): Promise<Group> {
  const res = await api.get<Group>(`/groups/${id}`)
  return res.data
}

// Owner/admin-only endpoints return 401 on permission denial; treat that as
// "forbidden" so a non-admin caller sees an error instead of being logged out.
const asForbidden = { treatUnauthorizedAsForbidden: true }

// PATCH /api/groups (JWT, owner/admin) -> 200
export async function updateGroup(body: UpdateGroupRequest): Promise<Group> {
  const res = await api.patch<Group>('/groups', body, asForbidden)
  return res.data
}

// PATCH /api/groups/transfer (JWT, owner) -> 200
export async function transferOwnership(id: number, newOwnerUserId: number): Promise<Group> {
  const res = await api.patch<Group>('/groups/transfer', { id, newOwnerUserId }, asForbidden)
  return res.data
}

// DELETE /api/groups/{id} (JWT, owner) -> 204
export async function deleteGroup(id: number): Promise<void> {
  await api.delete(`/groups/${id}`, asForbidden)
}

// POST /api/groups/{id}/join (JWT) -> 200 (open join policy only)
export async function joinGroup(id: number): Promise<void> {
  await api.post(`/groups/${id}/join`)
}
