import { useState } from 'react'
import { Button, EmptyState } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { getErrorMessage } from '@/lib/apiError'
import { useMyBlocks, useUnblockUser } from '../hooks'

export function BlocksPage() {
  const { data, isLoading, isError, error, refetch } = useMyBlocks()

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Blocked users</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
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
  const [actionError, setActionError] = useState<string | null>(null)
  return (
    <>
      <UserRow
        userId={userId}
        nickname={nickname}
        actions={
          <Button
            size="sm"
            variant="secondary"
            isLoading={unblock.isPending}
            onClick={() =>
              unblock.mutate(id, {
                onError: (error) => setActionError(getErrorMessage(error)),
                onSuccess: () => setActionError(null),
              })
            }
          >
            Unblock
          </Button>
        }
      />
      {actionError && (
        <p className="text-sm text-red-600" role="alert">
          {actionError}
        </p>
      )}
    </>
  )
}
