import { useEffect, useMemo } from 'react'
import { useMyGroups } from '@/features/groups/hooks'

export interface GroupLocationSelectorProps {
  value: number | null
  onChange: (groupId: number | null) => void
}

export function GroupLocationSelector({ value, onChange }: GroupLocationSelectorProps) {
  const { data: groups = [], isLoading } = useMyGroups()

  const options = useMemo(
    () => groups.map((group) => ({ id: group.id, name: group.name })),
    [groups],
  )

  useEffect(() => {
    if (options.length === 0) {
      onChange(null)
      return
    }

    if (value == null || !options.some((option) => option.id === value)) {
      onChange(options[0].id)
    }
  }, [onChange, options, value])

  if (isLoading) {
    return <p className="text-sm text-gray-600">Loading your groups…</p>
  }

  if (options.length === 0) {
    return (
      <p className="text-sm text-gray-600">
        You are not in any groups yet. Live location sharing is available to group members only.
      </p>
    )
  }

  return (
    <label className="flex flex-col gap-1 text-sm text-gray-700">
      <span className="font-medium text-gray-900">Group for live location</span>
      <select
        className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 shadow-sm"
        value={value ?? options[0].id}
        onChange={(event) => onChange(Number(event.target.value))}
      >
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.name}
          </option>
        ))}
      </select>
    </label>
  )
}
