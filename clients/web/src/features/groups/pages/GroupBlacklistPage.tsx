import { type FormEvent, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  ErrorState,
  FormField,
  Input,
} from '@/components/ui'
import { CenteredSpinner } from '@/components/common/CenteredSpinner'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { UserRow } from '@/components/common/UserRow'
import { getErrorMessage } from '@/lib/apiError'
import { GroupRole } from '@/types/api'
import { useAddToBlacklist, useBlacklist, useRemoveFromBlacklist } from '../blacklistHooks'
import { useMyGroupRole } from '../membersHooks'
import type { GroupBlacklistEntry } from '../blacklistApi'

export function GroupBlacklistPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const valid = Number.isFinite(groupId)
  const { role, isLoading: roleLoading } = useMyGroupRole(valid ? groupId : null)
  const blacklist = useBlacklist(valid ? groupId : null)

  if (roleLoading) return <CenteredSpinner />
  if (role !== GroupRole.Owner) {
    return <Navigate to={`/groups/${groupId}`} replace />
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}`} className="text-sm text-blue-600 hover:underline">
        Back to group
      </Link>
      <h1 className="text-2xl font-bold text-gray-900">Blacklist</h1>
      <AddCard groupId={groupId} />
      <QueryBoundary
        isLoading={blacklist.isLoading}
        isError={blacklist.isError}
        onRetry={() => blacklist.refetch()}
      >
        {blacklist.data && blacklist.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {blacklist.data.map((entry) => (
              <li key={entry.id}>
                <BlacklistRow groupId={groupId} entry={entry} />
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No blacklisted users" />
        )}
      </QueryBoundary>
    </div>
  )
}

function AddCard({ groupId }: { groupId: number }) {
  const add = useAddToBlacklist(groupId)
  const [userId, setUserId] = useState('')

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    const id = Number(userId)
    if (Number.isFinite(id) && id > 0) {
      add.mutate(id, { onSuccess: () => setUserId('') })
    }
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-gray-900">Blacklist a user</h2>
      </CardHeader>
      <CardBody className="flex flex-col gap-3">
        <p className="text-sm text-gray-600">
          Blacklisting removes the user from the group and clears any pending join requests.
        </p>
        <form className="flex items-end gap-2" onSubmit={onSubmit}>
          <FormField label="User ID" className="flex-1">
            {({ id }) => (
              <Input
                id={id}
                type="number"
                min={1}
                value={userId}
                onChange={(e) => setUserId(e.target.value)}
                placeholder="e.g. 2"
              />
            )}
          </FormField>
          <Button type="submit" variant="danger" isLoading={add.isPending} disabled={userId.trim() === ''}>
            Blacklist
          </Button>
        </form>
        {add.isError && <ErrorState title="Could not blacklist" message={getErrorMessage(add.error)} />}
        {add.isSuccess && <Badge color="green">User blacklisted</Badge>}
      </CardBody>
    </Card>
  )
}

function BlacklistRow({ groupId, entry }: { groupId: number; entry: GroupBlacklistEntry }) {
  const remove = useRemoveFromBlacklist(groupId)
  return (
    <UserRow
      userId={entry.userId}
      nickname={entry.userNickname}
      actions={
        <Button
          size="sm"
          variant="secondary"
          isLoading={remove.isPending}
          onClick={() => remove.mutate(entry.userId)}
        >
          Remove
        </Button>
      }
    />
  )
}
