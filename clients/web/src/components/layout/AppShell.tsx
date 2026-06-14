import { Outlet } from 'react-router-dom'
import { Navbar } from './Navbar'
import { Sidebar } from './Sidebar'

// App frame for the authenticated/main area. Renders the matched child route
// in <Outlet />, so it is used as a layout route in the router.
export function AppShell() {
  return (
    <div className="flex min-h-screen flex-col bg-gray-50">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-white focus:px-3 focus:py-2 focus:shadow"
      >
        Skip to main content
      </a>
      <Navbar />
      <div className="mx-auto flex w-full max-w-5xl flex-1">
        <Sidebar />
        <main id="main-content" className="flex-1 p-4">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
