import { useState } from 'react'
import { Icon } from './Icon'

export function CopyButton({ text, label = 'Copy reference' }: { text: string; label?: string }) {
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* clipboard blocked: nothing to do */
    }
  }
  return (
    <button type="button" onClick={copy} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-white px-3 text-body-md text-ink hover:bg-slate-50">
      <Icon name={copied ? 'check' : 'content_copy'} size={18} />
      {copied ? 'Copied' : label}
    </button>
  )
}
