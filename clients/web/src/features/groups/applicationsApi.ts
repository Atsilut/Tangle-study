import { api, getList } from '@/lib/apiClient'

export interface GroupApplication {
  id: number
  groupId: number
  applicantId: number
  applicantNickname: string
  isPending: boolean
  isIncoming: boolean
  createdAt: string
  updatedAt: string
}

const asForbidden = { treatUnauthorizedAsForbidden: true }

// POST /api/groups/{groupId}/applications (JWT) -> 201 | 200 (reciprocal invite)
export async function applyToGroup(groupId: number): Promise<void> {
  await api.post(`/groups/${groupId}/applications`)
}

// GET /api/applications/me -> 200 list | 204 (my pending + ignored outgoing)
export function getMyApplications(): Promise<GroupApplication[]> {
  return getList<GroupApplication>('/applications/me')
}

// GET /api/groups/{groupId}/applications (JWT, owner/admin) -> 200 list (may be [])
export function getGroupApplications(groupId: number): Promise<GroupApplication[]> {
  return getList<GroupApplication>(`/groups/${groupId}/applications`, asForbidden)
}

// GET /api/groups/{groupId}/applications/ignored (JWT, owner/admin) -> 200 list | 204
export function getGroupIgnoredApplications(groupId: number): Promise<GroupApplication[]> {
  return getList<GroupApplication>(`/groups/${groupId}/applications/ignored`, asForbidden)
}

// POST /api/applications/{id}/approve (owner/admin) -> 200
export async function approveApplication(id: number): Promise<void> {
  await api.post(`/applications/${id}/approve`, undefined, asForbidden)
}

// POST /api/applications/{id}/ignore (owner/admin) -> 204
export async function ignoreApplication(id: number): Promise<void> {
  await api.post(`/applications/${id}/ignore`, undefined, asForbidden)
}

// POST /api/applications/{id}/reject (owner/admin) -> 204
export async function rejectApplication(id: number): Promise<void> {
  await api.post(`/applications/${id}/reject`, undefined, asForbidden)
}

// DELETE /api/applications/{id} (applicant) -> 204
export async function cancelApplication(id: number): Promise<void> {
  await api.delete(`/applications/${id}`)
}
