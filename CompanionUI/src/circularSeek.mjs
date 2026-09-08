const tau = Math.PI * 2
const wrap = angle => ((angle + Math.PI + tau) % tau) - Math.PI

// Coordinates use screen Y (down), so positive angular movement is clockwise.
export class CircularSeekGesture {
  constructor(x, y, radius) {
    this.center = { x, y, radius }
    this.reset()
  }

  reset() {
    this.previous = null
    this.heading = null
    this.turn = 0
    this.arc = 0
    this.pending = 0
    this.speed = 0
    this.direction = 0
    this.active = false
    this.cancelled = false
    this.lastEmit = 0
    this.reversalPending = false
  }

  move(x, y, time) {
    if (this.cancelled || ![x, y, time].every(Number.isFinite)) return 0
    const dx = x - this.center.x, dy = y - this.center.y
    const radius = Math.hypot(dx, dy)
    if (radius < this.center.radius * .68 || radius > this.center.radius * 1.32) {
      this.cancelled = true
      return 0
    }
    const next = { x, y, time, angle: Math.atan2(dy, dx) }
    const previous = this.previous
    if (!previous) {
      this.previous = next
      this.lastEmit = time
      return 0
    }
    const distance = Math.hypot(x - previous.x, y - previous.y)
    if (distance < 2) return 0
    const elapsed = time - previous.time
    const angle = wrap(next.angle - previous.angle)
    if (elapsed <= 0 || Math.abs(angle) > .8) {
      this.cancelled = true
      return 0
    }
    if (elapsed > 500) {
      this.heading = null
      this.turn = 0
      this.arc = 0
      this.pending = 0
      this.speed = 0
      // An armed dial stays armed while the finger rests. Account for the
      // first movement after the pause at its measured (low) speed.
      if (!this.active) {
        this.previous = next
        this.lastEmit = time
        return 0
      }
    }
    const heading = Math.atan2(y - previous.y, x - previous.x)
    if (this.heading !== null) this.turn += wrap(heading - this.heading)
    this.heading = heading
    this.previous = next
    this.arc += angle
    // Angular travel AND curved motion: a straight swipe/tap cannot arm the dial.
    if (!this.active) {
      if (Math.abs(this.arc) < .55 || Math.abs(this.turn) < .25 || this.arc * this.turn <= 0) return 0
      this.active = true
      this.pending = this.arc * 10
    }
    // Forget the old direction's remainder/speed immediately on reversal.
    const direction = Math.sign(angle)
    if (this.direction && direction && this.direction !== direction) {
      this.pending = 0
      this.speed = 0
      this.reversalPending = true
    }
    if (direction) this.direction = direction
    const speed = Math.min(8, Math.abs(angle) * 1000 / elapsed)
    this.speed += (speed - this.speed) * .35
    const gain = 10 * (1 + Math.min(5, this.speed * .7))
    this.pending = Math.max(-60, Math.min(60, this.pending + angle * gain))
    // Deliver the first whole second in the new direction immediately, even
    // when it takes several small moves to accumulate. Never round jitter up.
    if (!this.reversalPending && time - this.lastEmit < 150) return 0
    const seconds = this.flush()
    if (seconds) {
      this.lastEmit = time
      this.reversalPending = false
    }
    return seconds
  }

  flush() {
    if (!this.active || this.cancelled) return 0
    const seconds = Math.max(-60, Math.min(60, Math.trunc(this.pending)))
    this.pending -= seconds
    return seconds || 0
  }
}
