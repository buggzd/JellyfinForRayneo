import { readFile } from 'node:fs/promises'
import { transformWithEsbuild } from 'vite'

export async function loadSeriesSearch(file = new URL('../src/seriesSearch.ts', import.meta.url)) {
  // Use the existing transpiler without opening a development server or loading
  // the local Jellyfin credentials plugin. Resolve pinyin from this workspace.
  const source = (await readFile(file, 'utf8'))
    .replaceAll("'pinyin-pro'", JSON.stringify(import.meta.resolve('pinyin-pro')))
  const { code } = await transformWithEsbuild(source, 'seriesSearch.ts', { target: 'es2022' })
  return import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)
}
