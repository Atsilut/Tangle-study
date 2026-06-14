import { describe, expect, it } from 'vitest'
import { GroupInvitePolicy, GroupRole } from '@/types/api'
import { canInviteToGroup } from './labels'

describe('canInviteToGroup', () => {
  it('denies when role is null', () => {
    expect(canInviteToGroup(GroupInvitePolicy.ForAll, null)).toBe(false)
  })

  it('allows any member when ForAll', () => {
    expect(canInviteToGroup(GroupInvitePolicy.ForAll, GroupRole.Member)).toBe(true)
  })

  it('allows only admins and owner when AdminsOnly', () => {
    expect(canInviteToGroup(GroupInvitePolicy.AdminsOnly, GroupRole.Member)).toBe(false)
    expect(canInviteToGroup(GroupInvitePolicy.AdminsOnly, GroupRole.Admin)).toBe(true)
    expect(canInviteToGroup(GroupInvitePolicy.AdminsOnly, GroupRole.Owner)).toBe(true)
  })
})
