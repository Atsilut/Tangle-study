import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { RouterProvider } from 'react-router-dom'
import { queryClient } from '@/lib/queryClient'
import { router } from '@/routes'
import { getCurrentUserId, useAuthStore } from '@/stores/authStore'
import '@/index.css'

// Persisted sessions may lack userId; decode from JWT before first paint.
const { accessToken, userId } = useAuthStore.getState()
if (accessToken && userId == null) {
  const decoded = getCurrentUserId()
  if (decoded != null) useAuthStore.setState({ userId: decoded })
}

useAuthStore.persist.onFinishHydration(() => {
  const state = useAuthStore.getState()
  if (state.accessToken && state.userId == null) {
    const decoded = getCurrentUserId()
    if (decoded != null) useAuthStore.setState({ userId: decoded })
  }
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  </StrictMode>,
)
