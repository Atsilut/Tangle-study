import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createGroup,
  deleteGroup,
  getDiscoverableGroups,
  getGroup,
  getMyGroups,
  joinGroup,
  transferOwnership,
  updateGroup,
  type CreateGroupRequest,
  type UpdateGroupRequest,
} from './api'

export const groupKeys = {
  all: ['groups'] as const,
  mine: () => [...groupKeys.all, 'mine'] as const,
  discover: () => [...groupKeys.all, 'discover'] as const,
  details: () => [...groupKeys.all, 'detail'] as const,
  detail: (id: number) => [...groupKeys.details(), id] as const,
}

export function useMyGroups() {
  return useQuery({ queryKey: groupKeys.mine(), queryFn: getMyGroups })
}

export function useDiscoverableGroups() {
  return useQuery({ queryKey: groupKeys.discover(), queryFn: getDiscoverableGroups })
}

export function useGroup(id: number | null) {
  return useQuery({
    queryKey: groupKeys.detail(id ?? -1),
    queryFn: () => getGroup(id as number),
    enabled: id != null,
  })
}

export function useCreateGroup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateGroupRequest) => createGroup(body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: groupKeys.mine() })
      queryClient.invalidateQueries({ queryKey: groupKeys.discover() })
    },
  })
}

export function useUpdateGroup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateGroupRequest) => updateGroup(body),
    onSuccess: (group) =>
      queryClient.invalidateQueries({ queryKey: groupKeys.detail(group.id) }),
  })
}

export function useTransferOwnership(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (newOwnerUserId: number) => transferOwnership(groupId, newOwnerUserId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: groupKeys.detail(groupId) }),
  })
}

export function useDeleteGroup() {
  return useMutation({ mutationFn: (id: number) => deleteGroup(id) })
}

export function useJoinGroup(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => joinGroup(groupId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: groupKeys.detail(groupId) })
      queryClient.invalidateQueries({ queryKey: groupKeys.mine() })
    },
  })
}
