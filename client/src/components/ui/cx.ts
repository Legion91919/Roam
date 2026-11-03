/** Joins class names, skipping empty values: cx('a', cond && 'b') -> "a b". Internal to the UI library. */
export function cx(...classes: (string | false | null | undefined)[]): string {
  return classes.filter(Boolean).join(' ')
}
