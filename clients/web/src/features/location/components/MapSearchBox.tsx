import { useEffect, useId, useState } from 'react'
import { Input } from '@/components/ui'
import { getErrorMessage } from '@/lib/apiError'
import { usePlaceSearchQuery } from '../hooks'
import type { Place } from '../places'

const DEBOUNCE_MS = 400

export interface MapSearchBoxProps {
  onSelectPlace: (place: Place) => void
}

export function MapSearchBox({ onSelectPlace }: MapSearchBoxProps) {
  const listId = useId()
  const [query, setQuery] = useState('')
  const [debouncedQuery, setDebouncedQuery] = useState('')

  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedQuery(query.trim()), DEBOUNCE_MS)
    return () => window.clearTimeout(handle)
  }, [query])

  const { data: results = [], isFetching, isError, error } = usePlaceSearchQuery(debouncedQuery)
  const showResults = debouncedQuery.length >= 2 && results.length > 0

  return (
    <div className="relative max-w-xl">
      <Input
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        placeholder="Search places…"
        aria-label="Search places"
        aria-controls={listId}
        aria-expanded={showResults}
        autoComplete="off"
      />
      {isFetching && debouncedQuery.length >= 2 && (
        <p className="mt-1 text-xs text-gray-600" role="status">
          Searching…
        </p>
      )}
      {isError && debouncedQuery.length >= 2 && (
        <p className="mt-1 text-xs text-red-700" role="alert">
          {getErrorMessage(error, 'Place search failed.')}
        </p>
      )}
      {showResults && (
        <ul
          id={listId}
          className="absolute left-0 right-0 top-full z-20 mt-1 max-h-48 overflow-y-auto rounded-md border border-gray-200 bg-white shadow-lg"
          role="listbox"
        >
          {results.map((place) => (
            <li key={place.placeId} role="option">
              <button
                type="button"
                className="w-full px-3 py-2 text-left text-sm text-gray-800 hover:bg-blue-50"
                onClick={() => {
                  onSelectPlace(place)
                  setQuery(place.displayName)
                }}
              >
                {place.displayName}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
