/** Material Symbols icon (the icon set Stitch used). `filled` switches to the solid variant. */
export function Icon({ name, size = 20, filled = false, className = '' }: { name: string; size?: number; filled?: boolean; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={`icon ${className}`}
      style={{ fontSize: size, fontVariationSettings: `'FILL' ${filled ? 1 : 0}` }}
    >
      {name}
    </span>
  )
}
