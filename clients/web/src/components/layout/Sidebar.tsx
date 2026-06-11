import { NavLink } from 'react-router-dom'
import { cn } from '@/lib/cn'

interface NavItem {
  to: string
  label: string
}

// Feature routes register their nav entries here as slices land.
const navItems: NavItem[] = [
  { to: '/', label: 'Home' },
  { to: '/posts', label: 'Posts' },
  { to: '/users', label: 'Users' },
  { to: '/friends', label: 'Friends' },
  { to: '/settings', label: 'Settings' },
]

export function Sidebar() {
  return (
    <aside className="w-48 shrink-0 border-r border-gray-200 bg-white">
      <nav className="flex flex-col gap-1 p-3" aria-label="Primary">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            className={({ isActive }) =>
              cn(
                'rounded-md px-3 py-2 text-sm font-medium',
                isActive ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-100',
              )
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  )
}
