/** Fuel in eighths, as on the dashboard. Interactive when onChange is given. */
export function FuelGauge({ value, onChange }: { value: number | null; onChange?: (eighths: number) => void }) {
  const label = value === null ? 'Not recorded' : value === 8 ? 'Full' : value === 0 ? 'Empty' : `${value}/8`
  return (
    <div>
      <div role={onChange ? 'radiogroup' : 'img'} aria-label={`Fuel level: ${label}`} className="flex gap-1">
        {Array.from({ length: 8 }, (_, i) => {
          const filled = value !== null && i < value
          const cell = <span className={`block h-8 w-full rounded ${filled ? 'bg-primary-container' : 'bg-surface-container-high'}`} />
          return onChange ? (
            <button key={i} type="button" role="radio" aria-checked={value === i + 1} aria-label={`${i + 1} eighths`} className="flex-1" onClick={() => onChange(i + 1 === value ? i : i + 1)}>
              {cell}
            </button>
          ) : (
            <span key={i} className="flex-1">{cell}</span>
          )
        })}
      </div>
      <div className="mt-1 flex justify-between text-body-sm text-secondary"><span>E</span><span className="font-medium text-ink">{label}</span><span>F</span></div>
    </div>
  )
}
