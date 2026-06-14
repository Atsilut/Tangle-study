import { api, getList } from '@/lib/apiClient'
import type { FriendsListVisibility } from '@/types/api'

export interface User {
  id: number
  email?: string | null
  nickname: string
  friendsListVisibility: FriendsListVisibility
  createdAt: string
  updatedAt: string
}

export interface UpdateProfileRequest {
  id: number
  nickname: string
}

export interface UpdateProfileResponse {
  nickname: string
  updatedAt: string
}

export interface UpdatePrivacyResponse {
  friendsListVisibility: FriendsListVisibility
  updatedAt: string
}

// GET /api/users -> 200 [] (non-null even when empty)
export function getUsers(): Promise<User[]> {
  return getList<User>('/users')
}

// GET /api/users/{id} -> 200 | 404
export async function getUser(id: number): Promise<User> {
  const res = await api.get<User>(`/users/${id}`)
  return res.data
}

// PATCH /api/users (JWT, self) -> 200
export async function updateProfile(body: UpdateProfileRequest): Promise<UpdateProfileResponse> {
  const res = await api.patch<UpdateProfileResponse>('/users', body)
  return res.data
}

// PATCH /api/users/privacy (JWT) -> 200
export async function updatePrivacy(
  friendsListVisibility: FriendsListVisibility,
): Promise<UpdatePrivacyResponse> {
  const res = await api.patch<UpdatePrivacyResponse>('/users/privacy', { friendsListVisibility })
  return res.data
}

// DELETE /api/users/{id} (JWT, self) -> 204
export async function deleteUser(id: number): Promise<void> {
  await api.delete(`/users/${id}`)
}
