import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { blockUser, getMyBlocks, unblockUser } from './api'

export const blockKeys = {
  all: ['blocks'] as const,
  myBlocks: () => [...blockKeys.all, 'me'] as const,
}

export function useMyBlocks() {
  return useQuery({ queryKey: blockKeys.myBlocks(), queryFn: getMyBlocks })
}

export function useBlockUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (blockedUserId: number) => blockUser(blockedUserId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: blockKeys.myBlocks() }),
  })
}

export function useUnblockUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unblockUser(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: blockKeys.myBlocks() }),
  })
}
