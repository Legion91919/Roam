import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { ApiError } from './api/client'
// Fonts are self-hosted from npm (no Google Fonts request at runtime: works offline and under strict CSP).
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/jetbrains-mono/500.css'
import 'material-symbols/outlined.css'
import './styles.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Don't retry 4xx answers (they won't change); retry network blips and 5xx twice.
      retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 2,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
