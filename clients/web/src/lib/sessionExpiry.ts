let sessionExpiryHandled = false

export function resetSessionExpiryGuard() {
  sessionExpiryHandled = false
}

/** Returns true the first time per browser session; false on subsequent calls. */
export function claimSessionExpiryHandling(): boolean {
  if (sessionExpiryHandled) return false
  sessionExpiryHandled = true
  return true
}
