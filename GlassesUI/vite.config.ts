import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

function applicationVersion() {
  const path = resolve(import.meta.dirname, '../version.properties')
  const versionLine = readFileSync(path, 'utf8')
    .split(/\r?\n/)
    .find((line) => line.startsWith('versionName='))
  const version = versionLine?.slice('versionName='.length).trim() ?? ''
  if (!/^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?$/.test(version)) {
    throw new Error('Invalid versionName in version.properties.')
  }
  return version
}

function developmentCredentials(): Plugin {
  const configuredPath = process.env.RAYNEO_JELLYFIN_DEV_CONFIG?.trim()
  const developmentConfigPath = configuredPath
    ? resolve(configuredPath)
    : resolve(import.meta.dirname, '../.jellyfin-dev.json')

  return {
    name: 'jellyfin-development-credentials',
    apply: 'serve',
    configureServer(server) {
      server.middlewares.use('/__jellyfin-dev-config', (_request, response) => {
        response.setHeader('Content-Type', 'application/json; charset=utf-8')
        response.setHeader('Cache-Control', 'no-store')
        try {
          const config = JSON.parse(readFileSync(developmentConfigPath, 'utf8')) as unknown
          response.statusCode = 200
          response.end(JSON.stringify(config))
        } catch {
          response.statusCode = 404
          response.end(JSON.stringify({ error: 'Development Jellyfin configuration is unavailable.' }))
        }
      })
    },
  }
}

export default defineConfig({
  base: './',
  define: {
    __APP_VERSION__: JSON.stringify(applicationVersion()),
  },
  plugins: [react(), developmentCredentials()],
  server: {
    host: '0.0.0.0',
    port: 4175,
  },
  build: {
    outDir: '../AndroidApp/app/src/main/assets/GlassesUI',
    emptyOutDir: true,
    sourcemap: false,
  },
})
