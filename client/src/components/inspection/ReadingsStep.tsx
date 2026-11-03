import { useState } from 'react'
import type { DamageMark, DamageSeverity } from '../../api/types'
import { Button, Field, Grid, Icon, Inline, Input, Pill, Select, Stack, Text } from '../ui'
import { FuelGauge } from './FuelGauge'
import type { StepProps } from './inspectionDraft'

const SEVERITIES: DamageSeverity[] = ['Minor', 'Moderate', 'Severe']

/** Step 2: odometer (never below the last reading), fuel level and optional damage marks. */
export function ReadingsStep({ draft, setDraft, locked, minimumOdometer }: StepProps & { minimumOdometer?: number }) {
  const [mark, setMark] = useState<DamageMark>({ panel: '', description: '', severity: 'Minor' })
  const tooLow = minimumOdometer !== undefined && draft.odometer !== '' && Number(draft.odometer) < minimumOdometer
  const addMark = () => {
    setDraft({ ...draft, damage: [...draft.damage, { ...mark, panel: mark.panel.trim(), description: mark.description.trim() }] })
    setMark({ panel: '', description: '', severity: 'Minor' })
  }

  return (
    <Grid columns={{ base: 1, lg: 2 }} gap={6}>
      <Stack gap={5}>
        <Field label="Odometer (km)" error={tooLow ? 'Lower than the last reading.' : undefined}
          hint={minimumOdometer !== undefined ? `Last reading ${minimumOdometer.toLocaleString('en-ZA')} km. It can't go down.` : 'Reading on the dashboard now.'}>
          <Input type="number" min={0} inputMode="numeric" className="tabular" invalid={tooLow} disabled={locked}
            value={draft.odometer} onChange={(e) => setDraft({ ...draft, odometer: e.target.value })} />
        </Field>
        <Stack gap={2}>
          <Text as="span" size="label" tone="strong">Fuel level</Text>
          <FuelGauge value={draft.fuel} onChange={locked ? undefined : (fuel) => setDraft({ ...draft, fuel })} />
        </Stack>
      </Stack>

      <Stack gap={3}>
        <Text as="span" size="label" tone="strong">Damage marks <Text as="span" size="label" tone="muted">(optional, for anything the checklist doesn't cover)</Text></Text>
        {draft.damage.length > 0 && (
          <Stack as="ul" gap={2}>
            {draft.damage.map((d, i) => (
              <li key={i} className="rounded-card bg-surface-container-low px-3 py-2">
                <Inline gap={2} justify="between">
                  <Inline gap={2}><Pill tone={d.severity === 'Severe' ? 'danger' : 'warning'}>{d.severity}</Pill><Text as="span" tone="strong">{d.panel}: {d.description}</Text></Inline>
                  {!locked && (
                    <button type="button" aria-label="Remove damage mark" className="text-secondary hover:text-danger" onClick={() => setDraft({ ...draft, damage: draft.damage.filter((_, j) => j !== i) })}>
                      <Icon name="delete" size={18} />
                    </button>
                  )}
                </Inline>
              </li>
            ))}
          </Stack>
        )}
        {!locked && (
          <Grid columns={{ base: 1, sm: 2 }} gap={2}>
            <Input controlSize="sm" placeholder="Panel, e.g. Rear bumper" aria-label="Damage panel" value={mark.panel} onChange={(e) => setMark({ ...mark, panel: e.target.value })} />
            <Input controlSize="sm" placeholder="What, e.g. 5 cm scratch" aria-label="Damage description" value={mark.description} onChange={(e) => setMark({ ...mark, description: e.target.value })} />
            <Select controlSize="sm" aria-label="Damage severity" value={mark.severity} onChange={(e) => setMark({ ...mark, severity: e.target.value as DamageSeverity })}
              options={SEVERITIES.map((s) => ({ value: s, label: s }))} />
            <Button variant="secondary" icon="add" disabled={!mark.panel.trim() || !mark.description.trim()} onClick={addMark}>Add damage mark</Button>
          </Grid>
        )}
      </Stack>
    </Grid>
  )
}
