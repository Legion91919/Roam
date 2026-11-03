import { useEffect, useId, useMemo, useRef, useState } from 'react'
import type { Branch } from '../../api/types'
import { Icon } from '../ui'

/**
 * Searchable branch combobox: type a city, code or suburb ("sand", "CPT", "airport"); results are
 * grouped by city with an airport / city-branch icon. Keyboard: arrows, Enter, Escape.
 */
export function BranchPicker({ branches, value, onChange, label }: { branches: Branch[] | undefined; value: string; onChange: (id: string) => void; label: string }) {
  const id = useId()
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const root = useRef<HTMLDivElement>(null)
  const selected = branches?.find((b) => b.id === value)

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase()
    const list = branches ?? []
    return q ? list.filter((b) => `${b.code} ${b.name} ${b.city} ${b.address} ${b.type}`.toLowerCase().includes(q)) : list
  }, [branches, query])

  const grouped = useMemo(() => {
    const groups = new Map<string, Branch[]>()
    for (const b of matches) groups.set(b.city, [...(groups.get(b.city) ?? []), b])
    return [...groups.entries()]
  }, [matches])

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (root.current && !root.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [])

  const choose = (branch: Branch) => {
    onChange(branch.id)
    setQuery('')
    setOpen(false)
  }

  return (
    <div ref={root} className="relative">
      <label htmlFor={id} className="mb-2 block text-label-md text-secondary">{label}</label>
      <div className="relative">
        <Icon name={selected?.type === 'City' ? 'storefront' : 'flight_land'} size={20} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-secondary" />
        <input
          id={id}
          role="combobox"
          aria-expanded={open}
          aria-controls={`${id}-list`}
          autoComplete="off"
          className="input pl-11 pr-10"
          placeholder={branches ? 'Search city, airport or branch' : 'Loading branches…'}
          value={open ? query : selected ? `${selected.code} · ${selected.name}` : ''}
          onFocus={() => { setOpen(true); setActive(0) }}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); setActive(0) }}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setActive((a) => Math.min(a + 1, matches.length - 1)) }
            if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)) }
            if (e.key === 'Enter' && open && matches[active]) { e.preventDefault(); choose(matches[active]) }
            if (e.key === 'Escape') setOpen(false)
          }}
        />
        <Icon name={open ? 'expand_less' : 'expand_more'} size={20} className="pointer-events-none absolute right-3.5 top-1/2 -translate-y-1/2 text-secondary" />
      </div>

      {open && (
        <ul id={`${id}-list`} role="listbox" className="absolute z-30 mt-2 max-h-80 w-full overflow-auto rounded-card border border-line bg-white py-2 shadow-overlay">
          {grouped.length === 0 && <li className="px-4 py-3 text-body-md text-secondary">No branches match "{query}".</li>}
          {grouped.map(([city, list]) => (
            <li key={city}>
              <p className="px-4 pb-1 pt-2 text-label-sm uppercase tracking-wider text-secondary">{city}</p>
              <ul>
                {list.map((b) => {
                  const index = matches.indexOf(b)
                  return (
                    <li
                      key={b.id}
                      role="option"
                      aria-selected={b.id === value}
                      onMouseDown={(e) => { e.preventDefault(); choose(b) }}
                      onMouseEnter={() => setActive(index)}
                      className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 ${index === active ? 'bg-surface-container-low' : ''}`}
                    >
                      <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${b.type === 'Airport' ? 'bg-accent-surface text-primary-container' : 'bg-surface-container text-on-surface-variant'}`}>
                        <Icon name={b.type === 'Airport' ? 'flight' : 'storefront'} size={18} />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-body-md text-ink"><span className="font-mono text-mono-code">{b.code}</span> · {b.name}</span>
                        <span className="block truncate text-body-sm text-secondary">{b.type === 'Airport' ? 'Airport terminal' : 'City branch'} · {b.address}</span>
                      </span>
                      {b.id === value && <Icon name="check" size={18} className="text-primary-container" />}
                    </li>
                  )
                })}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
