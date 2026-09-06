import { readFileSync } from 'node:fs'
import { defineConfig } from 'vite'

const version = readFileSync(new URL('../version.properties', import.meta.url), 'utf8')
const name = version.match(/^versionName=(.+)$/m)?.[1].trim()
const code = Number(version.match(/^versionCode=(\d+)$/m)?.[1])
if (!name || !Number.isSafeInteger(code) || code < 1) throw new Error('Invalid app version')

export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(name),
    __APP_VERSION_CODE__: JSON.stringify(code),
  },
})
