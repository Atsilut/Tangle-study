import { createBrowserRouter } from 'react-router-dom'
import { AppShell, ProtectedRoute } from '@/components/layout'
import { LoginPage, RegisterPage } from '@/features/auth'
import {
  PostCreatePage,
  PostDetailPage,
  PostEditPage,
  PostsListPage,
} from '@/features/posts'
import { SettingsPage, UserProfilePage, UsersListPage } from '@/features/users'
import { HomePage } from '@/pages/HomePage'
import { NotFoundPage } from '@/pages/NotFoundPage'

// Auth pages mount outside the AppShell (their own centered layout). Feature
// routes are added under the AppShell layout as each vertical slice lands.
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'posts', element: <PostsListPage /> },
      {
        path: 'posts/new',
        element: (
          <ProtectedRoute>
            <PostCreatePage />
          </ProtectedRoute>
        ),
      },
      { path: 'posts/:id', element: <PostDetailPage /> },
      {
        path: 'posts/:id/edit',
        element: (
          <ProtectedRoute>
            <PostEditPage />
          </ProtectedRoute>
        ),
      },
      { path: 'users', element: <UsersListPage /> },
      { path: 'users/:id', element: <UserProfilePage /> },
      {
        path: 'settings',
        element: (
          <ProtectedRoute>
            <SettingsPage />
          </ProtectedRoute>
        ),
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
])
