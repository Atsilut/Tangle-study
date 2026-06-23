import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { addToBlacklist, getBlacklist, removeFromBlacklist } from './blacklistApi'
import { groupKeys } from './hooks'
import { memberKeys } from './membersHooks'

export const blacklistKeys = {
  all: ['blacklist'] as const,
  list: (groupId: number) => [...blacklistKeys.all, 'group', groupId] as const,
}

export function useBlacklist(groupId: number | null) {
  return useQuery({
    queryKey: blacklistKeys.list(groupId ?? -1),
    queryFn: () => getBlacklist(groupId as number),
    enabled: groupId != null,
  })
}

export function useAddToBlacklist(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: number) => addToBlacklist(groupId, userId),
    onSuccess: () => {
      // Blacklisting also kicks the member and clears pending requests.
      queryClient.invalidateQueries({ queryKey: blacklistKeys.list(groupId) })
      queryClient.invalidateQueries({ queryKey: memberKeys.list(groupId) })
      queryClient.invalidateQueries({ queryKey: groupKeys.detail(groupId) })
    },
  })
}

export function useRemoveFromBlacklist(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (userId: number) => removeFromBlacklist(groupId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: blacklistKeys.list(groupId) }),
  })
}
