import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  acceptRequest,
  cancelRequest,
  getIgnoredRequests,
  getMyFriends,
  getPendingRequests,
  getUserFriends,
  ignoreRequest,
  rejectRequest,
  removeFriend,
  sendFriendRequest,
} from './api'

export const friendKeys = {
  all: ['friends'] as const,
  myFriends: () => [...friendKeys.all, 'me'] as const,
  userFriends: (userId: number) => [...friendKeys.all, 'user', userId] as const,
  pending: () => [...friendKeys.all, 'requests', 'pending'] as const,
  ignored: () => [...friendKeys.all, 'requests', 'ignored'] as const,
}

export function useMyFriends() {
  return useQuery({ queryKey: friendKeys.myFriends(), queryFn: getMyFriends })
}

export function useUserFriends(userId: number | null) {
  return useQuery({
    queryKey: friendKeys.userFriends(userId ?? -1),
    queryFn: () => getUserFriends(userId as number),
    enabled: userId != null,
  })
}

export function usePendingRequests() {
  return useQuery({ queryKey: friendKeys.pending(), queryFn: getPendingRequests })
}

export function useIgnoredRequests() {
  return useQuery({ queryKey: friendKeys.ignored(), queryFn: getIgnoredRequests })
}

// Invalidate everything friend-related; request actions ripple into friends
// and across the pending/ignored lists.
function useFriendMutation<TArgs>(fn: (args: TArgs) => Promise<void>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: friendKeys.all }),
  })
}

export function useSendFriendRequest() {
  return useFriendMutation((addresseeId: number) => sendFriendRequest(addresseeId))
}

export function useRemoveFriend() {
  return useFriendMutation((id: number) => removeFriend(id))
}

export function useAcceptRequest() {
  return useFriendMutation((id: number) => acceptRequest(id))
}

export function useIgnoreRequest() {
  return useFriendMutation((id: number) => ignoreRequest(id))
}

export function useCancelRequest() {
  return useFriendMutation((id: number) => cancelRequest(id))
}

export function useRejectRequest() {
  return useFriendMutation((id: number) => rejectRequest(id))
}
