import { type FormEvent, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  FormField,
  Input,
  Select,
} from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { FriendsListVisibility } from '@/types/api'
import {
  useCurrentUser,
  useDeleteAccount,
  useUpdatePrivacy,
  useUpdateProfile,
} from '../hooks'
import { friendsListVisibilityLabels, friendsListVisibilityOptions } from '../labels'
import type { User } from '../api'

export function SettingsPage() {
  const { data: user, isLoading, isError, error, refetch } = useCurrentUser()

  return (
    <div className="flex max-w-lg flex-col gap-4">
      <h1 className="text-2xl font-bold text-gray-900">Settings</h1>
      <QueryBoundary isLoading={isLoading} isError={isError} error={error} onRetry={() => refetch()}>
        {/* Remount when the user changes so forms re-seed from fresh data. */}
        {user && <SettingsForms key={user.id} user={user} />}
      </QueryBoundary>
    </div>
  )
}

function SettingsForms({ user }: { user: User }) {
  const navigate = useNavigate()
  const updateProfile = useUpdateProfile()
  const updatePrivacy = useUpdatePrivacy()
  const deleteAccount = useDeleteAccount()

  const [nickname, setNickname] = useState(user.nickname)
  const [visibility, setVisibility] = useState<FriendsListVisibility>(user.friendsListVisibility)
  const [confirmOpen, setConfirmOpen] = useState(false)

  const onSaveProfile = (e: FormEvent) => {
    e.preventDefault()
    updateProfile.mutate({ id: user.id, nickname })
  }

  const onSavePrivacy = (e: FormEvent) => {
    e.preventDefault()
    updatePrivacy.mutate(visibility)
  }

  const onDelete = () => {
    deleteAccount.mutate(user.id, {
      onSuccess: () => navigate('/login', { replace: true }),
    })
  }

  return (
    <>
      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-gray-900">Profile</h2>
        </CardHeader>
        <CardBody>
          <form className="flex flex-col gap-4" onSubmit={onSaveProfile}>
            <FormField label="Nickname" required>
              {({ id }) => (
                <Input
                  id={id}
                  value={nickname}
                  onChange={(e) => setNickname(e.target.value)}
                  required
                />
              )}
            </FormField>
            <div className="flex items-center gap-3">
              <Button
                type="submit"
                isLoading={updateProfile.isPending}
                disabled={nickname === user.nickname || nickname.trim() === ''}
              >
                Save
              </Button>
              {updateProfile.isSuccess && <span className="text-sm text-green-600">Saved.</span>}
            </div>
          </form>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-gray-900">Privacy</h2>
        </CardHeader>
        <CardBody>
          <form className="flex flex-col gap-4" onSubmit={onSavePrivacy}>
            <FormField label="Who can see your friends list">
              {({ id }) => (
                <Select
                  id={id}
                  value={visibility}
                  onChange={(e) => setVisibility(Number(e.target.value))}
                >
                  {friendsListVisibilityOptions.map((value) => (
                    <option key={value} value={value}>
                      {friendsListVisibilityLabels[value]}
                    </option>
                  ))}
                </Select>
              )}
            </FormField>
            <div className="flex items-center gap-3">
              <Button
                type="submit"
                isLoading={updatePrivacy.isPending}
                disabled={visibility === user.friendsListVisibility}
              >
                Save
              </Button>
              {updatePrivacy.isSuccess && <span className="text-sm text-green-600">Saved.</span>}
            </div>
          </form>
        </CardBody>
      </Card>

      <Card className="border-red-200">
        <CardHeader>
          <h2 className="text-sm font-semibold text-red-700">Danger zone</h2>
        </CardHeader>
        <CardBody className="flex items-center justify-between gap-4">
          <p className="text-sm text-gray-600">Permanently delete your account.</p>
          <Button variant="danger" onClick={() => setConfirmOpen(true)}>
            Delete account
          </Button>
        </CardBody>
      </Card>

      <ConfirmDialog
        isOpen={confirmOpen}
        title="Delete account"
        message="This permanently deletes your account. This cannot be undone."
        confirmLabel="Delete"
        destructive
        isLoading={deleteAccount.isPending}
        onConfirm={onDelete}
        onCancel={() => setConfirmOpen(false)}
      />
    </>
  )
}
