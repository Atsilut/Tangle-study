import { describe, expect, it } from 'vitest'
import { buildMapPinBoundsQuery, sanitizeBoundsForQuery } from './api'

describe('buildMapPinBoundsQuery', () => {
  it('serializes bounding box query params with fixed precision', () => {
    const query = buildMapPinBoundsQuery({
      minLatitude: 37.49053607035788,
      maxLatitude: 37.64238654169627,
      minLongitude: 126.84101422118835,
      maxLongitude: 127.11498577880542,
    })

    expect(query).toBe(
      'minLatitude=37.490536&maxLatitude=37.642387&minLongitude=126.841014&maxLongitude=127.114986',
    )
  })

  it('clamps out-of-range viewport bounds to valid query values', () => {
    const sanitized = sanitizeBoundsForQuery({
      minLatitude: -95,
      maxLatitude: 95,
      minLongitude: -540,
      maxLongitude: 540,
    })

    expect(sanitized).toEqual({
      minLatitude: -90,
      maxLatitude: 90,
      minLongitude: -180,
      maxLongitude: 180,
    })
  })

  it('returns null for degenerate bounds', () => {
    expect(
      sanitizeBoundsForQuery({
        minLatitude: 10,
        maxLatitude: 10,
        minLongitude: 0,
        maxLongitude: 1,
      }),
    ).toBeNull()
  })
})
