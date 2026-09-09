import { resolveI18nImport } from './i18n-test-helper.mjs'
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'

const { code } = await transformWithEsbuild(
  await readFile(new URL('../src/tutorialState.ts', import.meta.url), 'utf8'),
  'tutorialState.ts',
  { target: 'es2022' },
)
const { initialTutorialState, tutorialReducer, tutorialCommandForKey, hasSeenRemoteTutorial, rememberRemoteTutorial, tutorialStorageKey } =
  await import(`data:text/javascript;base64,${Buffer.from(resolveI18nImport(code)).toString('base64')}`)
const command = (state, value) => tutorialReducer(state, { type: 'command', command: value })
const advance = (state) => tutorialReducer(state, { type: 'advance' })

test('requires all six real commands and ignores premature or repeated inputs', () => {
  let state = command(initialTutorialState(), 'enter')
  assert.equal(state.phase, 'practice')
  state = command(state, 'left')
  assert.equal(state.step, 0)
  assert.equal(state.feedback, 'retry')
  assert.equal(advance(state), state, 'a timer alone cannot pass a lesson')
  for (const expected of ['right', 'down', 'left', 'up', 'enter', 'back']) {
    state = command(state, expected)
    assert.equal(state.feedback, 'success')
    assert.equal(command(state, expected), state, 'rapid inputs are consumed during feedback')
    state = advance(state)
  }
  assert.equal(state.phase, 'complete')
  assert.equal(state.outcome, null, 'completion waits for the user to return to browsing')
  assert.equal(command(state, 'enter').outcome, 'completed')
})

test('back pauses ordinary lessons, resumes the same progress, and exits only on selection', () => {
  let state = advance(command(command(initialTutorialState(), 'enter'), 'right'))
  state = command(state, 'back')
  assert.equal(state.exitOpen, true)
  assert.equal(state.choice, 0)
  assert.equal(advance(state), state)
  state = command(state, 'back')
  assert.equal(state.exitOpen, false)
  assert.equal(state.step, 1)
  state = command(state, 'back')
  state = command(state, 'right')
  assert.equal(state.outcome, null)
  state = command(state, 'enter')
  assert.equal(state.outcome, 'skipped')
  assert.equal(command(state, 'down'), state, 'late queued input cannot change the outcome')
})

test('an explicit exit during success freezes the lesson until it is resumed', () => {
  let state = command(command(initialTutorialState(), 'enter'), 'right')
  state = tutorialReducer(state, { type: 'exit' })
  assert.equal(advance(state), state)
  state = command(state, 'enter')
  assert.equal(state.exitOpen, false)
  assert.equal(advance(state).step, 1)
})

test('welcome can be skipped and completion can restart cleanly', () => {
  assert.equal(command(initialTutorialState(), 'back').outcome, 'skipped')
  assert.equal(command(command(initialTutorialState(), 'right'), 'enter').outcome, 'skipped')
  let state = command(initialTutorialState(), 'enter')
  for (const input of ['right', 'down', 'left', 'up', 'enter', 'back']) state = advance(command(state, input))
  state = command(command(state, 'right'), 'enter')
  assert.equal(state.phase, 'practice')
  assert.equal(state.step, 0)
  assert.equal(state.feedback, 'idle')
  assert.equal(state.outcome, null)
})

test('keyboard equivalents match the native bridge without accepting unrelated commands', () => {
  assert.deepEqual(['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp', 'Enter', 'Escape'].map(tutorialCommandForKey),
    ['right', 'down', 'left', 'up', 'enter', 'back'])
  assert.deepEqual(['D', 's', 'a', 'W', ' ', 'Backspace'].map(tutorialCommandForKey),
    ['right', 'down', 'left', 'up', 'enter', 'back'])
  assert.equal(tutorialCommandForKey('volume:50'), undefined)
  assert.equal(tutorialCommandForKey('1'), undefined)
})

test('stores only a versioned outcome, tolerates unavailable storage and ignores unknown values', () => {
  const values = new Map()
  globalThis.window = { localStorage: { getItem: (key) => values.get(key), setItem: (key, value) => values.set(key, value) } }
  try {
    assert.equal(hasSeenRemoteTutorial(), false)
    values.set(tutorialStorageKey, 'not-an-outcome')
    assert.equal(hasSeenRemoteTutorial(), false)
    rememberRemoteTutorial('skipped')
    assert.equal(hasSeenRemoteTutorial(), true)
    rememberRemoteTutorial('completed')
    assert.deepEqual([...values], [[tutorialStorageKey, 'completed']])
    Object.defineProperty(window, 'localStorage', { get() { throw new Error('Storage disabled') } })
    assert.equal(hasSeenRemoteTutorial(), false)
    assert.doesNotThrow(() => rememberRemoteTutorial('completed'))
  } finally {
    delete globalThis.window
  }
})
