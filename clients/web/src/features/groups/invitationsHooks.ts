import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  acceptInvitation,
  cancelInvitation,
  getIgnoredInvitations,
  getMyInvitations,
  ignoreInvitation,
  inviteToGroup,
  rejectInvitation,
} from './invitationsApi'
import { groupKeys } from './hooks'

export const invitationKeys = {
  all: ['invitations'] as const,
  mine: () => [...invitationKeys.all, 'me'] as const,
  ignored: () => [...invitationKeys.all, 'ignored'] as const,
}

export function useMyInvitations() {
  return useQuery({ queryKey: invitationKeys.mine(), queryFn: getMyInvitations })
}

export function useIgnoredInvitations() {
  return useQuery({ queryKey: invitationKeys.ignored(), queryFn: getIgnoredInvitations })
}

export function useInviteToGroup(groupId: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (inviteeId: number) => inviteToGroup(groupId, inviteeId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: invitationKeys.all }),
  })
}

// Invitee/inviter actions invalidate invitations and (on accept) group state.
function useInvitationAction(fn: (id: number) => Promise<void>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: invitationKeys.all })
      queryClient.invalidateQueries({ queryKey: groupKeys.all })
    },
  })
}

export function useAcceptInvitation() {
  return useInvitationAction(acceptInvitation)
}

export function useIgnoreInvitation() {
  return useInvitationAction(ignoreInvitation)
}

export function useRejectInvitation() {
  return useInvitationAction(rejectInvitation)
}

export function useCancelInvitation() {
  return useInvitationAction(cancelInvitation)
}
