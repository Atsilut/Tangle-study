import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

type BadgeColor = 'gray' | 'blue' | 'green' | 'red' | 'yellow'

const colorClasses: Record<BadgeColor, string> = {
  gray: 'bg-gray-100 text-gray-700',
  blue: 'bg-blue-100 text-blue-700',
  green: 'bg-green-100 text-green-700',
  red: 'bg-red-100 text-red-700',
  yellow: 'bg-yellow-100 text-yellow-800',
}

export interface BadgeProps {
  color?: BadgeColor
  className?: string
  children: ReactNode
}

export function Badge({ color = 'gray', className, children }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        colorClasses[color],
        className,
      )}
    >
      {children}
    </span>
  )
}
