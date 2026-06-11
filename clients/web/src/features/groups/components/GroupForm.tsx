import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, Input, Select, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { GroupJoinPolicy, GroupVisibility } from '@/types/api'
import {
  groupVisibilityLabels,
  groupVisibilityOptions,
  joinPolicyLabels,
  joinPolicyOptions,
} from '../labels'

export interface GroupFormValues {
  name: string
  description: string
  visibility: GroupVisibility
  joinPolicy: GroupJoinPolicy
}

export interface GroupFormProps {
  initial?: GroupFormValues
  submitLabel: string
  isPending: boolean
  error?: unknown
  onSubmit: (values: GroupFormValues) => void
  onCancel?: () => void
}

// Reused for create and edit. Name <= 50, description <= 500 (backend limits).
export function GroupForm({
  initial,
  submitLabel,
  isPending,
  error,
  onSubmit,
  onCancel,
}: GroupFormProps) {
  const [name, setName] = useState(initial?.name ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [visibility, setVisibility] = useState<GroupVisibility>(
    initial?.visibility ?? GroupVisibility.Public,
  )
  const [joinPolicy, setJoinPolicy] = useState<GroupJoinPolicy>(
    initial?.joinPolicy ?? GroupJoinPolicy.Requestable,
  )

  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({ name: name.trim(), description: description.trim(), visibility, joinPolicy })
  }

  return (
    <form className="flex flex-col gap-4" onSubmit={submit}>
      {error != null && <ErrorState title="Could not save" message={getErrorMessage(error)} />}
      <FormField label="Name" required>
        {({ id }) => (
          <Input
            id={id}
            value={name}
            maxLength={50}
            onChange={(e) => setName(e.target.value)}
            required
          />
        )}
      </FormField>
      <FormField label="Description">
        {({ id }) => (
          <TextArea
            id={id}
            value={description}
            maxLength={500}
            rows={4}
            onChange={(e) => setDescription(e.target.value)}
          />
        )}
      </FormField>
      <FormField label="Visibility">
        {({ id }) => (
          <Select
            id={id}
            value={visibility}
            onChange={(e) => setVisibility(Number(e.target.value))}
          >
            {groupVisibilityOptions.map((v) => (
              <option key={v} value={v}>
                {groupVisibilityLabels[v]}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <FormField label="Join policy">
        {({ id }) => (
          <Select
            id={id}
            value={joinPolicy}
            onChange={(e) => setJoinPolicy(Number(e.target.value))}
          >
            {joinPolicyOptions.map((p) => (
              <option key={p} value={p}>
                {joinPolicyLabels[p]}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <div className="flex items-center gap-2">
        <Button type="submit" isLoading={isPending} disabled={name.trim() === ''}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
    </form>
  )
}
