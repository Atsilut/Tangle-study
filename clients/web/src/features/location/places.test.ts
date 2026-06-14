import { describe, expect, it } from 'vitest'
import { buildPlaceReverseQuery, buildPlaceSearchQuery, parsePlace } from './places'

describe('places helpers', () => {
  it('builds search query for the location API', () => {
    expect(buildPlaceSearchQuery('Seoul', 3)).toBe('q=Seoul&limit=3')
  })

  it('builds reverse geocode query with fixed precision', () => {
    expect(buildPlaceReverseQuery(37.5665, 126.978)).toBe(
      'latitude=37.566500&longitude=126.978000',
    )
  })

  it('parses place search result', () => {
    const place = parsePlace({
      placeId: 'places/ChIJTest',
      displayName: 'Seoul, South Korea',
      latitude: 37.5665,
      longitude: 126.978,
    })

    expect(place.placeId).toBe('places/ChIJTest')
    expect(place.displayName).toBe('Seoul, South Korea')
    expect(place.latitude).toBeCloseTo(37.5665)
    expect(place.longitude).toBeCloseTo(126.978)
  })
})
