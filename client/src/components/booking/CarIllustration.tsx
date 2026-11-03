import type { VehicleCategory } from '../../api/types'

/**
 * Flat side-profile silhouettes per category (DESIGN.md: "precise vector vehicle profiles,
 * no stock photography"). Hand-drawn SVG, so no image hosting and no licensing questions.
 */
const bodies: Record<VehicleCategory, { body: string; glass: string; wheels: [number, number] }> = {
  Economy: {
    body: 'M22 74 L22 60 Q24 52 38 50 L66 47 L92 29 Q98 25 108 25 L156 25 Q166 25 172 33 L186 49 Q204 51 210 60 L212 74 Z',
    glass: 'M96 32 Q100 29 108 29 L130 29 L130 46 L74 47 Z M135 29 L154 29 Q162 29 167 35 L178 47 L135 46 Z',
    wheels: [64, 176],
  },
  Compact: {
    body: 'M18 74 L18 60 Q20 52 34 50 L64 46 L92 28 Q98 24 110 24 L158 24 Q170 24 178 32 L194 48 Q212 51 218 60 L220 74 Z',
    glass: 'M96 31 Q101 28 110 28 L134 28 L134 45 L72 46 Z M139 28 L157 28 Q166 28 172 34 L185 46 L139 45 Z',
    wheels: [62, 182],
  },
  Suv: {
    body: 'M16 76 L16 54 Q18 46 32 44 L56 42 L76 20 Q80 15 90 15 L196 15 Q204 15 206 22 L210 42 Q222 45 224 54 L224 76 Z',
    glass: 'M82 21 Q85 19 91 19 L130 19 L130 40 L62 41 Z M135 19 L165 19 L165 40 L135 40 Z M170 19 L196 19 Q200 19 201 23 L204 40 L170 40 Z',
    wheels: [60, 184],
  },
  Luxury: {
    body: 'M12 72 L12 60 Q14 53 30 51 L76 46 L106 28 Q114 24 126 24 L166 24 Q178 24 188 32 L204 45 Q222 48 228 57 L230 72 Z',
    glass: 'M111 31 Q117 28 126 28 L148 28 L148 44 L88 45 Z M153 28 L165 28 Q176 28 184 35 L194 44 L153 44 Z',
    wheels: [66, 192],
  },
}

export function CarIllustration({ category, muted = false, className = '' }: { category: VehicleCategory; muted?: boolean; className?: string }) {
  const car = bodies[category]
  return (
    <svg viewBox="0 0 240 96" className={className} role="img" aria-label={`${category} vehicle illustration`}>
      <ellipse cx="120" cy="86" rx="104" ry="5" fill="#141c28" opacity="0.06" />
      <path d={car.body} fill={muted ? '#d2daeb' : '#c9d3e3'} stroke={muted ? '#bec9c5' : '#53615f'} strokeWidth="1.5" strokeLinejoin="round" />
      <path d={car.glass} fill={muted ? '#eff3ff' : '#e6eeff'} stroke={muted ? '#bec9c5' : '#53615f'} strokeWidth="1" strokeLinejoin="round" />
      <line x1="40" y1="62" x2="200" y2="62" stroke={muted ? '#bec9c5' : '#53615f'} strokeWidth="1" opacity="0.4" />
      {car.wheels.map((x) => (
        <g key={x}>
          <circle cx={x} cy="74" r="13" fill={muted ? '#bec9c5' : '#29313d'} />
          <circle cx={x} cy="74" r="5.5" fill={muted ? '#eff3ff' : '#dbe3f3'} />
        </g>
      ))}
    </svg>
  )
}

const thumbSizes = { sm: 'w-20 p-1.5 [&>svg]:h-10', md: 'w-24 p-2 [&>svg]:h-10', lg: 'w-28 p-2 [&>svg]:h-14' } as const

/** The car drawing in a small tinted box: lists, summaries and the car picker. */
export function CarThumb({ category, size = 'sm', muted = false, onWhite = false, hideOnPhone = false }: {
  category: VehicleCategory
  size?: keyof typeof thumbSizes
  muted?: boolean
  /** White box (on a tinted panel) instead of tinted (on a white card). */
  onWhite?: boolean
  hideOnPhone?: boolean
}) {
  return (
    <span className={`${hideOnPhone ? 'hidden sm:block' : 'block'} shrink-0 rounded-lg ${onWhite ? 'bg-white' : 'bg-surface-container-low'} ${thumbSizes[size]}`}>
      <CarIllustration category={category} muted={muted} className="w-full" />
    </span>
  )
}
