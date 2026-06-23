import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { Card, CardBody, CardHeader, EmptyState } from '@/components/ui'
import { useAuthStore } from '@/stores/authStore'
import { FriendsListVisibility } from '@/types/api'
import { useMyFriends, useUserFriends } from '../hooks'

interface UserFriendsListProps {
  userId: number
  nickname: string
  visibility: FriendsListVisibility
  isSelf: boolean
}

export function UserFriendsList({ userId, nickname, visibility, isSelf }: UserFriendsListProps) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)

  const myFriends = useMyFriends({ enabled: isSelf })
  const theirFriends = useUserFriends(userId, {
    enabled: !isSelf && isAuthenticated && visibility !== FriendsListVisibility.Private,
  })

  if (!isSelf && !isAuthenticated) {
    if (visibility === FriendsListVisibility.Private) {
      return (
        <FriendsCard>
          <EmptyState
            title="Friends list is private"
            description="This user has chosen to hide their friends list."
          />
        </FriendsCard>
      )
    }
    return (
      <FriendsCard>
        <EmptyState
          title="Sign in to view friends"
          description={`Sign in to see whether ${nickname}'s friends list is visible to you.`}
          action={
            <Link to="/login" className="text-sm font-medium text-blue-600 hover:underline">
              Sign in
            </Link>
          }
        />
      </FriendsCard>
    )
  }

  if (!isSelf && visibility === FriendsListVisibility.Private) {
    return (
      <FriendsCard>
        <EmptyState
          title="Friends list is private"
          description="This user has chosen to hide their friends list."
        />
      </FriendsCard>
    )
  }

  if (isSelf) {
    return (
      <FriendsCard>
        <QueryBoundary
          isLoading={myFriends.isLoading}
          isError={myFriends.isError}
          error={myFriends.error}
          onRetry={() => myFriends.refetch()}
        >
          <FriendsListContent friends={myFriends.data ?? []} emptyTitle="No friends yet" />
        </QueryBoundary>
      </FriendsCard>
    )
  }

  return (
    <FriendsCard>
      <QueryBoundary
        isLoading={theirFriends.isLoading}
        isError={theirFriends.isError}
        error={theirFriends.error}
        onRetry={() => theirFriends.refetch()}
      >
        {theirFriends.data?.access === 'denied' ? (
          <EmptyState title="Friends list unavailable" description={theirFriends.data.message} />
        ) : (
          <FriendsListContent
            friends={theirFriends.data?.friends ?? []}
            emptyTitle={`${nickname} has no friends yet`}
          />
        )}
      </QueryBoundary>
    </FriendsCard>
  )
}

function FriendsCard({ children }: { children: ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <h2 className="text-lg font-semibold text-gray-900">Friends</h2>
      </CardHeader>
      <CardBody>{children}</CardBody>
    </Card>
  )
}

interface FriendsListContentProps {
  friends: { id: number; otherUserId: number; otherUserNickname: string }[]
  emptyTitle: string
}

function FriendsListContent({ friends, emptyTitle }: FriendsListContentProps) {
  if (friends.length === 0) {
    return <EmptyState title={emptyTitle} />
  }

  return (
    <ul className="flex flex-col gap-2">
      {friends.map((f) => (
        <li key={f.id}>
          <UserRow userId={f.otherUserId} nickname={f.otherUserNickname} />
        </li>
      ))}
    </ul>
  )
}
