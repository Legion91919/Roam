import type { StorybookConfig } from '@storybook/react-vite'

// The component library catalogue: every components/ui primitive plus the main feature components,
// rendered with the app's real Tailwind theme and fonts. npm run storybook → http://localhost:6006
const config: StorybookConfig = {
  stories: ['../src/**/*.stories.@(ts|tsx)'],
  addons: ['@storybook/addon-essentials', '@storybook/addon-a11y'],
  framework: { name: '@storybook/react-vite', options: {} },
  core: { disableTelemetry: true },
  docs: { autodocs: 'tag' },
}

export default config
