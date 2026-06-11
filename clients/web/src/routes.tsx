import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/components/layout'
import { LoginPage, RegisterPage } from '@/features/auth'
import { HomePage } from '@/pages/HomePage'
import { NotFoundPage } from '@/pages/NotFoundPage'

// Auth pages mount outside the AppShell (their own centered layout). Feature
// routes are added under the AppShell layout as each vertical slice lands.
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppShell />,
    children: [{ index: true, element: <HomePage /> }],
  },
  { path: '*', element: <NotFoundPage /> },
])
