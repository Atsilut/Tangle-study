import { api, getList } from '@/lib/apiClient'

export interface Friendship {
  id: number
  otherUserId: number
  otherUserNickname: string
  createdAt: string
  updatedAt: string
}

export interface FriendRequest {
  id: number
  requesterId: number
  addresseeId: number
  otherUserId: number
  otherUserNickname: string
  isPending: boolean
  isIncoming: boolean
  createdAt: string
  updatedAt: string
}

// GET /api/friendships/me -> 200 list | 204
export function getMyFriends(): Promise<Friendship[]> {
  return getList<Friendship>('/friendships/me')
}

// GET /api/friendships/users/{userId} -> 200 list | 204 (privacy-gated)
export function getUserFriends(userId: number): Promise<Friendship[]> {
  return getList<Friendship>(`/friendships/users/${userId}`)
}

// DELETE /api/friendships/{id} -> 204
export async function removeFriend(id: number): Promise<void> {
  await api.delete(`/friendships/${id}`)
}

// POST /api/friendships/requests -> 201 (created) | 200 (instant mutual friendship)
export async function sendFriendRequest(addresseeId: number): Promise<void> {
  await api.post('/friendships/requests', { addresseeId })
}

// GET /api/friendships/requests/pending -> 200 list | 204 (incoming + outgoing)
export function getPendingRequests(): Promise<FriendRequest[]> {
  return getList<FriendRequest>('/friendships/requests/pending')
}

// GET /api/friendships/requests/ignored -> 200 list | 204 (incoming ignored)
export function getIgnoredRequests(): Promise<FriendRequest[]> {
  return getList<FriendRequest>('/friendships/requests/ignored')
}

// POST /api/friendships/requests/{id}/accept -> 200
export async function acceptRequest(id: number): Promise<void> {
  await api.post(`/friendships/requests/${id}/accept`)
}

// POST /api/friendships/requests/{id}/ignore -> 204
export async function ignoreRequest(id: number): Promise<void> {
  await api.post(`/friendships/requests/${id}/ignore`)
}

// DELETE /api/friendships/requests/{id} -> 204 (cancel outgoing)
export async function cancelRequest(id: number): Promise<void> {
  await api.delete(`/friendships/requests/${id}`)
}

// DELETE /api/friendships/requests/{id}/reject -> 204
export async function rejectRequest(id: number): Promise<void> {
  await api.delete(`/friendships/requests/${id}/reject`)
}
