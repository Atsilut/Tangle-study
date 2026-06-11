import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { invalid, className, ...props },
  ref,
) {
  return (
    <input
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(
        'block w-full rounded-md border-0 px-3 py-2 text-sm text-gray-900 shadow-sm',
        'ring-1 ring-inset placeholder:text-gray-400',
        'focus:ring-2 focus:ring-inset focus:ring-blue-600',
        'disabled:cursor-not-allowed disabled:bg-gray-50 disabled:opacity-60',
        invalid ? 'ring-red-500 focus:ring-red-500' : 'ring-gray-300',
        className,
      )}
      {...props}
    />
  )
})
