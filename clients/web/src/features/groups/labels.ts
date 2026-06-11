import { BoardVisibility, GroupJoinPolicy, GroupRole, GroupVisibility } from '@/types/api'

export const groupVisibilityLabels: Record<GroupVisibility, string> = {
  [GroupVisibility.Private]: 'Private',
  [GroupVisibility.Public]: 'Public',
}

export const groupVisibilityOptions = [GroupVisibility.Public, GroupVisibility.Private]

export const joinPolicyLabels: Record<GroupJoinPolicy, string> = {
  [GroupJoinPolicy.Open]: 'Open',
  [GroupJoinPolicy.Requestable]: 'Requestable',
  [GroupJoinPolicy.InvitationOnly]: 'Invitation only',
}

export const joinPolicyOptions = [
  GroupJoinPolicy.Open,
  GroupJoinPolicy.Requestable,
  GroupJoinPolicy.InvitationOnly,
]

export const groupRoleLabels: Record<GroupRole, string> = {
  [GroupRole.Member]: 'Member',
  [GroupRole.Admin]: 'Admin',
  [GroupRole.Owner]: 'Owner',
}

export const boardVisibilityLabels: Record<BoardVisibility, string> = {
  [BoardVisibility.AdminOnly]: 'Admins only',
  [BoardVisibility.MembersOnly]: 'Members only',
  [BoardVisibility.ForAll]: 'Everyone',
}

export const boardVisibilityOptions = [
  BoardVisibility.ForAll,
  BoardVisibility.MembersOnly,
  BoardVisibility.AdminOnly,
]
