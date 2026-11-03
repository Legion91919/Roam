import type { Preview } from '@storybook/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
// Same fonts and stylesheet as src/main.tsx, so stories look exactly like the app.
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/jetbrains-mono/500.css'
import 'material-symbols/outlined.css'
import '../src/styles.css'

// Feature components may read cached data; stories never reach an API (queries stay idle).
const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, enabled: false } } })

const preview: Preview = {
  tags: ['autodocs'],
  parameters: {
    layout: 'padded',
    controls: { expanded: true, matchers: { color: /(background|color)$/i } },
    backgrounds: {
      default: 'page',
      values: [
        { name: 'page', value: '#f8f9ff' },
        { name: 'white', value: '#ffffff' },
      ],
    },
    viewport: {
      viewports: {
        phone: { name: 'Phone (390)', styles: { width: '390px', height: '844px' } },
        tablet: { name: 'Tablet (820)', styles: { width: '820px', height: '1180px' } },
        desktop: { name: 'Desktop (1440)', styles: { width: '1440px', height: '900px' } },
      },
    },
  },
  decorators: [(Story) => <QueryClientProvider client={queryClient}><Story /></QueryClientProvider>],
}

export default preview
