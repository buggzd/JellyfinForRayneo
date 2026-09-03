import { readdirSync, readFileSync, rmSync } from 'node:fs'
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

function preserveUnityMetadata(): Plugin {
  const outputRoot = resolve(import.meta.dirname, '../Assets/StreamingAssets/GlassesUI')
  const assetRoot = resolve(outputRoot, 'assets')
  return {
    name: 'preserve-unity-metadata',
    apply: 'build',
    closeBundle() {
      try {
        const index = readFileSync(resolve(outputRoot, 'index.html'), 'utf8')
        for (const file of readdirSync(assetRoot)) {
          if (!/^index-.*\.(?:css|js)$/.test(file) || index.includes(`assets/${file}`)) continue
          rmSync(resolve(assetRoot, file), { force: true })
          rmSync(resolve(assetRoot, `${file}.meta`), { force: true })
        }
      } catch {
        // Unity creates the output folders and .meta files after the first build.
      }
    },
  }
}

export default defineConfig({
  base: './',
  plugins: [react(), developmentCredentials(), preserveUnityMetadata()],
  server: {
    host: '0.0.0.0',
    port: 4175,
  },
  build: {
    outDir: '../Assets/StreamingAssets/GlassesUI',
    emptyOutDir: false,
    sourcemap: false,
  },
})
