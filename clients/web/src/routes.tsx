import { createBrowserRouter } from 'react-router-dom'
import { AppShell, ProtectedRoute } from '@/components/layout'
import { LoginPage, RegisterPage } from '@/features/auth'
import { BlocksPage } from '@/features/blocks'
import { FriendsPage } from '@/features/friends'
import {
  GroupCreatePage,
  GroupDetailPage,
  GroupEditPage,
  GroupMembersPage,
  GroupsPage,
  InvitationsPage,
  ApplicationsPage,
  GroupApplicationsPage,
  GroupBoardsPage,
  GroupBoardPostsPage,
  GroupBoardPostDetailPage,
} from '@/features/groups'
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
        path: 'friends',
        element: (
          <ProtectedRoute>
            <FriendsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'blocks',
        element: (
          <ProtectedRoute>
            <BlocksPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups',
        element: (
          <ProtectedRoute>
            <GroupsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/new',
        element: (
          <ProtectedRoute>
            <GroupCreatePage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id',
        element: (
          <ProtectedRoute>
            <GroupDetailPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id/edit',
        element: (
          <ProtectedRoute>
            <GroupEditPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id/members',
        element: (
          <ProtectedRoute>
            <GroupMembersPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id/applications',
        element: (
          <ProtectedRoute>
            <GroupApplicationsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id/boards',
        element: (
          <ProtectedRoute>
            <GroupBoardsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id/boards/:boardId',
        element: (
          <ProtectedRoute>
            <GroupBoardPostsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'groups/:id/boards/:boardId/posts/:postId',
        element: (
          <ProtectedRoute>
            <GroupBoardPostDetailPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'invitations',
        element: (
          <ProtectedRoute>
            <InvitationsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'applications',
        element: (
          <ProtectedRoute>
            <ApplicationsPage />
          </ProtectedRoute>
        ),
      },
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
