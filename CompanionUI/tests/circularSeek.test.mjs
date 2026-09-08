import assert from 'node:assert/strict'
import test from 'node:test'
import { CircularSeekGesture } from '../src/circularSeek.mjs'
import { parseSeekCommand } from '../../SharedUI/seekCommand.mjs'

function circle(direction = 1, interval = 50, offset = 0) {
  const gesture = new CircularSeekGesture(150, 250, 100)
  const output = []
  for (let i = 0; i <= 80; i++) {
    const angle = offset + direction * i * Math.PI / 40
    const value = gesture.move(150 + 100 * Math.cos(angle), 250 + 100 * Math.sin(angle), i * interval)
    if (value) output.push(value)
  }
  const tail = gesture.flush()
  if (tail) output.push(tail)
  return { gesture, output, total: output.reduce((sum, v) => sum + v, 0) }
}

test('clockwise seeks forward, counterclockwise seeks backward and faster rotation increases distance', () => {
  const slow = circle(1, 100), fast = circle(1, 16), reverse = circle(-1, 100)
  assert.ok(slow.total > 0)
  assert.ok(fast.total > slow.total * 2)
  assert.ok(reverse.total < 0)
  assert.ok(fast.output.every(value => Number.isInteger(value) && value <= 60))
  assert.ok(Math.abs(circle(1, 100, Math.PI - .1).total - slow.total) < 3)
})

test('straight swipes, jitter, center touches and teleports never become a rotation', () => {
  for (const points of [
    Array.from({ length: 30 }, (_, i) => [90 + i * 4, 150]),
    Array.from({ length: 30 }, (_, i) => [250 + i % 2, 250 + i % 2]),
    [[150, 250], [250, 250], [150, 350]],
    [[250, 250], [50, 250]],
  ]) {
    const gesture = new CircularSeekGesture(150, 250, 100)
    assert.ok(points.every(([x,y],i) => gesture.move(x,y,i*30) === 0))
    assert.equal(gesture.active, false)
  }
})

test('reversal changes sign and leaving the ring consumes the gesture without further seeks', () => {
  const { gesture } = circle(1, 50)
  const values=[]
  for(let i=1;i<=20;i++) {
    const a=-i*Math.PI/40
    values.push(gesture.move(150+100*Math.cos(a),250+100*Math.sin(a),4000+i*50))
  }
  assert.ok(values.some(v=>v<0))
  assert.equal(gesture.move(150,250,5050),0)
  assert.equal(gesture.active,true)
  assert.equal(gesture.flush(),0)
})

test('seek command accepts only nonzero bounded integer updates', () => {
  for (const seconds of [1, -1, 60, -60, 25]) assert.equal(parseSeekCommand(`seek:${seconds}`),seconds)
  for (const value of ['seek:0','seek:-0','seek:61','seek:-61','seek:1.5','seek:+1','seek:01','seek:1;alert(1)',null]) assert.equal(parseSeekCommand(value),null)
})

// Every move belongs to one finger-down stroke; no flush/re-arm between legs.
test('an uninterrupted stroke reverses immediately, including fine correction and a pause', () => {
  for (const offset of [0, Math.PI - .1]) {
    const gesture = new CircularSeekGesture(150, 250, 100)
    let time = 0
    const move = (angle, interval = 30) => {
      time += interval
      return gesture.move(150 + 100 * Math.cos(offset + angle), 250 + 100 * Math.sin(offset + angle), time)
    }
    const forward = []
    for (let i = 0; i <= 20; i++) forward.push(move(i * .08))
    assert.ok(forward.some(value => value > 0))
    // Less than the normal 150 ms update interval since the last forward send.
    assert.ok(move(1.48) < 0, 'reverse before lifting or waiting for the throttle')
    assert.ok(move(1.60) > 0, 'switch back within the same stroke')
    assert.equal(move(1.57), 0, 'sub-second correction accumulates')
    assert.ok(move(1.51) < 0, 'first whole reverse second is emitted without a throttle delay')
    assert.ok(move(1.39, 650) < 0, 'a pause must not discard the first reverse movement')
    assert.ok(gesture.flush() <= 0, 'no forward remainder after reversal')
    assert.equal(gesture.active, true)
    assert.equal(gesture.cancelled, false)
  }
})
