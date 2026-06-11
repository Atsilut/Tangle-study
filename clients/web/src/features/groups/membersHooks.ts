import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/authStore'
import type { GroupRole } from '@/types/api'
import { getGroupMembers, removeMember, updateMemberRole } from './membersApi'
import { groupKeys } from './hooks'

export const memberKeys = {
  list: (groupId: number) => [...groupKeys.detail(groupId), 'members'] as const,
}

export function useGroupMembers(groupId: number | null) {
  return useQuery({
    queryKey: memberKeys.list(groupId ?? -1),
    queryFn: () => getGroupMembers(groupId as number),
    enabled: groupId != null,
  })
}

// Current user's role in the group, or null if not a member / not loaded.
export function useMyGroupRole(groupId: number | null): {
  role: GroupRole | null
  isLoading: boolean
} {
  const userId = useAuthStore((s) => s.userId)
  const { data, isLoading } = useGroupMembers(groupId)
  const role = data?.find((m) => m.userId === userId)?.role ?? null
  return { role, isLoading }
}

export function useUpdateMemberRole(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, role }: { userId: number; role: GroupRole }) =>
      updateMemberRole(groupId, userId, role),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: memberKeys.list(groupId) }),
  })
}

export function useRemoveMember(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: number) => removeMember(groupId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: memberKeys.list(groupId) })
      queryClient.invalidateQueries({ queryKey: groupKeys.detail(groupId) })
    },
  })
}
