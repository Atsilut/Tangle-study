import { FriendsListVisibility } from '@/types/api'

export const friendsListVisibilityLabels: Record<FriendsListVisibility, string> = {
  [FriendsListVisibility.Public]: 'Public',
  [FriendsListVisibility.FriendsOnly]: 'Friends only',
  [FriendsListVisibility.Private]: 'Private',
}

export const friendsListVisibilityOptions = [
  FriendsListVisibility.Public,
  FriendsListVisibility.FriendsOnly,
  FriendsListVisibility.Private,
]
