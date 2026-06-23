import { useId, type ReactNode } from 'react'
import { cn } from '@/lib/cn'

export interface FormFieldProps {
  label: string
  error?: string
  hint?: string
  required?: boolean
  className?: string
  // Render-prop receives the generated id so the control wires up
  // htmlFor/aria-describedby for accessibility.
  children: (props: { id: string; describedBy?: string; invalid: boolean }) => ReactNode
}

export function FormField({
  label,
  error,
  hint,
  required,
  className,
  children,
}: FormFieldProps) {
  const id = useId()
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = error ? errorId : hint ? hintId : undefined

  return (
    <div className={cn('flex flex-col gap-1', className)}>
      <label htmlFor={id} className="text-sm font-medium text-gray-900">
        {label}
        {required && <span className="ml-0.5 text-red-600">*</span>}
      </label>
      {children({ id, describedBy, invalid: Boolean(error) })}
      {hint && !error && (
        <p id={hintId} className="text-xs text-gray-500">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-xs text-red-600">
          {error}
        </p>
      )}
    </div>
  )
}
