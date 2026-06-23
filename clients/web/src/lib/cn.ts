// Tiny class-name joiner: filters falsy values so components can compose
// conditional Tailwind classes without pulling in a dependency.
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ')
}
