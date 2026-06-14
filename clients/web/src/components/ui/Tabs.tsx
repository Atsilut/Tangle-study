import { useId } from 'react'
import { cn } from '@/lib/cn'

export interface TabItem {
  id: string
  label: string
  count?: number
}

export interface TabsProps {
  tabs: TabItem[]
  activeId: string
  onChange: (id: string) => void
  className?: string
  /** When set, links tab buttons to a single shared panel via aria-controls. */
  panelId?: string
  /** Shared prefix for tab button ids (must match tabpanel aria-labelledby). */
  idPrefix?: string
}

export function Tabs({ tabs, activeId, onChange, className, panelId, idPrefix }: TabsProps) {
  const generatedId = useId()
  const baseId = idPrefix ?? generatedId

  return (
    <div role="tablist" className={cn('flex gap-1 border-b border-gray-200', className)}>
      {tabs.map((tab) => {
        const active = tab.id === activeId
        const tabId = `${baseId}-tab-${tab.id}`
        return (
          <button
            key={tab.id}
            id={tabId}
            role="tab"
            type="button"
            aria-selected={active}
            aria-controls={panelId}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(tab.id)}
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm font-medium',
              active
                ? 'border-blue-600 text-blue-700'
                : 'border-transparent text-gray-500 hover:text-gray-700',
            )}
          >
            {tab.label}
            {tab.count != null && tab.count > 0 && (
              <span className="ml-1.5 rounded-full bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600">
                {tab.count}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
