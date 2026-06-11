import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/authStore'
import type { FriendsListVisibility } from '@/types/api'
import {
  deleteUser,
  getUser,
  getUsers,
  updatePrivacy,
  updateProfile,
  type UpdateProfileRequest,
} from './api'

export const userKeys = {
  all: ['users'] as const,
  lists: () => [...userKeys.all, 'list'] as const,
  details: () => [...userKeys.all, 'detail'] as const,
  detail: (id: number) => [...userKeys.details(), id] as const,
}

export function useUsers() {
  return useQuery({ queryKey: userKeys.lists(), queryFn: getUsers })
}

export function useUser(id: number | null) {
  return useQuery({
    queryKey: userKeys.detail(id ?? -1),
    queryFn: () => getUser(id as number),
    enabled: id != null,
  })
}

// There is no /api/users/me; resolve the current user from the JWT sub claim.
export function useCurrentUser() {
  const userId = useAuthStore((s) => s.userId)
  return useUser(userId)
}

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateProfileRequest) => updateProfile(body),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: userKeys.detail(variables.id) })
      queryClient.invalidateQueries({ queryKey: userKeys.lists() })
    },
  })
}

export function useUpdatePrivacy() {
  const queryClient = useQueryClient()
  const userId = useAuthStore((s) => s.userId)
  return useMutation({
    mutationFn: (visibility: FriendsListVisibility) => updatePrivacy(visibility),
    onSuccess: () => {
      if (userId != null) queryClient.invalidateQueries({ queryKey: userKeys.detail(userId) })
    },
  })
}

export function useDeleteAccount() {
  const clear = useAuthStore((s) => s.clear)
  return useMutation({
    mutationFn: (id: number) => deleteUser(id),
    onSuccess: () => clear(),
  })
}
