import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

function developmentCredentials(): Plugin {
  return {
    name: 'jellyfin-development-credentials',
    apply: 'serve',
    configureServer(server) {
      server.middlewares.use('/__jellyfin-dev-config', (_request, response) => {
        response.setHeader('Content-Type', 'application/json; charset=utf-8')
        response.setHeader('Cache-Control', 'no-store')
        try {
          const path = resolve(import.meta.dirname, '../.jellyfin-dev.json')
          const config = JSON.parse(readFileSync(path, 'utf8')) as unknown
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
