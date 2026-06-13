import {
  BoardVisibility,
  BoardWriteability,
  GroupInvitePolicy,
  GroupJoinPolicy,
  GroupRole,
  GroupVisibility,
} from '@/types/api'

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

export const invitePolicyLabels: Record<GroupInvitePolicy, string> = {
  [GroupInvitePolicy.AdminsOnly]: 'Admins only',
  [GroupInvitePolicy.ForAll]: 'All members',
}

export const invitePolicyOptions = [GroupInvitePolicy.AdminsOnly, GroupInvitePolicy.ForAll]

export function canInviteToGroup(
  invitePolicy: GroupInvitePolicy,
  role: GroupRole | null | undefined,
): boolean {
  if (role == null) return false
  if (invitePolicy === GroupInvitePolicy.ForAll) return true
  return role === GroupRole.Owner || role === GroupRole.Admin
}

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

export const boardWriteabilityLabels: Record<BoardWriteability, string> = {
  [BoardWriteability.AdminOnly]: 'Admins only',
  [BoardWriteability.MembersOnly]: 'Members only',
  [BoardWriteability.ForAll]: 'Everyone who can view',
}

export const boardWriteabilityOptions = [
  BoardWriteability.ForAll,
  BoardWriteability.MembersOnly,
  BoardWriteability.AdminOnly,
]
