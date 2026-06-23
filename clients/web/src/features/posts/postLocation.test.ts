import { describe, expect, it } from 'vitest'
import { parsePostLocation } from './postLocation'

describe('parsePostLocation', () => {
  it('returns null for missing or invalid values', () => {
    expect(parsePostLocation(null)).toBeNull()
    expect(parsePostLocation({})).toBeNull()
    expect(parsePostLocation({ latitude: 'x', longitude: 1 })).toBeNull()
  })

  it('parses camelCase numeric coordinates', () => {
    expect(parsePostLocation({ latitude: 37.5, longitude: 126.9 })).toEqual({
      latitude: 37.5,
      longitude: 126.9,
    })
  })

  it('parses PascalCase and string coordinates from the API', () => {
    expect(parsePostLocation({ Latitude: '37.5665', Longitude: '126.9780' })).toEqual({
      latitude: 37.5665,
      longitude: 126.978,
    })
  })
})
