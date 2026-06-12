import { type FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Button, Card, CardBody, CardHeader, FormField, Input } from '@/components/ui'

// There is no list-all-groups endpoint, so this landing page offers creating a
// group and looking one up by id (plus links to invitations/applications).
export function GroupsPage() {
  const navigate = useNavigate()
  const [lookupId, setLookupId] = useState('')

  const onLookup = (e: FormEvent) => {
    e.preventDefault()
    const id = Number(lookupId)
    if (Number.isFinite(id) && id > 0) navigate(`/groups/${id}`)
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Groups</h1>
        <Link to="/groups/new">
          <Button size="sm">New group</Button>
        </Link>
      </div>

      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold text-gray-900">Open a group</h2>
        </CardHeader>
        <CardBody>
          <form className="flex items-end gap-2" onSubmit={onLookup}>
            <FormField label="Group ID" className="flex-1">
              {({ id }) => (
                <Input
                  id={id}
                  type="number"
                  min={1}
                  value={lookupId}
                  onChange={(e) => setLookupId(e.target.value)}
                  placeholder="e.g. 1"
                />
              )}
            </FormField>
            <Button type="submit" disabled={lookupId.trim() === ''}>
              Open
            </Button>
          </form>
        </CardBody>
      </Card>

      <div className="flex gap-4 text-sm">
        <Link to="/invitations" className="text-blue-600 hover:underline">
          My invitations
        </Link>
        <Link to="/applications" className="text-blue-600 hover:underline">
          My applications
        </Link>
      </div>
    </div>
  )
}
