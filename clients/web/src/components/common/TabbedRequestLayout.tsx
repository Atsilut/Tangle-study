import type { ReactNode } from 'react'
import { useId } from 'react'
import { Tabs, type TabItem } from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'

export interface TabbedRequestPanel {
  id: string
  isLoading: boolean
  isError: boolean
  error?: unknown
  onRetry?: () => void
  children: ReactNode
}

export interface TabbedRequestLayoutProps {
  tabs: TabItem[]
  activeId: string
  onTabChange: (id: string) => void
  panels: TabbedRequestPanel[]
}

/** Tabs plus one query-backed panel selected by activeId. */
export function TabbedRequestLayout({
  tabs,
  activeId,
  onTabChange,
  panels,
}: TabbedRequestLayoutProps) {
  const baseId = useId()
  const panelId = `${baseId}-panel`
  const panel = panels.find((p) => p.id === activeId)
  if (!panel) return null

  return (
    <>
      <Tabs
        tabs={tabs}
        activeId={activeId}
        onChange={onTabChange}
        panelId={panelId}
        idPrefix={baseId}
      />
      <div role="tabpanel" id={panelId} aria-labelledby={`${baseId}-tab-${activeId}`}>
        <QueryBoundary
        isLoading={panel.isLoading}
        isError={panel.isError}
        error={panel.error}
        onRetry={panel.onRetry}
      >
        {panel.children}
      </QueryBoundary>
      </div>
    </>
  )
}
