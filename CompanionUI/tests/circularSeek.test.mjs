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
