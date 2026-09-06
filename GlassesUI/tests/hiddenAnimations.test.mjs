import assert from 'node:assert/strict'
import test from 'node:test'
import { suspendHiddenAnimations } from '../../SharedUI/hiddenAnimations.mjs'

function surface(opacity, exits = []) {
  const classes = new Set()
  return {
    classList: { add: name => classes.add(name), remove: name => classes.delete(name) },
    isConnected: true,
    opacity,
    ownerDocument: { defaultView: { getComputedStyle: element => ({ opacity: element.opacity }) } },
    getAnimations: () => exits,
    suspended: () => classes.has('has-suspended-animations'),
  }
}

function transition() {
  let finish, cancel
  return {
    effect: { getComputedTiming: () => ({ endTime: 1000 }) },
    finished: new Promise((resolve, reject) => { finish = resolve; cancel = reject }),
    finish: () => finish(),
    cancel: () => cancel(new Error('Transition replaced')),
  }
}

const settle = () => new Promise(resolve => setImmediate(resolve))

test('older WebViews without animation inspection keep their existing effects', () => {
  const element = surface('0')
  element.getAnimations = undefined
  assert.equal(suspendHiddenAnimations(element, true), undefined)
  assert.equal(element.suspended(), false)
})

test('decorations keep moving throughout the exit fade and resume on reveal', async () => {
  const exit = transition()
  const element = surface('0.5', [exit])
  const reveal = suspendHiddenAnimations(element, true)
  assert.equal(element.suspended(), false)
  element.opacity = '0'
  exit.finish()
  await settle()
  assert.equal(element.suspended(), true)
  reveal()
  assert.equal(element.suspended(), false)
})

test('a quick reveal cannot be suspended by an old exit completion', async () => {
  const exit = transition()
  const element = surface('0', [exit])
  suspendHiddenAnimations(element, true)()
  exit.finish()
  await settle()
  assert.equal(element.suspended(), false)
})

test('no-transition and reduced-motion exits suspend immediately only when fully invisible', () => {
  const element = surface('0')
  suspendHiddenAnimations(element, true)
  assert.equal(element.suspended(), true)
  suspendHiddenAnimations(element, false)
  assert.equal(element.suspended(), false)
  element.opacity = '0.1'
  suspendHiddenAnimations(element, true)
  assert.equal(element.suspended(), false)
})

test('cancelled transitions are safe and detached surfaces are not retained as suspended', async () => {
  const exit = transition()
  const element = surface('0', [exit])
  suspendHiddenAnimations(element, true)
  element.isConnected = false
  exit.cancel()
  await settle()
  assert.equal(element.suspended(), false)
})
