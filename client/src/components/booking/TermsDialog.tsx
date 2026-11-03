import type { TermsDocument } from '../../api/types'
import { Button, Dialog, Stack, Text } from '../ui'

/** The current rental terms, with "I accept these terms" (the version is sent with the booking). */
export function TermsDialog({ terms, open, onClose, onAccept }: { terms: TermsDocument | undefined; open: boolean; onClose: () => void; onAccept: () => void }) {
  return (
    <Dialog open={open} onClose={onClose} size="lg" eyebrow={`Version ${terms?.version ?? ''}`} title={terms?.title ?? 'Rental terms'}
      footer={<>
        <Button variant="secondary" onClick={onClose}>Close</Button>
        <Button icon="check" onClick={() => { onAccept(); onClose() }}>I accept these terms</Button>
      </>}>
      <Stack gap={4}>
        {terms?.sections.map((s) => (
          <section key={s.heading}>
            <Text size="lg" tone="strong" weight="medium">{s.heading}</Text>
            <Text className="mt-1">{s.body}</Text>
          </section>
        ))}
        <Text size="sm" tone="subtle">Sample terms for a fictional company (proof of concept).</Text>
      </Stack>
    </Dialog>
  )
}
