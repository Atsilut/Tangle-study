import { createBrowserRouter } from 'react-router-dom'
import { AppShell } from '@/components/layout'
import { HomePage } from '@/pages/HomePage'
import { NotFoundPage } from '@/pages/NotFoundPage'

// Feature routes are added under the AppShell layout as each vertical slice
// lands (auth, posts, ...). Auth pages (login/register) will mount outside
// the shell later.
export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [{ index: true, element: <HomePage /> }],
  },
  { path: '*', element: <NotFoundPage /> },
])
