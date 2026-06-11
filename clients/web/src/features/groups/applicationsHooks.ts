import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  applyToGroup,
  approveApplication,
  cancelApplication,
  getGroupApplications,
  getGroupIgnoredApplications,
  getMyApplications,
  ignoreApplication,
  rejectApplication,
} from './applicationsApi'
import { groupKeys } from './hooks'

export const applicationKeys = {
  all: ['applications'] as const,
  mine: () => [...applicationKeys.all, 'me'] as const,
  group: (groupId: number) => [...applicationKeys.all, 'group', groupId] as const,
  groupIgnored: (groupId: number) => [...applicationKeys.all, 'group', groupId, 'ignored'] as const,
}

export function useMyApplications() {
  return useQuery({ queryKey: applicationKeys.mine(), queryFn: getMyApplications })
}

export function useGroupApplications(groupId: number | null) {
  return useQuery({
    queryKey: applicationKeys.group(groupId ?? -1),
    queryFn: () => getGroupApplications(groupId as number),
    enabled: groupId != null,
  })
}

export function useGroupIgnoredApplications(groupId: number | null) {
  return useQuery({
    queryKey: applicationKeys.groupIgnored(groupId ?? -1),
    queryFn: () => getGroupIgnoredApplications(groupId as number),
    enabled: groupId != null,
  })
}

export function useApplyToGroup(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => applyToGroup(groupId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: applicationKeys.all })
      queryClient.invalidateQueries({ queryKey: groupKeys.detail(groupId) })
    },
  })
}

// Manage/cancel actions ripple into applications, group, and member lists.
function useApplicationAction(fn: (id: number) => Promise<void>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: applicationKeys.all })
      queryClient.invalidateQueries({ queryKey: groupKeys.all })
    },
  })
}

export function useApproveApplication() {
  return useApplicationAction(approveApplication)
}

export function useIgnoreApplication() {
  return useApplicationAction(ignoreApplication)
}

export function useRejectApplication() {
  return useApplicationAction(rejectApplication)
}

export function useCancelApplication() {
  return useApplicationAction(cancelApplication)
}
