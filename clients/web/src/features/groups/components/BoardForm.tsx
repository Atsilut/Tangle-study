import { type FormEvent, useState } from 'react'
import { Button, ErrorState, FormField, Input, Select, TextArea } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { BoardVisibility, BoardWriteability } from '@/types/api'
import {
  boardVisibilityLabels,
  boardVisibilityOptions,
  boardWriteabilityLabels,
  boardWriteabilityOptions,
} from '../labels'

export interface BoardFormValues {
  name: string
  description: string
  visibility: BoardVisibility
  writeability: BoardWriteability
}

export interface BoardFormProps {
  initial?: BoardFormValues
  submitLabel: string
  isPending: boolean
  error?: unknown
  onSubmit: (values: BoardFormValues) => void
  onCancel?: () => void
}

// Reused for create and edit. Name capped at 100 chars (GroupBoard*RequestDto).
export function BoardForm({
  initial,
  submitLabel,
  isPending,
  error,
  onSubmit,
  onCancel,
}: BoardFormProps) {
  const [name, setName] = useState(initial?.name ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [visibility, setVisibility] = useState<BoardVisibility>(
    initial?.visibility ?? BoardVisibility.ForAll,
  )
  const [writeability, setWriteability] = useState<BoardWriteability>(
    initial?.writeability ?? BoardWriteability.MembersOnly,
  )

  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({ name: name.trim(), description: description.trim(), visibility, writeability })
  }

  return (
    <form className="flex flex-col gap-4" onSubmit={submit}>
      {error != null && <ErrorState title="Could not save" message={getErrorMessage(error)} />}
      <FormField label="Name" required>
        {({ id }) => (
          <Input
            id={id}
            value={name}
            maxLength={100}
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
            rows={3}
            onChange={(e) => setDescription(e.target.value)}
          />
        )}
      </FormField>
      <FormField label="Who can read">
        {({ id }) => (
          <Select
            id={id}
            value={visibility}
            onChange={(e) => setVisibility(Number(e.target.value) as BoardVisibility)}
          >
            {boardVisibilityOptions.map((v) => (
              <option key={v} value={v}>
                {boardVisibilityLabels[v]}
              </option>
            ))}
          </Select>
        )}
      </FormField>
      <FormField label="Who can post">
        {({ id }) => (
          <Select
            id={id}
            value={writeability}
            onChange={(e) => setWriteability(Number(e.target.value) as BoardWriteability)}
          >
            {boardWriteabilityOptions.map((v) => (
              <option key={v} value={v}>
                {boardWriteabilityLabels[v]}
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
