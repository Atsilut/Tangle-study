import { Button, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { useMyBlocks, useUnblockUser } from '../hooks'

export function BlocksPage() {
  const { data, isLoading, isError, refetch } = useMyBlocks()

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Blocked users</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} onRetry={() => refetch()}>
        {data && data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {data.map((block) => (
              <li key={block.id}>
                <BlockRow
                  id={block.id}
                  userId={block.blockedUserId}
                  nickname={block.blockedUserNickname}
                />
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No blocked users" />
        )}
      </QueryBoundary>
    </div>
  )
}

function BlockRow({ id, userId, nickname }: { id: number; userId: number; nickname: string }) {
  const unblock = useUnblockUser()
  return (
    <UserRow
      userId={userId}
      nickname={nickname}
      actions={
        <Button
          size="sm"
          variant="secondary"
          isLoading={unblock.isPending}
          onClick={() => unblock.mutate(id)}
        >
          Unblock
        </Button>
      }
    />
  )
}
