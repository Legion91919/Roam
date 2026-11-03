import { Icon, Stack, Text, Toggle } from '../ui'

interface Extra {
  id: 'gps' | 'childSeat'
  icon: string
  title: string
  description: string
  price: string
}

const EXTRAS: Extra[] = [
  { id: 'gps', icon: 'explore', title: 'GPS navigation', description: 'Live traffic across Gauteng, KZN and the Western Cape.', price: 'R 60,00 / day · capped at R 300,00' },
  { id: 'childSeat', icon: 'child_care', title: 'Child safety seat', description: 'Isofix certified, inspected before every departure.', price: 'R 50,00 / day · capped at R 250,00' },
]

/** Screen 3 extras with toggles. Prices shown here are copy; the charged amounts come from the quote. */
export function ExtrasList({ value, onChange }: { value: { gps: boolean; childSeat: boolean }; onChange: (changes: Partial<{ gps: boolean; childSeat: boolean }>) => void }) {
  return (
    <Stack gap={3}>
      {EXTRAS.map((extra) => {
        const on = value[extra.id]
        return (
          <div key={extra.id} className={`flex items-center gap-4 rounded-card p-4 transition-colors ${on ? 'bg-accent-surface/60' : 'bg-surface-container-low'}`}>
            <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-full ${on ? 'bg-primary-fixed text-primary' : 'bg-surface-container text-secondary'}`}>
              <Icon name={extra.icon} size={22} />
            </span>
            <Stack gap={0} grow>
              <Text size="lg" tone="strong" weight="medium">{extra.title}</Text>
              <Text size="sm" tone="muted">{extra.description}</Text>
              <Text mono tone={on ? 'brand' : 'default'} className="mt-0.5">{extra.price}</Text>
            </Stack>
            <Toggle checked={on} onChange={(checked) => onChange({ [extra.id]: checked })} label={extra.title} />
          </div>
        )
      })}
    </Stack>
  )
}
